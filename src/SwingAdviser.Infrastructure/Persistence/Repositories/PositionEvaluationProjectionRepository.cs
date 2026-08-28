using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Domain.Common;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.Persistence.Repositories;

internal enum PositionProjectionStatus
{
    Ready,
    IncompletePositionData,
    PointInTimeUnverified,
    ReconciliationRequired,
}

internal sealed record PositionEvaluationPriceProjection(
    Guid RevisionId,
    DateOnly BarDate,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    string Currency,
    string PriceUnitBasisSha256,
    string ContentSha256);

internal sealed record PositionEvaluationLotProjection(
    Guid MarginLotId,
    Guid OpeningTradeExecutionRevisionId,
    decimal? CurrentQuantity,
    decimal? EntryBasisPrice,
    decimal? FixedAtr,
    decimal? RiskAmountR,
    decimal? StopMultiplier,
    decimal? PartialTakeProfitRMultiple,
    decimal? PartialTakeProfitFraction,
    DateOnly? AtrReferenceBarDate,
    long? AtrPeriod,
    string? AtrAlgorithmId,
    decimal? StopPrice,
    decimal? TakeProfitPrice,
    string? PriceCurrency,
    string? PriceUnitBasisSha256,
    Guid? ContractRevisionId,
    Guid? RiskBasisSnapshotId,
    Guid? RiskPlanRevisionId,
    IReadOnlyList<Guid> MarginCostObservationIds);

internal sealed record PositionEvaluationManifestDraft(
    Guid AnalysisRunId,
    Guid PositionId,
    Guid AnalysisInputManifestId,
    Guid CurrentPriceRevisionId,
    string TradeExecutionRevisionIdsJson,
    string LotAllocationRevisionIdsJson,
    string PositionAdjustmentIdsJson,
    string ContractRevisionIdsJson,
    string RiskBasisSnapshotIdsJson,
    string RiskPlanRevisionIdsJson,
    string MarginCostObservationIdsJson,
    string ProjectionVersion,
    DateTimeOffset RecordedCutoffAtUtc,
    string CanonicalJson,
    string ManifestSha256);

internal sealed record PositionEvaluationProjection(
    Guid PositionId,
    Guid InstrumentId,
    string PositionSide,
    PositionProjectionStatus Status,
    IReadOnlyList<string> StatusReasons,
    PositionEvaluationPriceProjection CurrentPrice,
    IReadOnlyList<PositionEvaluationLotProjection> Lots,
    PositionEvaluationManifestDraft Manifest);

internal sealed class PositionEvaluationProjectionRepository(SwingAdviserDbContext dbContext)
{
    public const string ProjectionVersion = "position-projection-v1";
    public const string ManifestSchemaVersion = "position-evaluation-input-manifest-v1";
    public const string IdListSchemaVersion = "position-evaluation-exact-id-list-v1";

    public async Task<PositionEvaluationProjection> BuildAsync(
        Guid analysisRunId,
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        if (analysisRunId == Guid.Empty || positionId == Guid.Empty)
        {
            throw new ArgumentException("Analysis run and position IDs cannot be empty.");
        }

        var run = await dbContext.Set<AnalysisRunRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == analysisRunId, cancellationToken);
        var position = await dbContext.Set<PositionRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == positionId, cancellationToken);
        if (position.CreatedAtUtc > run.RecordedCutoffAtUtc)
        {
            throw new InvalidDataException("The position was created after the analysis transaction cutoff.");
        }

        var marketManifest = await dbContext.Set<AnalysisInputManifestRow>()
            .AsNoTracking()
            .SingleAsync(
                row => row.AnalysisRunId == analysisRunId && row.InstrumentId == position.InstrumentId,
                cancellationToken);
        if (marketManifest.SelectedRecordedCutoffAtUtc != run.RecordedCutoffAtUtc)
        {
            throw new InvalidDataException("The market manifest and analysis run use different transaction cutoffs.");
        }

        var priceSet = await new PriceRevisionSetRepository(dbContext)
            .VerifyManifestAsync(marketManifest.Id, cancellationToken);
        if (!priceSet.RevisionIdsByBarDate.TryGetValue(run.EvaluationBarDate, out var currentPriceRevisionId))
        {
            throw new InvalidDataException("The market manifest has no exact evaluation-bar price revision.");
        }

        var currentPriceRevision = await dbContext.Set<DailyPriceRevisionRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == currentPriceRevisionId, cancellationToken);
        var currentPrice = await dbContext.Set<DailyPriceRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == currentPriceRevision.DailyPriceId, cancellationToken);
        if (currentPrice.InstrumentId != position.InstrumentId ||
            currentPrice.BarDate != run.EvaluationBarDate ||
            currentPriceRevision.BarStatus is not ("Confirmed" or "Corrected"))
        {
            throw new InvalidDataException("The evaluation-bar price revision is not a usable member of the position instrument graph.");
        }

        var status = PositionProjectionStatus.Ready;
        var reasons = new SortedSet<string>(StringComparer.Ordinal);
        void Fail(PositionProjectionStatus failureStatus, string reason)
        {
            if (failureStatus > status)
            {
                status = failureStatus;
            }

            reasons.Add(reason);
        }

        if (run.PointInTimeStatus != "Verified" || marketManifest.PointInTimeStatus != "Verified")
        {
            Fail(PositionProjectionStatus.PointInTimeUnverified, "PointInTimeUnverified");
        }

        var stateRows = await dbContext.Set<PositionStateRevisionRow>()
            .AsNoTracking()
            .Where(row => row.PositionId == positionId &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc &&
                          row.EffectiveAtUtc <= run.AnalyzedAtUtc)
            .ToListAsync(cancellationToken);
        var state = SingleLeaf(
            stateRows,
            row => row.PositionId.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "position state").SingleOrDefault();
        if (state is null || state.Status != "Open")
        {
            Fail(PositionProjectionStatus.IncompletePositionData, "PositionNotOpenAtCutoff");
        }
        else if (state.ReconciliationStatus is "Required" or "InProgress")
        {
            Fail(PositionProjectionStatus.ReconciliationRequired, "PositionReconciliationRequired");
        }

        var lots = await dbContext.Set<MarginLotRow>()
            .AsNoTracking()
            .Where(row => row.PositionId == positionId && row.CreatedAtUtc <= run.RecordedCutoffAtUtc)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
        if (lots.Count == 0)
        {
            Fail(PositionProjectionStatus.IncompletePositionData, "NoMarginLotsAtCutoff");
        }

        var lotIds = lots.Select(row => row.Id).ToArray();
        var executions = await dbContext.Set<TradeExecutionRow>()
            .AsNoTracking()
            .Where(row => row.PositionId == positionId && row.CreatedAtUtc <= run.RecordedCutoffAtUtc)
            .ToListAsync(cancellationToken);
        var executionIds = executions.Select(row => row.Id).ToArray();
        var executionRows = await dbContext.Set<TradeExecutionRevisionRow>()
            .AsNoTracking()
            .Where(row => executionIds.Contains(row.TradeExecutionId) &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc &&
                          row.UserConfirmedAtUtc <= run.AnalyzedAtUtc &&
                          row.ExecutedAtUtc <= run.AnalyzedAtUtc)
            .ToListAsync(cancellationToken);
        var executionLeaves = SingleLeaf(
            executionRows,
            row => row.TradeExecutionId.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "trade execution").ToList();
        if (executionLeaves.Count != executions.Count)
        {
            throw new InvalidDataException("A logical trade execution has no eligible revision at the cutoff.");
        }
        var executionById = executions.ToDictionary(row => row.Id);
        var executionLeafByLogicalId = executionLeaves.ToDictionary(row => row.TradeExecutionId);
        var executionRevisionById = executionRows.ToDictionary(row => row.Id);

        foreach (var lot in lots)
        {
            if (!executionById.TryGetValue(lot.OpeningTradeExecutionId, out var opening) ||
                opening.ExecutionKind != "Open" ||
                !executionLeafByLogicalId.TryGetValue(opening.Id, out var openingLeaf))
            {
                throw new InvalidDataException($"Margin lot {lot.Id} has no opening execution in its position graph.");
            }

            if (!executionRevisionById.TryGetValue(lot.InitialOpeningTradeExecutionRevisionId, out var initialOpening) ||
                initialOpening.TradeExecutionId != opening.Id ||
                initialOpening.RevisionNo != 1 ||
                initialOpening.SupersedesId is not null ||
                initialOpening.RecordDisposition != "Effective" ||
                initialOpening.ChangeKind != "Initial")
            {
                throw new InvalidDataException(
                    $"Margin lot {lot.Id} initial opening revision crosses or corrupts its execution graph.");
            }

            if (openingLeaf.RecordDisposition != "Effective")
            {
                Fail(PositionProjectionStatus.ReconciliationRequired, "OpeningExecutionVoided");
            }
        }

        var allocationRows = await dbContext.Set<LotAllocationRevisionRow>()
            .AsNoTracking()
            .Where(row => (lotIds.Contains(row.MarginLotId) || executionIds.Contains(row.ClosingTradeExecutionId)) &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc &&
                          row.UserConfirmedAtUtc <= run.AnalyzedAtUtc)
            .ToListAsync(cancellationToken);
        var allocationLeaves = SingleLeaf(
            allocationRows,
            row => row.AllocationKey.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "lot allocation").ToList();
        foreach (var allocation in allocationLeaves)
        {
            if (!lotIds.Contains(allocation.MarginLotId) ||
                !executionById.TryGetValue(allocation.ClosingTradeExecutionId, out var closing) ||
                closing.ExecutionKind != "Close" ||
                !executionLeafByLogicalId.TryGetValue(closing.Id, out var closingLeaf) ||
                closingLeaf.RecordDisposition != "Effective" ||
                allocation.ClosingTradeExecutionRevisionId != closingLeaf.Id)
            {
                throw new InvalidDataException($"Lot allocation {allocation.Id} crosses the position/lot execution graph.");
            }
        }


        foreach (var closingLeaf in executionLeaves.Where(row =>
                     executionById[row.TradeExecutionId].ExecutionKind == "Close" &&
                     row.RecordDisposition == "Effective"))
        {
            var allocatedQuantity = allocationLeaves
                .Where(row => row.ClosingTradeExecutionId == closingLeaf.TradeExecutionId &&
                              row.RecordDisposition == "Effective")
                .Sum(row => row.Quantity);
            if (allocatedQuantity != closingLeaf.Quantity)
            {
                throw new InvalidDataException(
                    $"Close execution {closingLeaf.TradeExecutionId} is not fully and explicitly allocated to lots.");
            }
        }

        var adjustmentRows = await dbContext.Set<PositionAdjustmentRow>()
            .AsNoTracking()
            .Where(row => (row.PositionId == positionId || lotIds.Contains(row.MarginLotId)) &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc &&
                          (row.ConfirmedAtUtc == null || row.ConfirmedAtUtc <= run.AnalyzedAtUtc) &&
                          row.EffectiveDate <= run.EvaluationBarDate)
            .ToListAsync(cancellationToken);
        var adjustmentLeaves = SingleLeaf(
            adjustmentRows,
            row => row.AdjustmentKey.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "position adjustment").ToList();
        if (adjustmentLeaves.Any(row => row.PositionId != positionId || !lotIds.Contains(row.MarginLotId)))
        {
            throw new InvalidDataException("A position adjustment crosses the position/lot graph.");
        }

        var actionApplications = await dbContext.Set<AnalysisActionApplicationRow>()
            .AsNoTracking()
            .Where(row => row.AnalysisInputManifestId == marketManifest.Id)
            .OrderBy(row => row.Ordinal)
            .ToListAsync(cancellationToken);
        var applicationByActionRevision = actionApplications.ToDictionary(row => row.CorporateActionRevisionId);
        long AdjustmentOrdinal(PositionAdjustmentRow row) =>
            applicationByActionRevision.TryGetValue(row.CorporateActionRevisionId, out var application)
                ? application.Ordinal
                : long.MinValue;
        var actionRevisionIds = actionApplications.Select(row => row.CorporateActionRevisionId).ToArray();
        var actionRevisions = await dbContext.Set<CorporateActionRevisionRow>()
            .AsNoTracking()
            .Where(row => actionRevisionIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var actionParentIds = actionRevisions.Values.Select(row => row.CorporateActionId).Distinct().ToArray();
        var actionParents = await dbContext.Set<CorporateActionRow>()
            .AsNoTracking()
            .Where(row => actionParentIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        if (actionRevisions.Count != actionRevisionIds.Distinct().Count())
        {
            throw new InvalidDataException("The analysis manifest references a missing corporate-action revision.");
        }

        var actionGraphRows = await dbContext.Set<CorporateActionRevisionRow>()
            .AsNoTracking()
            .Where(row => actionParentIds.Contains(row.CorporateActionId) &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc)
            .ToListAsync(cancellationToken);
        var actionGraphLeaves = SingleLeaf(
            actionGraphRows,
            row => row.CorporateActionId.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "corporate action").ToDictionary(row => row.CorporateActionId);
        var availableActionRows = actionGraphRows.Where(row => marketManifest.SelectionBasis switch
        {
            "ObservedAt" => row.FirstObservedAtUtc <= marketManifest.SelectedAvailableCutoffAtUtc,
            "SourceAvailableAt" =>
                row.AvailabilityStatus is "Known" or "Estimated" &&
                row.AvailableAtUtc is { } availableAt &&
                availableAt <= marketManifest.SelectedAvailableCutoffAtUtc,
            _ => false,
        });
        var availableActionLeaves = SingleLeaf(
            availableActionRows,
            row => row.CorporateActionId.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "available corporate action").ToDictionary(row => row.CorporateActionId);

        foreach (var application in actionApplications)
        {
            var selected = actionRevisions[application.CorporateActionRevisionId];
            var expected = application.ApplicationStatus == "ExcludedUnavailable"
                ? actionGraphLeaves.GetValueOrDefault(selected.CorporateActionId)
                : availableActionLeaves.GetValueOrDefault(selected.CorporateActionId);
            if (expected is null || expected.Id != selected.Id)
            {
                throw new InvalidDataException(
                    $"Analysis action application {application.Id} does not reference the exact as-of revision leaf.");
            }
        }

        bool LotWasHeldImmediatelyBefore(MarginLotRow lot, DateOnly effectiveDate)
        {
            var openingLeaf = executionLeafByLogicalId[lot.OpeningTradeExecutionId];
            var actionStart = new DateTimeOffset(
                effectiveDate.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromHours(9));
            if (openingLeaf.ExecutedAtUtc >= actionStart || openingLeaf.RecordDisposition != "Effective")
            {
                return false;
            }

            decimal quantity = openingLeaf.Quantity;
            var priorEvents = new List<LotProjectionEvent>();
            priorEvents.AddRange(adjustmentLeaves
                .Where(row => row.MarginLotId == lot.Id &&
                              row.Status is "Applied" or "Resolved" &&
                              row.EffectiveDate < effectiveDate)
                .Select(row => new LotProjectionEvent(
                    new DateTimeOffset(row.EffectiveDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(9)),
                    0,
                    AdjustmentOrdinal(row),
                    row.Id,
                    row,
                    null)));
            priorEvents.AddRange(allocationLeaves
                .Where(row => row.MarginLotId == lot.Id &&
                              row.RecordDisposition == "Effective" &&
                              executionLeafByLogicalId[row.ClosingTradeExecutionId].ExecutedAtUtc < actionStart)
                .Select(row => new LotProjectionEvent(
                    executionLeafByLogicalId[row.ClosingTradeExecutionId].ExecutedAtUtc,
                    1,
                    0,
                    row.Id,
                    null,
                    row)));
            foreach (var priorEvent in priorEvents
                         .OrderBy(item => item.EffectiveAtUtc)
                         .ThenBy(item => item.KindOrder)
                         .ThenBy(item => item.SequenceOrder)
                         .ThenBy(item => item.Id))
            {
                if (priorEvent.Adjustment is { } priorAdjustment)
                {
                    if (priorAdjustment.BeforeQuantity != quantity || priorAdjustment.AfterQuantity is null)
                    {
                        return true;
                    }

                    quantity = priorAdjustment.AfterQuantity.Value;
                }
                else
                {
                    quantity -= priorEvent.Allocation!.Quantity;
                    if (quantity < 0)
                    {
                        return true;
                    }
                }
            }

            return quantity > 0;
        }

        foreach (var application in actionApplications)
        {
            var action = actionRevisions[application.CorporateActionRevisionId];
            if (!actionParents.TryGetValue(action.CorporateActionId, out var parent) ||
                parent.InstrumentId != position.InstrumentId ||
                action.RecordedAtUtc > run.RecordedCutoffAtUtc)
            {
                throw new InvalidDataException("A corporate action does not belong to the position instrument/cutoff graph.");
            }

            var availableAtCutoff = marketManifest.SelectionBasis switch
            {
                "ObservedAt" => action.FirstObservedAtUtc <= marketManifest.SelectedAvailableCutoffAtUtc,
                "SourceAvailableAt" =>
                    action.AvailabilityStatus is "Known" or "Estimated" &&
                    action.AvailableAtUtc is { } availableAt &&
                    availableAt <= marketManifest.SelectedAvailableCutoffAtUtc,
                _ => false,
            };
            var applicationSemanticMismatch = application.ApplicationStatus switch
            {
                "Applied" =>
                    !availableAtCutoff ||
                    action.EffectiveDate > run.EvaluationBarDate ||
                    action.Status == "Cancelled" ||
                    action.ActionType == "Unsupported",
                "ExcludedNotEffective" =>
                    action.EffectiveDate <= run.EvaluationBarDate && action.Status != "Cancelled",
                "ExcludedUnavailable" => availableAtCutoff,
                _ => false,
            };
            if (applicationSemanticMismatch)
            {
                throw new InvalidDataException(
                    $"Analysis action application {application.Id} status contradicts its exact revision/cutoffs.");
            }

            if (application.ApplicationStatus != "ExcludedUnavailable" &&
                !availableAtCutoff && marketManifest.PointInTimeStatus == "Verified")
            {
                throw new InvalidDataException("A corporate action was unavailable at the market manifest cutoff.");
            }
            if (application.ApplicationStatus != "ExcludedUnavailable" && action.PointInTimeStatus != "Verified")
            {
                Fail(PositionProjectionStatus.PointInTimeUnverified, "CorporateActionPointInTimeUnverified");
            }

            if (application.ApplicationStatus == "Applied" && action.Status == "Cancelled")
            {
                throw new InvalidDataException("A cancelled corporate action cannot be applied to a position projection.");
            }

            var affectsHolding = action.EffectiveDate <= run.EvaluationBarDate &&
                                 lots.Any(lot => LotWasHeldImmediatelyBefore(lot, action.EffectiveDate));
            if (affectsHolding && application.ApplicationStatus is "Unsupported" or "ReconciliationRequired")
            {
                Fail(PositionProjectionStatus.ReconciliationRequired, "UnsupportedCorporateAction");
            }

            if (application.ApplicationStatus == "Applied" &&
                action.ActionType is "Split" or "Consolidation")
            {
                foreach (var affectedLot in lots.Where(lot =>
                             action.EffectiveDate <= run.EvaluationBarDate &&
                             LotWasHeldImmediatelyBefore(lot, action.EffectiveDate)))
                {
                    if (!adjustmentLeaves.Any(row =>
                            row.MarginLotId == affectedLot.Id &&
                            row.CorporateActionRevisionId == action.Id &&
                            row.Status is "Applied" or "Resolved"))
                    {
                        Fail(PositionProjectionStatus.ReconciliationRequired, "MissingCorporateActionAdjustment");
                    }
                }
            }
        }

        foreach (var adjustment in adjustmentLeaves)
        {
            if (!applicationByActionRevision.TryGetValue(adjustment.CorporateActionRevisionId, out var application) &&
                adjustment.Status != "Reversed")
            {
                throw new InvalidDataException($"Position adjustment {adjustment.Id} is absent from the market manifest action graph.");
            }

            if (adjustment.Status == "ReconciliationRequired" ||
                application?.ApplicationStatus is "Unsupported" or "ReconciliationRequired")
            {
                Fail(PositionProjectionStatus.ReconciliationRequired, "CorporateActionAdjustmentRequired");
            }

            if (adjustment.Status is "Applied" or "Resolved" && application?.ApplicationStatus != "Applied")
            {
                throw new InvalidDataException(
                    $"Position adjustment {adjustment.Id} is active for a corporate action not applied by the market manifest.");
            }

            if (adjustment.Status is "Applied" or "Resolved")
            {
                var action = actionRevisions[adjustment.CorporateActionRevisionId];
                if (action.ActionType is not ("Split" or "Consolidation") ||
                    action.RatioNumerator is not { } numerator || numerator <= 0 ||
                    action.RatioDenominator is not { } denominator || denominator <= 0 ||
                    adjustment.EffectiveDate != action.EffectiveDate ||
                    adjustment.AfterQuantity is null || adjustment.AfterBasisPrice is null)
                {
                    throw new InvalidDataException(
                        $"Position adjustment {adjustment.Id} is not a complete split/consolidation conversion.");
                }

                var quantityFactor = (decimal)numerator / denominator;
                var priceFactor = (decimal)denominator / numerator;
                var invalidConversion =
                    adjustment.QuantityFactor != quantityFactor ||
                    adjustment.PriceFactor != priceFactor ||
                    application!.VolumeFactor != quantityFactor ||
                    application.PriceFactor != priceFactor ||
                    adjustment.AfterQuantity != checked(adjustment.BeforeQuantity * quantityFactor) ||
                    adjustment.AfterBasisPrice != checked(adjustment.BeforeBasisPrice * priceFactor) ||
                    !ConvertedValueMatches(adjustment.BeforeFixedAtr, adjustment.AfterFixedAtr, priceFactor) ||
                    !ConvertedValueMatches(adjustment.BeforeStopPrice, adjustment.AfterStopPrice, priceFactor) ||
                    !ConvertedValueMatches(
                        adjustment.BeforeTakeProfitPrice,
                        adjustment.AfterTakeProfitPrice,
                        priceFactor);
                if (invalidConversion)
                {
                    throw new InvalidDataException(
                        $"Position adjustment {adjustment.Id} factors/values contradict its corporate-action revision.");
                }
            }

            if (adjustment.ReplacesAdjustmentKey is { } replacedKey &&
                !adjustmentLeaves.Any(row => row.AdjustmentKey == replacedKey && row.Status == "Reversed"))
            {
                throw new InvalidDataException(
                    $"Replacement adjustment {adjustment.Id} has no reversed predecessor key at the cutoff.");
            }
        }
        var verifiedActionGraphJson = BuildActionGraphCanonicalJson(actionApplications, actionRevisions);
        var verifiedActionGraphSha256 = Hash(verifiedActionGraphJson);
        var unitActionApplications = actionApplications
            .Where(application =>
                application.ApplicationStatus == "Applied" &&
                actionRevisions[application.CorporateActionRevisionId].ActionType is "Split" or "Consolidation")
            .ToList();
        var verifiedUnitActionGraphSha256 = Hash(
            BuildActionGraphCanonicalJson(unitActionApplications, actionRevisions));
        var currentPriceUnitBasisSha256 = CalculateRiskPriceUnitBasisHash(
            position.InstrumentId,
            currentPriceRevision.Currency,
            verifiedUnitActionGraphSha256);

        var contractRows = await dbContext.Set<MarginLotContractRevisionRow>()
            .AsNoTracking()
            .Where(row => lotIds.Contains(row.MarginLotId) &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc &&
                          row.ConfirmedAtUtc <= run.AnalyzedAtUtc)
            .ToListAsync(cancellationToken);
        _ = SingleLeaf(
            contractRows,
            row => row.MarginLotId.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "margin lot contract");
        var contractLeaves = contractRows
            .Where(row => row.EffectiveFromDate <= run.EvaluationBarDate &&
                          (row.EffectiveToDate == null || row.EffectiveToDate >= run.EvaluationBarDate))
            .GroupBy(row => row.MarginLotId)
            .Select(group => group.OrderByDescending(row => row.RevisionNo).First())
            .ToList();
        var contractByLot = contractLeaves.ToDictionary(row => row.MarginLotId);

        var basisRows = await dbContext.Set<RiskBasisSnapshotRow>()
            .AsNoTracking()
            .Where(row => lotIds.Contains(row.MarginLotId) && row.CreatedAtUtc <= run.RecordedCutoffAtUtc)
            .ToListAsync(cancellationToken);
        var basisLeaves = SingleLeaf(
            basisRows,
            row => row.MarginLotId.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "risk basis").ToList();
        var basisByLot = basisLeaves.ToDictionary(row => row.MarginLotId);
        var selectedBasisIds = basisLeaves.Select(row => row.Id).ToArray();
        var basisManifestIds = basisLeaves
            .Where(row => row.AnalysisInputManifestId.HasValue)
            .Select(row => row.AnalysisInputManifestId!.Value)
            .Distinct()
            .ToArray();
        var basisManifests = await dbContext.Set<AnalysisInputManifestRow>()
            .AsNoTracking()
            .Where(row => basisManifestIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        var planRows = await dbContext.Set<RiskPlanRevisionRow>()
            .AsNoTracking()
            .Where(row => selectedBasisIds.Contains(row.RiskBasisSnapshotId) &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc &&
                          row.EffectiveAtUtc <= run.AnalyzedAtUtc)
            .ToListAsync(cancellationToken);
        var planLeaves = SingleLeaf(
            planRows,
            row => row.RiskBasisSnapshotId.ToString("D"),
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "risk plan").ToList();
        var planByBasis = planLeaves.ToDictionary(row => row.RiskBasisSnapshotId);
        var planRowById = planRows.ToDictionary(row => row.Id);
        var planLeafIds = planLeaves.Select(row => row.Id).ToHashSet();
        var basisRowById = basisLeaves.ToDictionary(row => row.Id);
        var allocationLeafById = allocationLeaves.ToDictionary(row => row.Id);
        var allocationRowById = allocationRows.ToDictionary(row => row.Id);
        var adjustmentRowById = adjustmentRows.ToDictionary(row => row.Id);
        foreach (var planRow in planRows)
        {
            var planLotId = basisRowById[planRow.RiskBasisSnapshotId].MarginLotId;
            if (planRow.PlanReason == "PartialExitBreakeven")
            {
                if (planRow.TriggerTradeExecutionId is not { } triggerExecutionId ||
                    planRow.TriggerLotAllocationRevisionId is not { } triggerAllocationId ||
                    !executionById.TryGetValue(triggerExecutionId, out var triggerLogicalExecution) ||
                    triggerLogicalExecution.ExecutionKind != "Close" ||
                    !allocationRowById.TryGetValue(triggerAllocationId, out var triggerAllocation) ||
                    triggerAllocation.RecordDisposition != "Effective" ||
                    triggerAllocation.MarginLotId != planLotId ||
                    triggerAllocation.ClosingTradeExecutionId != triggerExecutionId ||
                    !executionRevisionById.TryGetValue(
                        triggerAllocation.ClosingTradeExecutionRevisionId,
                        out var triggerExecutionRevision) ||
                    triggerExecutionRevision.TradeExecutionId != triggerExecutionId ||
                    triggerExecutionRevision.RecordDisposition != "Effective" ||
                    planRow.EffectiveAtUtc != triggerExecutionRevision.ExecutedAtUtc)
                {
                    throw new InvalidDataException(
                        $"Partial-exit risk plan {planRow.Id} lacks exact current execution/allocation evidence.");
                }

                if (planLeafIds.Contains(planRow.Id) &&
                    (!allocationLeafById.ContainsKey(triggerAllocationId) ||
                     !executionLeafByLogicalId.TryGetValue(triggerExecutionId, out var currentTriggerExecution) ||
                     currentTriggerExecution.Id != triggerExecutionRevision.Id))
                {
                    throw new InvalidDataException(
                        $"Active partial-exit risk plan {planRow.Id} does not reference current execution/allocation leaves.");
                }
            }
            else if (planRow.PlanReason == "CorporateActionConversion")
            {
                if (planRow.TriggerPositionAdjustmentId is not { } triggerAdjustmentId ||
                    !adjustmentRowById.TryGetValue(triggerAdjustmentId, out var triggerAdjustment) ||
                    triggerAdjustment.MarginLotId != planLotId ||
                    triggerAdjustment.Status is not ("Applied" or "Resolved") ||
                    planRow.SupersedesId is not { } predecessorId ||
                    !planRowById.TryGetValue(predecessorId, out var predecessor) ||
                    triggerAdjustment.BeforeStopPrice != predecessor.StopPrice ||
                    triggerAdjustment.BeforeTakeProfitPrice != predecessor.TakeProfitPrice ||
                    triggerAdjustment.AfterStopPrice != planRow.StopPrice ||
                    triggerAdjustment.AfterTakeProfitPrice != planRow.TakeProfitPrice)
                {
                    throw new InvalidDataException(
                        $"Corporate-action risk plan {planRow.Id} does not match its exact adjustment evidence.");
                }
            }
        }

        foreach (var adjustment in adjustmentLeaves.Where(row => row.Status is "Applied" or "Resolved"))
        {
            if (basisByLot.ContainsKey(adjustment.MarginLotId) &&
                !planRows.Any(row =>
                    row.PlanReason == "CorporateActionConversion" &&
                    row.TriggerPositionAdjustmentId == adjustment.Id))
            {
                throw new InvalidDataException(
                    $"Corporate-action adjustment {adjustment.Id} has no exact risk-plan conversion revision.");
            }
        }

        var costItems = await dbContext.Set<MarginCostItemRow>()
            .AsNoTracking()
            .Where(row => lotIds.Contains(row.MarginLotId) && row.CreatedAtUtc <= run.RecordedCutoffAtUtc)
            .ToListAsync(cancellationToken);
        var costItemIds = costItems.Select(row => row.Id).ToArray();
        var costObservationRows = await dbContext.Set<MarginCostObservationRow>()
            .AsNoTracking()
            .Where(row => costItemIds.Contains(row.MarginCostItemId) &&
                          row.RecordedAtUtc <= run.RecordedCutoffAtUtc &&
                          row.ObservedAtUtc <= run.AnalyzedAtUtc &&
                          (row.AvailableAtUtc == null || row.AvailableAtUtc <= run.AnalyzedAtUtc))
            .ToListAsync(cancellationToken);
        var costLeaves = SingleLeaf(
            costObservationRows,
            row => $"{row.MarginCostItemId:D}:{row.ValuationKind}",
            row => row.Id,
            row => row.SupersedesId,
            row => row.RevisionNo,
            "margin cost observation").ToList();
        var costItemById = costItems.ToDictionary(row => row.Id);
        var eligibleCostObservationById = costObservationRows.ToDictionary(row => row.Id);
        foreach (var confirmed in costLeaves.Where(row => row.ValuationKind == "Confirmed"))
        {
            if (confirmed.ReconcilesEstimateId is { } estimateId &&
                (!eligibleCostObservationById.TryGetValue(estimateId, out var estimate) ||
                 estimate.MarginCostItemId != confirmed.MarginCostItemId ||
                 estimate.ValuationKind != "Estimate"))
            {
                throw new InvalidDataException(
                    $"Confirmed margin cost observation {confirmed.Id} does not reconcile the exact Estimate leaf.");
            }
        }

        var lotProjections = new List<PositionEvaluationLotProjection>(lots.Count);
        foreach (var lot in lots.OrderBy(row => row.Id))
        {
            var opening = executionLeafByLogicalId[lot.OpeningTradeExecutionId];
            contractByLot.TryGetValue(lot.Id, out var contract);
            basisByLot.TryGetValue(lot.Id, out var basis);
            var plan = basis is not null && planByBasis.TryGetValue(basis.Id, out var selectedPlan)
                ? selectedPlan
                : null;

            decimal? quantity = opening.RecordDisposition == "Effective" ? opening.Quantity : null;
            decimal? basisPrice = opening.RecordDisposition == "Effective"
                ? basis?.EntryBasisPrice ?? opening.Price
                : null;
            decimal? fixedAtr = opening.RecordDisposition == "Effective" ? basis?.FixedAtr : null;
            var projectionEvents = new List<LotProjectionEvent>();
            projectionEvents.AddRange(adjustmentLeaves
                .Where(row => row.MarginLotId == lot.Id && row.Status != "Reversed")
                .Select(row => new LotProjectionEvent(
                    new DateTimeOffset(row.EffectiveDate.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(9)),
                    0,
                    AdjustmentOrdinal(row),
                    row.Id,
                    row,
                    null)));
            projectionEvents.AddRange(allocationLeaves
                .Where(row => row.MarginLotId == lot.Id && row.RecordDisposition == "Effective")
                .Select(row => new LotProjectionEvent(
                    executionLeafByLogicalId[row.ClosingTradeExecutionId].ExecutedAtUtc,
                    1,
                    0,
                    row.Id,
                    null,
                    row)));

            foreach (var projectionEvent in projectionEvents
                         .OrderBy(item => item.EffectiveAtUtc)
                         .ThenBy(item => item.KindOrder)
                         .ThenBy(item => item.SequenceOrder)
                         .ThenBy(item => item.Id))
            {
                if (projectionEvent.Allocation is { } allocation)
                {
                    if (quantity is null)
                    {
                        continue;
                    }

                    quantity -= allocation.Quantity;
                    if (quantity < 0)
                    {
                        throw new InvalidDataException($"Lot {lot.Id} is over-allocated at the requested cutoff.");
                    }

                    continue;
                }

                var adjustment = projectionEvent.Adjustment!;

                if (adjustment.Status is not ("Applied" or "Resolved") ||
                    adjustment.AfterQuantity is null || adjustment.AfterBasisPrice is null ||
                    quantity is null || basisPrice is null || fixedAtr is null)
                {
                    quantity = null;
                    basisPrice = null;
                    fixedAtr = null;
                    Fail(PositionProjectionStatus.ReconciliationRequired, "CorporateActionProjectionUnavailable");
                    break;
                }

                if (adjustment.BeforeQuantity != quantity || adjustment.BeforeBasisPrice != basisPrice)
                {
                    throw new InvalidDataException($"Position adjustment {adjustment.Id} does not continue the lot projection.");
                }

                quantity = adjustment.AfterQuantity;
                basisPrice = adjustment.AfterBasisPrice;
                if (adjustment.AfterFixedAtr is null)
                {
                    fixedAtr = null;
                    Fail(PositionProjectionStatus.ReconciliationRequired, "CorporateActionRiskUnitUnavailable");
                    break;
                }

                fixedAtr = adjustment.AfterFixedAtr;
            }

            if (quantity > 0)
            {
                if (contract is null || basis is null || plan is null)
                {
                    Fail(PositionProjectionStatus.IncompletePositionData, "MissingLotContractOrRiskGraph");
                }
                else if (contract.OpeningTradeExecutionRevisionId != opening.Id ||
                         basis.OpeningTradeExecutionRevisionId != opening.Id)
                {
                    throw new InvalidDataException($"Lot {lot.Id} contract/risk basis references a stale opening revision.");
                }
                else if (basis.PriceCurrency is null || basis.PriceUnitBasisSha256 is null ||
                         basis.AnalysisInputManifestId is not { } basisManifestId ||
                         !basisManifests.TryGetValue(basisManifestId, out var basisManifest) ||
                         basisManifest.InstrumentId != position.InstrumentId ||
                         basisManifest.SelectedRecordedCutoffAtUtc > basis.CreatedAtUtc ||
                         basis.PriceUnitBasisSha256 != CalculateRiskPriceUnitBasisHash(
                             position.InstrumentId,
                             basis.PriceCurrency,
                             basisManifest.CorporateActionSetSha256))
                {
                    Fail(PositionProjectionStatus.ReconciliationRequired, "RiskPriceUnitUnverified");
                }
                else if (!string.Equals(basis.PriceCurrency, currentPriceRevision.Currency, StringComparison.Ordinal) ||
                         !string.Equals(contract.ContractCurrency, currentPriceRevision.Currency, StringComparison.Ordinal))
                {
                    Fail(PositionProjectionStatus.ReconciliationRequired, "PriceCurrencyMismatch");
                }
            }

            var lotCostIds = costLeaves
                .Where(row => costItemById[row.MarginCostItemId].MarginLotId == lot.Id)
                .OrderBy(row => costItemById[row.MarginCostItemId].CostType, StringComparer.Ordinal)
                .ThenBy(row => costItemById[row.MarginCostItemId].OccurrenceKey, StringComparer.Ordinal)
                .ThenBy(row => row.ValuationKind, StringComparer.Ordinal)
                .Select(row => row.Id)
                .ToArray();
            lotProjections.Add(new PositionEvaluationLotProjection(
                lot.Id,
                opening.Id,
                quantity,
                basisPrice,
                fixedAtr,
                fixedAtr is { } currentFixedAtr && basis is not null
                    ? checked(currentFixedAtr * basis.StopMultiplier)
                    : null,
                basis?.StopMultiplier,
                basis?.PartialTakeProfitRMultiple,
                basis?.PartialTakeProfitFraction,
                basis?.AtrReferenceBarDate,
                basis?.AtrPeriod,
                basis?.AtrAlgorithmId,
                plan?.StopPrice,
                plan?.TakeProfitPrice,
                basis?.PriceCurrency,
                basis?.PriceCurrency is { } priceCurrency
                    ? CalculateRiskPriceUnitBasisHash(
                        position.InstrumentId,
                        priceCurrency,
                        verifiedUnitActionGraphSha256)
                    : null,
                contract?.Id,
                basis?.Id,
                plan?.Id,
                lotCostIds));
        }

        var effectiveExecutions = executionLeaves
            .Where(row => row.RecordDisposition == "Effective")
            .OrderBy(row => row.ExecutedAtUtc)
            .ThenBy(row => row.TradeExecutionId)
            .ToList();
        var orderedAllocations = allocationLeaves
            .OrderBy(row => row.MarginLotId)
            .ThenBy(row => row.ClosingTradeExecutionId)
            .ThenBy(row => row.AllocationKey)
            .ToList();
        var orderedAdjustments = adjustmentLeaves
            .OrderBy(row => row.MarginLotId)
            .ThenBy(AdjustmentOrdinal)
            .ThenBy(row => row.AdjustmentKey)
            .ToList();
        var orderedContracts = contractLeaves.OrderBy(row => row.MarginLotId).ToList();
        var orderedBases = basisLeaves.OrderBy(row => row.MarginLotId).ToList();
        var basisLotById = basisLeaves.ToDictionary(row => row.Id, row => row.MarginLotId);
        var orderedPlans = planLeaves.OrderBy(row => basisLotById[row.RiskBasisSnapshotId]).ToList();
        var orderedCosts = costLeaves
            .OrderBy(row => costItemById[row.MarginCostItemId].MarginLotId)
            .ThenBy(row => costItemById[row.MarginCostItemId].CostType, StringComparer.Ordinal)
            .ThenBy(row => costItemById[row.MarginCostItemId].OccurrenceKey, StringComparer.Ordinal)
            .ThenBy(row => row.ValuationKind, StringComparer.Ordinal)
            .ToList();

        var tradeJson = ExactIdsJson(effectiveExecutions.Select(row => row.Id));
        var allocationJson = ExactIdsJson(orderedAllocations.Select(row => row.Id));
        var adjustmentJson = ExactIdsJson(orderedAdjustments.Select(row => row.Id));
        var contractJson = ExactIdsJson(orderedContracts.Select(row => row.Id));
        var basisJson = ExactIdsJson(orderedBases.Select(row => row.Id));
        var planJson = ExactIdsJson(orderedPlans.Select(row => row.Id));
        var costJson = ExactIdsJson(orderedCosts.Select(row => row.Id));
        var canonicalJson = BuildCanonicalManifestJson(
            run,
            position,
            state,
            lots,
            marketManifest,
            verifiedActionGraphJson,
            verifiedActionGraphSha256,
            verifiedUnitActionGraphSha256,
            currentPrice,
            currentPriceRevision,
            effectiveExecutions,
            orderedAllocations,
            orderedAdjustments,
            orderedContracts,
            orderedBases,
            orderedPlans,
            orderedCosts,
            costItemById);
        var manifestHash = Hash(canonicalJson);

        var draft = new PositionEvaluationManifestDraft(
            run.Id,
            position.Id,
            marketManifest.Id,
            currentPriceRevision.Id,
            tradeJson,
            allocationJson,
            adjustmentJson,
            contractJson,
            basisJson,
            planJson,
            costJson,
            ProjectionVersion,
            run.RecordedCutoffAtUtc,
            canonicalJson,
            manifestHash);
        return new PositionEvaluationProjection(
            position.Id,
            position.InstrumentId,
            position.PositionSide,
            status,
            reasons.ToArray(),
            new PositionEvaluationPriceProjection(
                currentPriceRevision.Id,
                currentPrice.BarDate,
                currentPriceRevision.Open,
                currentPriceRevision.High,
                currentPriceRevision.Low,
                currentPriceRevision.Close,
                currentPriceRevision.Currency,
                currentPriceUnitBasisSha256,
                currentPriceRevision.ContentSha256),
            lotProjections,
            draft);
    }

    internal static string ExactIdsJson(IEnumerable<Guid> ids)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", IdListSchemaVersion);
            writer.WritePropertyName("ids");
            writer.WriteStartArray();
            foreach (var id in ids)
            {
                writer.WriteStringValue(PersistenceValueFormats.FormatGuid(id));
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string CalculateRiskPriceUnitBasisHash(
        Guid instrumentId,
        string currency,
        string corporateActionSetSha256)
    {
        var json = JsonSerializer.Serialize(new
        {
            SchemaVersion = "risk-price-unit-basis-v1",
            InstrumentId = instrumentId,
            Currency = currency,
            CorporateActionSetSha256 = corporateActionSetSha256,
        });
        return Hash(json);
    }

    private static IReadOnlyList<T> SingleLeaf<T>(
        IEnumerable<T> rows,
        Func<T, string> logicalKey,
        Func<T, Guid> id,
        Func<T, Guid?> supersedesId,
        Func<T, long> revisionNo,
        string graphName)
    {
        var leaves = new List<T>();
        foreach (var group in rows.GroupBy(logicalKey, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(revisionNo).ToList();
            var byId = ordered.ToDictionary(id);
            if (ordered.Count == 0 || revisionNo(ordered[0]) != 1 || supersedesId(ordered[0]) is not null)
            {
                throw new InvalidDataException($"The {graphName} revision graph does not start at revision 1.");
            }

            for (var index = 1; index < ordered.Count; index++)
            {
                var current = ordered[index];
                var predecessorId = supersedesId(current);
                if (revisionNo(current) != revisionNo(ordered[index - 1]) + 1 ||
                    predecessorId is null || !byId.TryGetValue(predecessorId.Value, out var predecessor) ||
                    revisionNo(predecessor) != revisionNo(current) - 1)
                {
                    throw new InvalidDataException($"The {graphName} revision graph is disconnected or crosses a logical parent.");
                }
            }

            var superseded = ordered
                .Select(supersedesId)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToHashSet();
            var groupLeaves = ordered.Where(row => !superseded.Contains(id(row))).ToList();
            if (groupLeaves.Count != 1)
            {
                throw new InvalidDataException($"The {graphName} revision graph has multiple leaves.");
            }

            leaves.Add(groupLeaves[0]);
        }

        return leaves;
    }

    private static string BuildCanonicalManifestJson(
        AnalysisRunRow run,
        PositionRow position,
        PositionStateRevisionRow? state,
        IReadOnlyList<MarginLotRow> lots,
        AnalysisInputManifestRow marketManifest,
        string verifiedActionGraphJson,
        string verifiedActionGraphSha256,
        string verifiedUnitActionGraphSha256,
        DailyPriceRow currentPrice,
        DailyPriceRevisionRow currentPriceRevision,
        IReadOnlyList<TradeExecutionRevisionRow> executions,
        IReadOnlyList<LotAllocationRevisionRow> allocations,
        IReadOnlyList<PositionAdjustmentRow> adjustments,
        IReadOnlyList<MarginLotContractRevisionRow> contracts,
        IReadOnlyList<RiskBasisSnapshotRow> bases,
        IReadOnlyList<RiskPlanRevisionRow> plans,
        IReadOnlyList<MarginCostObservationRow> costs,
        IReadOnlyDictionary<Guid, MarginCostItemRow> costItems)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", ManifestSchemaVersion);
            writer.WriteString("projectionVersion", ProjectionVersion);
            WriteGuid(writer, "analysisRunId", run.Id);
            WriteGuid(writer, "positionId", position.Id);
            WriteGuid(writer, "instrumentId", position.InstrumentId);
            writer.WriteString("positionSide", position.PositionSide);
            WriteGuid(writer, "analysisInputManifestId", marketManifest.Id);
            writer.WriteString("analysisInputManifestSha256", marketManifest.ManifestSha256);
            writer.WriteString("verifiedCorporateActionGraphSha256", verifiedActionGraphSha256);
            writer.WriteString("verifiedPriceUnitActionGraphSha256", verifiedUnitActionGraphSha256);
            writer.WritePropertyName("verifiedCorporateActionGraph");
            writer.WriteRawValue(verifiedActionGraphJson);
            writer.WriteString("recordedCutoffAtUtc", PersistenceValueFormats.FormatInstant(run.RecordedCutoffAtUtc));
            writer.WriteString("analyzedAtUtc", PersistenceValueFormats.FormatInstant(run.AnalyzedAtUtc));
            writer.WriteString("evaluationBarDate", PersistenceValueFormats.FormatMarketDate(run.EvaluationBarDate));
            writer.WritePropertyName("positionStateRevision");
            if (state is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                WriteGuid(writer, "revisionId", state.Id);
                writer.WriteString("contentSha256", state.ContentSha256);
                writer.WriteEndObject();
            }

            writer.WritePropertyName("marginLots");
            writer.WriteStartArray();
            foreach (var lot in lots.OrderBy(row => row.Id))
            {
                writer.WriteStartObject();
                WriteGuid(writer, "marginLotId", lot.Id);
                WriteGuid(writer, "openingTradeExecutionId", lot.OpeningTradeExecutionId);
                WriteGuid(writer, "initialOpeningTradeExecutionRevisionId", lot.InitialOpeningTradeExecutionRevisionId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("currentPriceRevision");
            writer.WriteStartObject();
            WriteGuid(writer, "dailyPriceId", currentPrice.Id);
            WriteGuid(writer, "revisionId", currentPriceRevision.Id);
            writer.WriteString("contentSha256", currentPriceRevision.ContentSha256);
            writer.WriteEndObject();

            WriteRevisionArray(writer, "tradeExecutions", executions, row =>
            {
                WriteGuid(writer, "logicalId", row.TradeExecutionId);
                WriteGuid(writer, "revisionId", row.Id);
                writer.WriteString("contentSha256", row.ContentSha256);
            });
            WriteRevisionArray(writer, "lotAllocations", allocations, row =>
            {
                WriteGuid(writer, "logicalId", row.AllocationKey);
                WriteGuid(writer, "marginLotId", row.MarginLotId);
                WriteGuid(writer, "revisionId", row.Id);
                writer.WriteString("contentSha256", row.ContentSha256);
                writer.WriteString("disposition", row.RecordDisposition);
            });
            WriteRevisionArray(writer, "positionAdjustments", adjustments, row =>
            {
                WriteGuid(writer, "logicalId", row.AdjustmentKey);
                WriteGuid(writer, "marginLotId", row.MarginLotId);
                WriteGuid(writer, "corporateActionRevisionId", row.CorporateActionRevisionId);
                WriteGuid(writer, "revisionId", row.Id);
                writer.WriteString("contentSha256", row.ContentSha256);
                writer.WriteString("status", row.Status);
            });
            WriteRevisionArray(writer, "contracts", contracts, row =>
            {
                WriteGuid(writer, "marginLotId", row.MarginLotId);
                WriteGuid(writer, "revisionId", row.Id);
                writer.WriteString("contentSha256", row.ContentSha256);
            });
            WriteRevisionArray(writer, "riskBases", bases, row =>
            {
                WriteGuid(writer, "marginLotId", row.MarginLotId);
                WriteGuid(writer, "revisionId", row.Id);
                writer.WriteString("contentSha256", row.ContentSha256);
            });
            WriteRevisionArray(writer, "riskPlans", plans, row =>
            {
                WriteGuid(writer, "riskBasisSnapshotId", row.RiskBasisSnapshotId);
                WriteGuid(writer, "revisionId", row.Id);
                writer.WriteString("contentSha256", row.ContentSha256);
            });
            WriteRevisionArray(writer, "marginCosts", costs, row =>
            {
                var item = costItems[row.MarginCostItemId];
                WriteGuid(writer, "marginLotId", item.MarginLotId);
                WriteGuid(writer, "logicalId", item.Id);
                writer.WriteString("valuationKind", row.ValuationKind);
                WriteGuid(writer, "revisionId", row.Id);
                writer.WriteString("contentSha256", row.ContentSha256);
            });
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string BuildActionGraphCanonicalJson(
        IReadOnlyList<AnalysisActionApplicationRow> applications,
        IReadOnlyDictionary<Guid, CorporateActionRevisionRow> revisions)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "position-evaluation-corporate-action-graph-v1");
            writer.WritePropertyName("applications");
            writer.WriteStartArray();
            foreach (var application in applications.OrderBy(row => row.Ordinal))
            {
                var revision = revisions[application.CorporateActionRevisionId];
                writer.WriteStartObject();
                writer.WriteNumber("ordinal", application.Ordinal);
                WriteGuid(writer, "corporateActionRevisionId", revision.Id);
                writer.WriteString("corporateActionContentSha256", revision.ContentSha256);
                writer.WriteString("actionType", revision.ActionType);
                writer.WriteString("actionStatus", revision.Status);
                writer.WriteString("effectiveDate", PersistenceValueFormats.FormatMarketDate(revision.EffectiveDate));
                writer.WriteString("applicationStatus", application.ApplicationStatus);
                if (application.ReferencePriceRevisionId is { } referencePriceRevisionId)
                {
                    WriteGuid(writer, "referencePriceRevisionId", referencePriceRevisionId);
                }
                else
                {
                    writer.WriteNull("referencePriceRevisionId");
                }

                WriteNullableDecimal(writer, "priceFactor", application.PriceFactor);
                WriteNullableDecimal(writer, "volumeFactor", application.VolumeFactor);
                WriteNullableDecimal(writer, "cumulativePriceFactor", application.CumulativePriceFactor);
                WriteNullableDecimal(writer, "cumulativeVolumeFactor", application.CumulativeVolumeFactor);
                writer.WriteString("reason", application.Reason);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRevisionArray<T>(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<T> rows,
        Action<T> writeRow)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var row in rows)
        {
            writer.WriteStartObject();
            writeRow(row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteGuid(Utf8JsonWriter writer, string propertyName, Guid value) =>
        writer.WriteString(propertyName, PersistenceValueFormats.FormatGuid(value));

    private static void WriteNullableDecimal(Utf8JsonWriter writer, string propertyName, decimal? value)
    {
        if (value is { } present)
        {
            writer.WriteString(propertyName, PersistenceValueFormats.FormatDecimal(present));
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }

    private static bool ConvertedValueMatches(decimal? before, decimal? after, decimal factor) =>
        before is null ? after is null : after == checked(before.Value * factor);

    private static string Hash(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    private sealed record LotProjectionEvent(
        DateTimeOffset EffectiveAtUtc,
        int KindOrder,
        long SequenceOrder,
        Guid Id,
        PositionAdjustmentRow? Adjustment,
        LotAllocationRevisionRow? Allocation);
}
