using Microsoft.EntityFrameworkCore;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Repositories;

internal sealed record ReconstructedPriceRevisionSet(
    Guid SetId,
    Guid InstrumentId,
    string Provider,
    IReadOnlyDictionary<DateOnly, Guid> RevisionIdsByBarDate,
    string Sha256);

internal sealed class PriceRevisionSetRepository(SwingAdviserDbContext dbContext)
{
    public const string HashAlgorithmId = AnalysisInputHashing.PriceRevisionSetHashAlgorithmId;

    public async Task<ReconstructedPriceRevisionSet> ReconstructAndVerifyAsync(
        Guid setId,
        CancellationToken cancellationToken = default)
    {
        var chain = await LoadParentChainAsync(setId, cancellationToken);
        var target = chain[^1];
        var members = new Dictionary<DateOnly, Guid>();

        foreach (var set in chain)
        {
            var changes = await dbContext.Set<PriceRevisionSetChangeRow>()
                .AsNoTracking()
                .Where(change => change.PriceRevisionSetId == set.Id)
                .OrderBy(change => change.Ordinal)
                .ToListAsync(cancellationToken);

            foreach (var change in changes)
            {
                ApplyChange(members, change);
            }

            await VerifySetMetadataAsync(set, members, cancellationToken);
        }

        var finalRevisions = await LoadAndVerifyRevisionGraphAsync(
            target,
            members,
            cancellationToken);
        var calculatedHash = CalculateSetHash(
            target.InstrumentId,
            target.Provider,
            finalRevisions.Select(item => (item.BarDate, item.Revision.ContentSha256)));

        if (!string.Equals(calculatedHash, target.SetSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Price revision set {target.Id} hash does not match its reconstructed members.");
        }

        return new ReconstructedPriceRevisionSet(
            target.Id,
            target.InstrumentId,
            target.Provider,
            new Dictionary<DateOnly, Guid>(members),
            calculatedHash);
    }

    public async Task<ReconstructedPriceRevisionSet> VerifyManifestAsync(
        Guid manifestId,
        CancellationToken cancellationToken = default)
    {
        var manifest = await dbContext.Set<AnalysisInputManifestRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == manifestId, cancellationToken);
        var run = await dbContext.Set<AnalysisRunRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == manifest.AnalysisRunId, cancellationToken);
        var set = await dbContext.Set<PriceRevisionSetRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == manifest.PriceRevisionSetId, cancellationToken);
        var reconstructed = await ReconstructAndVerifyAsync(
            manifest.PriceRevisionSetId,
            cancellationToken);

        var firstDate = reconstructed.RevisionIdsByBarDate.Count == 0
            ? (DateOnly?)null
            : reconstructed.RevisionIdsByBarDate.Keys.Min();
        var lastDate = reconstructed.RevisionIdsByBarDate.Count == 0
            ? (DateOnly?)null
            : reconstructed.RevisionIdsByBarDate.Keys.Max();

        if (manifest.InstrumentId != reconstructed.InstrumentId ||
            !string.Equals(manifest.PriceProvider, reconstructed.Provider, StringComparison.Ordinal) ||
            manifest.BarCount != reconstructed.RevisionIdsByBarDate.Count ||
            manifest.FirstBarDate != firstDate ||
            manifest.LastBarDate != lastDate ||
            !string.Equals(manifest.PriceRevisionSetSha256, reconstructed.Sha256, StringComparison.Ordinal) ||
            manifest.SelectedRecordedCutoffAtUtc != set.SelectedRecordedCutoffAtUtc ||
            manifest.SelectedAvailableCutoffAtUtc != set.SelectedAvailableCutoffAtUtc ||
            !string.Equals(manifest.SelectionRuleVersion, set.SelectorVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.PointInTimeStatus, set.PointInTimeStatus, StringComparison.Ordinal) ||
            manifest.SelectedRecordedCutoffAtUtc != run.RecordedCutoffAtUtc ||
            manifest.SelectedAvailableCutoffAtUtc != run.AnalyzedAtUtc ||
            (string.Equals(run.PointInTimeStatus, "Verified", StringComparison.Ordinal) &&
             !string.Equals(manifest.PointInTimeStatus, "Verified", StringComparison.Ordinal)) ||
            !string.Equals(manifest.SelectionRuleVersion, run.PriceSelectorVersion, StringComparison.Ordinal) ||
            (lastDate is { } lastBarDate && lastBarDate > run.EvaluationBarDate))
        {
            throw new InvalidDataException(
                $"Analysis input manifest {manifest.Id} does not match its price revision set.");
        }

        await VerifyManifestPointInTimeAsync(manifest, reconstructed, cancellationToken);

        return reconstructed;
    }

    internal static string CalculateSetHash(
        Guid instrumentId,
        string provider,
        IEnumerable<(DateOnly BarDate, string ContentSha256)> members) =>
        AnalysisInputHashing.CalculatePriceRevisionSetHash(
            new InstrumentId(instrumentId),
            provider,
            members.Select(member => (member.BarDate, new Sha256Hash(member.ContentSha256))))
        .Value;

    private async Task<List<PriceRevisionSetRow>> LoadParentChainAsync(
        Guid setId,
        CancellationToken cancellationToken)
    {
        var reversed = new List<PriceRevisionSetRow>();
        var visited = new HashSet<Guid>();
        var currentId = setId;
        Guid? expectedInstrumentId = null;
        string? expectedProvider = null;

        while (true)
        {
            if (!visited.Add(currentId))
            {
                throw new InvalidDataException($"Price revision set chain contains a cycle at {currentId}.");
            }

            var current = await dbContext.Set<PriceRevisionSetRow>()
                .AsNoTracking()
                .SingleAsync(row => row.Id == currentId, cancellationToken);

            expectedInstrumentId ??= current.InstrumentId;
            expectedProvider ??= current.Provider;
            if (current.InstrumentId != expectedInstrumentId ||
                !string.Equals(current.Provider, expectedProvider, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A price revision set parent belongs to another instrument/provider.");
            }

            reversed.Add(current);
            if (current.ParentSetId is not { } parentId)
            {
                break;
            }

            currentId = parentId;
        }

        reversed.Reverse();
        return reversed;
    }

    private async Task VerifySetMetadataAsync(
        PriceRevisionSetRow set,
        IReadOnlyDictionary<DateOnly, Guid> members,
        CancellationToken cancellationToken)
    {
        var firstDate = members.Count == 0 ? (DateOnly?)null : members.Keys.Min();
        var lastDate = members.Count == 0 ? (DateOnly?)null : members.Keys.Max();
        if (set.BarCount != members.Count || set.FirstBarDate != firstDate || set.LastBarDate != lastDate)
        {
            throw new InvalidDataException($"Price revision set {set.Id} count/date metadata is inconsistent.");
        }

        var revisionGraph = await LoadAndVerifyRevisionGraphAsync(set, members, cancellationToken);
        var calculatedHash = CalculateSetHash(
            set.InstrumentId,
            set.Provider,
            revisionGraph.Select(item => (item.BarDate, item.Revision.ContentSha256)));
        if (!string.Equals(calculatedHash, set.SetSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Price revision set {set.Id} hash does not match its reconstructed members.");
        }
    }

    private async Task<List<VerifiedPriceRevision>> LoadAndVerifyRevisionGraphAsync(
        PriceRevisionSetRow set,
        IReadOnlyDictionary<DateOnly, Guid> members,
        CancellationToken cancellationToken)
    {
        var revisionIds = members.Values.ToArray();
        var revisions = await dbContext.Set<DailyPriceRevisionRow>()
            .AsNoTracking()
            .Where(revision => revisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);

        if (revisions.Count != revisionIds.Length)
        {
            throw new InvalidDataException($"Price revision set {set.Id} references a missing price revision.");
        }

        var dailyPriceIds = revisions.Values.Select(revision => revision.DailyPriceId).Distinct().ToArray();
        var prices = await dbContext.Set<DailyPriceRow>()
            .AsNoTracking()
            .Where(price => dailyPriceIds.Contains(price.Id))
            .ToDictionaryAsync(price => price.Id, cancellationToken);
        var result = new List<VerifiedPriceRevision>(members.Count);

        foreach (var (barDate, revisionId) in members.OrderBy(item => item.Key))
        {
            var revision = revisions[revisionId];
            if (!prices.TryGetValue(revision.DailyPriceId, out var price) ||
                price.InstrumentId != set.InstrumentId ||
                !string.Equals(price.Provider, set.Provider, StringComparison.Ordinal) ||
                price.BarDate != barDate)
            {
                throw new InvalidDataException(
                    $"Price revision {revisionId} does not match set {set.Id}'s natural key.");
            }


            if (revision.RecordedAtUtc > set.SelectedRecordedCutoffAtUtc)
            {
                throw new InvalidDataException(
                    $"Price revision {revisionId} was recorded after set {set.Id}'s transaction cutoff.");
            }

            result.Add(new VerifiedPriceRevision(barDate, revision));
        }

        return result;
    }

    private async Task VerifyManifestPointInTimeAsync(
        AnalysisInputManifestRow manifest,
        ReconstructedPriceRevisionSet reconstructed,
        CancellationToken cancellationToken)
    {
        var revisionIds = reconstructed.RevisionIdsByBarDate.Values.ToArray();
        var revisions = await dbContext.Set<DailyPriceRevisionRow>()
            .AsNoTracking()
            .Where(revision => revisionIds.Contains(revision.Id))
            .ToListAsync(cancellationToken);

        foreach (var revision in revisions)
        {
            if (revision.RecordedAtUtc > manifest.SelectedRecordedCutoffAtUtc)
            {
                throw new InvalidDataException(
                    $"Price revision {revision.Id} was recorded after manifest {manifest.Id}'s transaction cutoff.");
            }

            var isAvailable = manifest.SelectionBasis switch
            {
                "ObservedAt" => revision.FirstObservedAtUtc <= manifest.SelectedAvailableCutoffAtUtc,
                "SourceAvailableAt" =>
                    revision.AvailabilityStatus is "Known" or "Estimated" &&
                    revision.AvailableAtUtc is { } availableAtUtc &&
                    availableAtUtc <= manifest.SelectedAvailableCutoffAtUtc,
                _ => false,
            };
            var isPermittedUnverifiedUnknown =
                manifest.SelectionBasis == "SourceAvailableAt" &&
                manifest.PointInTimeStatus == "Unverified" &&
                revision.AvailabilityStatus == "Unknown" &&
                revision.AvailableAtUtc is null;

            if (!isAvailable && !isPermittedUnverifiedUnknown)
            {
                throw new InvalidDataException(
                    $"Price revision {revision.Id} was not available under manifest {manifest.Id}'s selection basis and cutoff.");
            }
        }
    }

    private static void ApplyChange(
        IDictionary<DateOnly, Guid> members,
        PriceRevisionSetChangeRow change)
    {
        switch (change.Operation)
        {
            case "Add" when change.DailyPriceRevisionId is { } addedId:
                if (!members.TryAdd(change.BarDate, addedId))
                {
                    throw new InvalidDataException($"Add targets an existing bar {change.BarDate}.");
                }

                break;
            case "Replace" when change.DailyPriceRevisionId is { } replacementId &&
                                change.ReplacedDailyPriceRevisionId is { } replacedId:
                if (!members.TryGetValue(change.BarDate, out var currentReplacementId) ||
                    currentReplacementId != replacedId)
                {
                    throw new InvalidDataException($"Replace does not name the current member at {change.BarDate}.");
                }

                members[change.BarDate] = replacementId;
                break;
            case "Remove" when change.ReplacedDailyPriceRevisionId is { } removedId:
                if (!members.TryGetValue(change.BarDate, out var currentRemovalId) ||
                    currentRemovalId != removedId)
                {
                    throw new InvalidDataException($"Remove does not name the current member at {change.BarDate}.");
                }

                members.Remove(change.BarDate);
                break;
            default:
                throw new InvalidDataException($"Invalid price revision set operation '{change.Operation}'.");
        }
    }

    private sealed record VerifiedPriceRevision(
        DateOnly BarDate,
        DailyPriceRevisionRow Revision);
}
