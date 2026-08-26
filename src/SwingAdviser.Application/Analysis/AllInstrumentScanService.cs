using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.MarketData;

namespace SwingAdviser.Application.Analysis;

public sealed record AllInstrumentScanInput
{
    public AllInstrumentScanInput(
        InstrumentIdentifierRevision identifierRevision,
        InstrumentMasterRevision masterRevision,
        TechnicalIndicatorCalculationRequest? indicatorRequest,
        string? preparationFailureReason = null)
    {
        IdentifierRevision = identifierRevision ?? throw new ArgumentNullException(nameof(identifierRevision));
        MasterRevision = masterRevision ?? throw new ArgumentNullException(nameof(masterRevision));
        if (IdentifierRevision.InstrumentId != MasterRevision.InstrumentId)
        {
            throw new ArgumentException(
                "The identifier revision and master revision must belong to the same instrument.",
                nameof(identifierRevision));
        }

        if (indicatorRequest is not null && !string.IsNullOrWhiteSpace(preparationFailureReason))
        {
            throw new ArgumentException(
                "A scan input cannot contain both a prepared indicator request and a preparation failure.");
        }

        IndicatorRequest = indicatorRequest;
        PreparationFailureReason = string.IsNullOrWhiteSpace(preparationFailureReason)
            ? null
            : preparationFailureReason.Trim();
    }

    public InstrumentIdentifierRevision IdentifierRevision { get; }
    public string InstrumentCode => IdentifierRevision.Value;
    public InstrumentMasterRevision MasterRevision { get; }
    public TechnicalIndicatorCalculationRequest? IndicatorRequest { get; }
    public string? PreparationFailureReason { get; }
    public InstrumentId InstrumentId => MasterRevision.InstrumentId;
}

public sealed record AllInstrumentScanRequest
{
    public AllInstrumentScanRequest(
        AnalysisRun run,
        StrategyParameterSnapshot strategySnapshot,
        CandidateStrategyParameters strategyParameters,
        IReadOnlyList<AllInstrumentScanInput> instruments)
    {
        Run = run ?? throw new ArgumentNullException(nameof(run));
        StrategySnapshot = strategySnapshot ?? throw new ArgumentNullException(nameof(strategySnapshot));
        StrategyParameters = strategyParameters ?? throw new ArgumentNullException(nameof(strategyParameters));
        Instruments = Array.AsReadOnly(
            (instruments ?? throw new ArgumentNullException(nameof(instruments))).ToArray());
    }

    public AnalysisRun Run { get; }
    public StrategyParameterSnapshot StrategySnapshot { get; }
    public CandidateStrategyParameters StrategyParameters { get; }
    public IReadOnlyList<AllInstrumentScanInput> Instruments { get; }
}

public sealed record AllInstrumentScanProgress(
    int ProcessedInstrumentCount,
    int TotalInstrumentCount,
    int CandidateCount,
    int FailedInstrumentCount,
    string CurrentInstrumentCode);

public enum AllInstrumentScanItemStatus
{
    Completed,
    Skipped,
    Failed,
}

public sealed record AllInstrumentScanDirectionResult
{
    public AllInstrumentScanDirectionResult(
        PositionSide side,
        TechnicalAnalysisResult technicalResult,
        CandidateResult? candidate,
        DateTimeOffset createdAtUtc)
    {
        TechnicalResult = technicalResult ?? throw new ArgumentNullException(nameof(technicalResult));
        if (technicalResult.PositionSide != side)
        {
            throw new ArgumentException("The direction and technical result side must match.", nameof(side));
        }

        if ((technicalResult.Outcome == TechnicalAnalysisOutcome.Candidate) != (candidate is not null))
        {
            throw new ArgumentException("Only a Candidate technical result may carry a candidate entity.");
        }

        if (candidate is not null && candidate.TechnicalAnalysisResultId != technicalResult.Id)
        {
            throw new ArgumentException(
                "The candidate must belong to the supplied technical result.",
                nameof(candidate));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The result timestamp must be UTC.", nameof(createdAtUtc));
        }

        Side = side;
        Candidate = candidate;
        CreatedAtUtc = createdAtUtc;
    }

    public PositionSide Side { get; }
    public TechnicalAnalysisResult TechnicalResult { get; }
    public CandidateResult? Candidate { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}

public sealed record AllInstrumentScanItemResult
{
    private AllInstrumentScanItemResult(
        InstrumentId instrumentId,
        string instrumentCode,
        AllInstrumentScanItemStatus status,
        string? statusReason,
        IReadOnlyList<AllInstrumentScanDirectionResult> directions)
    {
        InstrumentId = instrumentId;
        InstrumentCode = instrumentCode;
        Status = status;
        StatusReason = statusReason;
        Directions = Array.AsReadOnly(directions.OrderBy(result => result.Side).ToArray());
    }

    public InstrumentId InstrumentId { get; }
    public string InstrumentCode { get; }
    public AllInstrumentScanItemStatus Status { get; }
    public string? StatusReason { get; }
    public IReadOnlyList<AllInstrumentScanDirectionResult> Directions { get; }

    internal static AllInstrumentScanItemResult Completed(
        AllInstrumentScanInput input,
        IReadOnlyList<AllInstrumentScanDirectionResult> directions)
    {
        if (directions.Count != 2 || directions.Select(item => item.Side).Distinct().Count() != 2)
        {
            throw new ArgumentException("A completed instrument requires one Long and one Short result.", nameof(directions));
        }

        var failed = directions.Any(item => item.TechnicalResult.Outcome == TechnicalAnalysisOutcome.Failed);
        return new AllInstrumentScanItemResult(
            input.InstrumentId,
            input.InstrumentCode,
            failed ? AllInstrumentScanItemStatus.Failed : AllInstrumentScanItemStatus.Completed,
            failed ? "One or more candidate directions failed unexpectedly." : null,
            directions);
    }

    internal static AllInstrumentScanItemResult Skipped(
        AllInstrumentScanInput input,
        string reason) =>
        new(
            input.InstrumentId,
            input.InstrumentCode,
            AllInstrumentScanItemStatus.Skipped,
            reason,
            []);

    internal static AllInstrumentScanItemResult Failed(
        AllInstrumentScanInput input,
        string reason) =>
        new(
            input.InstrumentId,
            input.InstrumentCode,
            AllInstrumentScanItemStatus.Failed,
            reason,
            []);
}

public sealed record RankedScanCandidate(
    string InstrumentCode,
    PositionSide Side,
    CandidateResult Candidate);

public sealed record AllInstrumentScanSummary
{
    internal AllInstrumentScanSummary(
        AnalysisRunId analysisRunId,
        IReadOnlyList<AllInstrumentScanItemResult> items)
    {
        AnalysisRunId = analysisRunId;
        Items = Array.AsReadOnly(items.ToArray());
        TotalInputCount = items.Count;
        SkippedInstrumentCount = items.Count(item => item.Status == AllInstrumentScanItemStatus.Skipped);
        FailedInstrumentCount = items.Count(item => item.Status == AllInstrumentScanItemStatus.Failed);
        SucceededInstrumentCount = items.Count(item => item.Status == AllInstrumentScanItemStatus.Completed);
        EligibleInstrumentCount = SucceededInstrumentCount + FailedInstrumentCount;
        CandidateCount = items.Sum(item => item.Directions.Count(direction => direction.Candidate is not null));
        UnusableInstrumentCount = items.Count(item =>
            item.Status == AllInstrumentScanItemStatus.Completed &&
            item.Directions.Any(direction => direction.TechnicalResult.Outcome is not (
                TechnicalAnalysisOutcome.Candidate or TechnicalAnalysisOutcome.NotCandidate)));
        SuggestedRunStatus = FailedInstrumentCount == 0
            ? AnalysisRunStatus.Succeeded
            : SucceededInstrumentCount > 0
                ? AnalysisRunStatus.PartiallySucceeded
                : AnalysisRunStatus.Failed;
    }

    public AnalysisRunId AnalysisRunId { get; }
    public IReadOnlyList<AllInstrumentScanItemResult> Items { get; }
    public int TotalInputCount { get; }
    public int EligibleInstrumentCount { get; }
    public int SucceededInstrumentCount { get; }
    public int FailedInstrumentCount { get; }
    public int SkippedInstrumentCount { get; }
    public int UnusableInstrumentCount { get; }
    public int CandidateCount { get; }
    public AnalysisRunStatus SuggestedRunStatus { get; }

    public IReadOnlyList<RankedScanCandidate> GetRankedCandidates(PositionSide side)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side));
        }

        return Items
            .SelectMany(item => item.Directions
                .Where(direction => direction.Side == side && direction.Candidate is not null)
                .Select(direction => new RankedScanCandidate(
                    item.InstrumentCode,
                    side,
                    direction.Candidate!)))
            .OrderByDescending(item => item.Candidate.Score)
            .ThenBy(item => item.InstrumentCode, StringComparer.Ordinal)
            .ToArray();
    }
}

public interface IAllInstrumentScanService
{
    Task<AllInstrumentScanSummary> ScanAsync(
        AllInstrumentScanRequest request,
        IProgress<AllInstrumentScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class AllInstrumentScanService : IAllInstrumentScanService
{
    private readonly ITechnicalIndicatorEngine indicatorEngine;
    private readonly ICandidateScoringEngine candidateEngine;
    private readonly TimeProvider timeProvider;

    public AllInstrumentScanService(
        ITechnicalIndicatorEngine indicatorEngine,
        ICandidateScoringEngine candidateEngine,
        TimeProvider? timeProvider = null)
    {
        this.indicatorEngine = indicatorEngine ?? throw new ArgumentNullException(nameof(indicatorEngine));
        this.candidateEngine = candidateEngine ?? throw new ArgumentNullException(nameof(candidateEngine));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<AllInstrumentScanSummary> ScanAsync(
        AllInstrumentScanRequest request,
        IProgress<AllInstrumentScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRunContract(request);
        var orderedInputs = ValidateAndOrderInputs(request.Instruments);
        return Task.Run(
            () => ScanCore(request, orderedInputs, progress, cancellationToken),
            cancellationToken);
    }

    private AllInstrumentScanSummary ScanCore(
        AllInstrumentScanRequest request,
        IReadOnlyList<AllInstrumentScanInput> inputs,
        IProgress<AllInstrumentScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new List<AllInstrumentScanItemResult>(inputs.Count);
        var candidateCount = 0;
        var failureCount = 0;
        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AllInstrumentScanItemResult result;
            try
            {
                result = ScanOne(request, input);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                result = AllInstrumentScanItemResult.Failed(
                    input,
                    $"SCAN_ITEM_FAILED: {exception.GetType().Name}: {exception.Message}");
            }
            results.Add(result);
            candidateCount += result.Directions.Count(direction => direction.Candidate is not null);
            if (result.Status == AllInstrumentScanItemStatus.Failed)
            {
                failureCount++;
            }

            if (progress is not null)
            {
                try
                {
                    progress.Report(new AllInstrumentScanProgress(
                        results.Count,
                        inputs.Count,
                        candidateCount,
                        failureCount,
                        input.InstrumentCode));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Progress observers are outside the scan contract and cannot invalidate results.
                }
            }
        }

        return new AllInstrumentScanSummary(request.Run.Id, results);
    }

    private AllInstrumentScanItemResult ScanOne(
        AllInstrumentScanRequest request,
        AllInstrumentScanInput input)
    {
        var run = request.Run;
        var master = input.MasterRevision;
        var identifier = input.IdentifierRevision;
        if (identifier.Disposition != RecordDisposition.Effective ||
            (identifier.ValidFrom is not null && run.EvaluationBarDate < identifier.ValidFrom) ||
            (identifier.ValidTo is not null && run.EvaluationBarDate > identifier.ValidTo))
        {
            return AllInstrumentScanItemResult.Skipped(
                input,
                "IDENTIFIER_NOT_EFFECTIVE: The selected instrument code revision is not effective on the evaluation date.");
        }

        if (identifier.Audit.Revision.RecordedAtUtc > run.RecordedCutoffAtUtc ||
            !identifier.Audit.Availability.IsAvailableBy(run.AnalyzedAtUtc) ||
            identifier.Audit.FirstObservedAtUtc > run.AnalyzedAtUtc)
        {
            return AllInstrumentScanItemResult.Skipped(
                input,
                "IDENTIFIER_NOT_POINT_IN_TIME_AVAILABLE: The instrument code revision was not available at analysis time.");
        }

        if (!master.EffectivePeriod.Contains(run.EvaluationBarDate))
        {
            return AllInstrumentScanItemResult.Skipped(
                input,
                "MASTER_NOT_EFFECTIVE: The selected master revision is not effective on the evaluation date.");
        }

        if (master.Audit.Revision.RecordedAtUtc > run.RecordedCutoffAtUtc)
        {
            return AllInstrumentScanItemResult.Skipped(
                input,
                "MASTER_AFTER_RECORDED_CUTOFF: The master revision was recorded after the run cutoff.");
        }

        if (!master.Audit.Availability.IsAvailableBy(run.AnalyzedAtUtc) ||
            master.Audit.FirstObservedAtUtc > run.AnalyzedAtUtc)
        {
            return AllInstrumentScanItemResult.Skipped(
                input,
                "MASTER_NOT_POINT_IN_TIME_AVAILABLE: The master revision was not available at analysis time.");
        }

        if (!request.StrategyParameters.Universe.Includes(master, out var universeReason))
        {
            return AllInstrumentScanItemResult.Skipped(input, universeReason!);
        }

        if (input.IndicatorRequest is null)
        {
            return AllInstrumentScanItemResult.Failed(
                input,
                input.PreparationFailureReason ??
                "INPUT_PREPARATION_FAILED: No verified point-in-time indicator request was prepared.");
        }

        var indicatorRequest = input.IndicatorRequest;
        if (indicatorRequest.Manifest.AnalysisRunId != run.Id ||
            indicatorRequest.Manifest.InstrumentId != input.InstrumentId ||
            indicatorRequest.EvaluationBarDate != run.EvaluationBarDate)
        {
            return AllInstrumentScanItemResult.Failed(
                input,
                "INPUT_IDENTITY_MISMATCH: The manifest, instrument, evaluation date, and run context must remain an inseparable bundle.");
        }

        TechnicalIndicatorCalculationResult indicatorResult;
        try
        {
            indicatorResult = indicatorEngine.Calculate(
                indicatorRequest,
                request.StrategyParameters.Indicators);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return AllInstrumentScanItemResult.Completed(
                input,
                CreateFailedDirections(
                    run,
                    input,
                    indicatorRequest.Manifest.Id,
                    $"INDICATOR_ENGINE_FAILED: {exception.GetType().Name}: {exception.Message}",
                    [],
                    null));
        }

        if (!MatchesIndicatorIdentity(indicatorResult.Identity, indicatorRequest))
        {
            return AllInstrumentScanItemResult.Failed(
                input,
                "INDICATOR_RESULT_IDENTITY_MISMATCH: The indicator result does not belong to the requested run, manifest, instrument, evaluation date, and manifest hash.");
        }

        var directions = new List<AllInstrumentScanDirectionResult>(2);
        foreach (var side in new[] { PositionSide.Long, PositionSide.Short })
        {
            try
            {
                var decision = candidateEngine.Evaluate(
                    side,
                    indicatorResult,
                    request.StrategyParameters.Indicators,
                    request.StrategyParameters.Candidates);
                directions.Add(CreateDirection(
                    run,
                    input,
                    indicatorRequest.Manifest.Id,
                    side,
                    indicatorResult,
                    decision));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                directions.Add(CreateFailedDirection(
                    run,
                    input,
                    indicatorRequest.Manifest.Id,
                    side,
                    $"CANDIDATE_ENGINE_FAILED: {exception.GetType().Name}: {exception.Message}",
                    indicatorResult.IndicatorResults,
                    indicatorResult.CalculationStartBarDate));
            }
        }

        return AllInstrumentScanItemResult.Completed(input, directions);
    }

    private AllInstrumentScanDirectionResult CreateDirection(
        AnalysisRun run,
        AllInstrumentScanInput input,
        Guid manifestId,
        PositionSide side,
        TechnicalIndicatorCalculationResult indicatorResult,
        CandidateSelectionDecision decision)
    {
        var createdAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var technical = new TechnicalAnalysisResult(
            Guid.NewGuid(),
            run.Id,
            manifestId,
            input.InstrumentId,
            side,
            decision.Outcome,
            decision.ReasonSummary,
            decision.Reasons,
            indicatorResult.CalculationStartBarDate,
            indicatorResult.IndicatorResults);
        CandidateResult? candidate = null;
        if (decision.IsCandidate)
        {
            if (decision.Score is null || decision.Confidence is null || decision.PrimaryReason is null)
            {
                throw new InvalidOperationException("A candidate decision must contain score, confidence, and primary reason.");
            }

            candidate = CandidateResult.Create(
                CandidateResultId.New(),
                technical,
                decision.Score.Value,
                decision.Confidence.Value,
                decision.PrimaryReason,
                decision.Components,
                createdAtUtc);
        }

        return new AllInstrumentScanDirectionResult(side, technical, candidate, createdAtUtc);
    }

    private IReadOnlyList<AllInstrumentScanDirectionResult> CreateFailedDirections(
        AnalysisRun run,
        AllInstrumentScanInput input,
        Guid manifestId,
        string reason,
        IReadOnlyList<IndicatorResult> indicators,
        DateOnly? calculationStartBarDate) =>
        new[] { PositionSide.Long, PositionSide.Short }
            .Select(side => CreateFailedDirection(
                run,
                input,
                manifestId,
                side,
                reason,
                indicators,
                calculationStartBarDate))
            .ToArray();

    private AllInstrumentScanDirectionResult CreateFailedDirection(
        AnalysisRun run,
        AllInstrumentScanInput input,
        Guid manifestId,
        PositionSide side,
        string reason,
        IReadOnlyList<IndicatorResult> indicators,
        DateOnly? calculationStartBarDate)
    {
        var createdAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var technical = new TechnicalAnalysisResult(
            Guid.NewGuid(),
            run.Id,
            manifestId,
            input.InstrumentId,
            side,
            TechnicalAnalysisOutcome.Failed,
            reason,
            [$"UNEXPECTED_FAILURE: {reason}"],
            calculationStartBarDate,
            indicators);
        return new AllInstrumentScanDirectionResult(side, technical, null, createdAtUtc);
    }

    private void ValidateRunContract(AllInstrumentScanRequest request)
    {
        var run = request.Run;
        var snapshot = request.StrategySnapshot;
        var normalizedParameters = request.StrategyParameters.ToSnapshotNormalizedJson(
            snapshot.StrategyKey,
            snapshot.StrategyVersion,
            snapshot.AlgorithmVersion);
        var parameterHash = request.StrategyParameters.CalculateSnapshotHash(
            snapshot.StrategyKey,
            snapshot.StrategyVersion,
            snapshot.AlgorithmVersion);
        if (run.Status != AnalysisRunStatus.Running)
        {
            throw new InvalidOperationException("An all-instrument scan requires a Running analysis run.");
        }

        if (run.PointInTimeStatus != PointInTimeStatus.Verified)
        {
            throw new InvalidOperationException("An all-instrument scan cannot produce candidates for an unverified run.");
        }

        if (run.RecordedCutoffAtUtc > run.AnalyzedAtUtc)
        {
            throw new InvalidOperationException("The recorded cutoff cannot be later than analysis time.");
        }

        if (run.IndicatorEngineVersion != indicatorEngine.Version ||
            run.CandidateEngineVersion != candidateEngine.Version)
        {
            throw new InvalidOperationException("The run must freeze the exact indicator and candidate engine versions in use.");
        }

        if (run.StrategyParameterSnapshotId != snapshot.Id ||
            snapshot.SchemaVersion != CandidateStrategyParameters.SchemaVersion ||
            snapshot.AlgorithmVersion != candidateEngine.Version ||
            snapshot.NormalizedParametersJson != normalizedParameters ||
            snapshot.ParametersHash != parameterHash)
        {
            throw new InvalidOperationException("The typed strategy parameters do not match the frozen parameter snapshot.");
        }

        if (snapshot.CapturedAtUtc > run.AnalyzedAtUtc)
        {
            throw new InvalidOperationException("The strategy snapshot cannot be captured after analysis starts.");
        }
    }

    private static bool MatchesIndicatorIdentity(
        TechnicalIndicatorCalculationIdentity? identity,
        TechnicalIndicatorCalculationRequest request) =>
        identity is not null &&
        identity.AnalysisRunId == request.Manifest.AnalysisRunId &&
        identity.AnalysisInputManifestId == request.Manifest.Id &&
        identity.InstrumentId == request.Manifest.InstrumentId &&
        identity.EvaluationBarDate == request.EvaluationBarDate &&
        identity.ManifestHash == request.Manifest.ManifestHash;

    private static IReadOnlyList<AllInstrumentScanInput> ValidateAndOrderInputs(
        IReadOnlyList<AllInstrumentScanInput> inputs)
    {
        if (inputs.GroupBy(input => input.InstrumentId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Each instrument may appear only once in an all-instrument scan.", nameof(inputs));
        }

        if (inputs.GroupBy(input => input.InstrumentCode, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Each instrument code may appear only once in an all-instrument scan.", nameof(inputs));
        }

        return inputs
            .OrderBy(input => input.InstrumentCode, StringComparer.Ordinal)
            .ThenBy(input => input.InstrumentId.Value)
            .ToArray();
    }
}
