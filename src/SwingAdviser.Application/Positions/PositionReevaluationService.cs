using SwingAdviser.Domain.Common;

namespace SwingAdviser.Application.Positions;

/// <summary>
/// Starts one holding re-evaluation against the already-created running analysis run.
/// Position-specific calculations and the append-only persistence boundary remain behind
/// <see cref="IPositionReevaluationRepository"/> so this workflow has no execution-writing path.
/// </summary>
public sealed record PositionReevaluationRequest
{
    public PositionReevaluationRequest(AnalysisRunId analysisRunId)
    {
        if (analysisRunId.Value == Guid.Empty)
        {
            throw new ArgumentException("Analysis run ID cannot be empty.", nameof(analysisRunId));
        }

        AnalysisRunId = analysisRunId;
    }

    public AnalysisRunId AnalysisRunId { get; }
}

/// <summary>
/// The repository guarantees that this is Open at the run cutoff. Instrument identity is
/// included only for deterministic work ordering; duplicate instruments remain independent
/// positions and must not be collapsed.
/// </summary>
public sealed record OpenPositionReevaluationTarget
{
    public OpenPositionReevaluationTarget(PositionId positionId, InstrumentId instrumentId)
    {
        if (positionId.Value == Guid.Empty || instrumentId.Value == Guid.Empty)
        {
            throw new ArgumentException("Position and instrument IDs cannot be empty.");
        }

        PositionId = positionId;
        InstrumentId = instrumentId;
    }

    public PositionId PositionId { get; }
    public InstrumentId InstrumentId { get; }
}

public sealed record PositionReevaluationProgress(
    int ProcessedPositionCount,
    int TotalPositionCount,
    int CompletedPositionCount,
    int FailedPositionCount,
    PositionId CurrentPositionId);

public enum PositionReevaluationItemStatus
{
    Completed,
    Failed,
}

public sealed record PositionReevaluationItemResult
{
    private PositionReevaluationItemResult(
        OpenPositionReevaluationTarget target,
        PositionReevaluationItemStatus status,
        PositionEvaluationOutcome? outcome,
        string? failureReason)
    {
        Target = target;
        Status = status;
        Outcome = outcome;
        FailureReason = failureReason;
    }

    public OpenPositionReevaluationTarget Target { get; }
    public PositionReevaluationItemStatus Status { get; }
    public PositionEvaluationOutcome? Outcome { get; }
    public string? FailureReason { get; }

    internal static PositionReevaluationItemResult Completed(
        OpenPositionReevaluationTarget target,
        PositionEvaluationOutcome outcome) =>
        new(target, PositionReevaluationItemStatus.Completed, outcome, null);

    internal static PositionReevaluationItemResult Failed(
        OpenPositionReevaluationTarget target,
        string failureReason) =>
        new(target, PositionReevaluationItemStatus.Failed, null, failureReason);
}

public sealed record PositionReevaluationSummary
{
    internal PositionReevaluationSummary(
        AnalysisRunId analysisRunId,
        IReadOnlyList<PositionReevaluationItemResult> items)
    {
        AnalysisRunId = analysisRunId;
        Items = Array.AsReadOnly(items.ToArray());
        TotalPositionCount = Items.Count;
        CompletedPositionCount = Items.Count(item => item.Status == PositionReevaluationItemStatus.Completed);
        FailedPositionCount = Items.Count(item => item.Status == PositionReevaluationItemStatus.Failed);
        SuggestedRunStatus = FailedPositionCount == 0
            ? AnalysisRunStatus.Succeeded
            : CompletedPositionCount > 0
                ? AnalysisRunStatus.PartiallySucceeded
                : AnalysisRunStatus.Failed;
    }

    public AnalysisRunId AnalysisRunId { get; }
    public IReadOnlyList<PositionReevaluationItemResult> Items { get; }
    public int TotalPositionCount { get; }
    public int CompletedPositionCount { get; }
    public int FailedPositionCount { get; }
    public AnalysisRunStatus SuggestedRunStatus { get; }
}

/// <summary>
/// Application-facing boundary for selecting open positions and atomically appending one
/// position evaluation. It deliberately exposes no trade execution, lot-allocation, or
/// risk-plan mutation operation.
/// </summary>
public interface IPositionReevaluationRepository
{
    Task<IReadOnlyList<OpenPositionReevaluationTarget>> ListOpenPositionsAsync(
        AnalysisRunId analysisRunId,
        CancellationToken cancellationToken = default);

    Task<PositionEvaluationOutcome> EvaluateAndPersistAsync(
        AnalysisRunId analysisRunId,
        PositionId positionId,
        CancellationToken cancellationToken = default);
}

public interface IPositionReevaluationService
{
    Task<PositionReevaluationSummary> ReevaluateAsync(
        PositionReevaluationRequest request,
        IProgress<PositionReevaluationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class PositionReevaluationService(IPositionReevaluationRepository repository)
    : IPositionReevaluationService
{
    private readonly IPositionReevaluationRepository repository = repository ??
        throw new ArgumentNullException(nameof(repository));

    public async Task<PositionReevaluationSummary> ReevaluateAsync(
        PositionReevaluationRequest request,
        IProgress<PositionReevaluationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var targets = await repository.ListOpenPositionsAsync(
            request.AnalysisRunId,
            cancellationToken);
        var orderedTargets = ValidateAndOrderTargets(targets);
        var items = new List<PositionReevaluationItemResult>(orderedTargets.Count);
        var completedCount = 0;
        var failedCount = 0;

        foreach (var target in orderedTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PositionReevaluationItemResult item;
            try
            {
                var outcome = await repository.EvaluateAndPersistAsync(
                    request.AnalysisRunId,
                    target.PositionId,
                    cancellationToken);
                if (!Enum.IsDefined(outcome))
                {
                    throw new InvalidDataException("The position reevaluation returned an unsupported outcome.");
                }

                item = PositionReevaluationItemResult.Completed(target, outcome);
                completedCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                item = PositionReevaluationItemResult.Failed(
                    target,
                    $"POSITION_REEVALUATION_FAILED: {exception.GetType().Name}: {exception.Message}");
                failedCount++;
            }

            items.Add(item);
            ReportProgress(
                progress,
                new PositionReevaluationProgress(
                    items.Count,
                    orderedTargets.Count,
                    completedCount,
                    failedCount,
                    target.PositionId));
        }

        return new PositionReevaluationSummary(request.AnalysisRunId, items);
    }

    private static IReadOnlyList<OpenPositionReevaluationTarget> ValidateAndOrderTargets(
        IReadOnlyList<OpenPositionReevaluationTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Any(target => target is null))
        {
            throw new ArgumentException("An open-position reevaluation target cannot be null.", nameof(targets));
        }

        if (targets.GroupBy(target => target.PositionId).Any(group => group.Count() != 1))
        {
            throw new ArgumentException(
                "Each open position may appear only once in a reevaluation run.",
                nameof(targets));
        }

        return targets
            .OrderBy(target => target.InstrumentId.Value)
            .ThenBy(target => target.PositionId.Value)
            .ToArray();
    }

    private static void ReportProgress(
        IProgress<PositionReevaluationProgress>? progress,
        PositionReevaluationProgress value)
    {
        if (progress is null)
        {
            return;
        }

        try
        {
            progress.Report(value);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Observers cannot invalidate completed, append-only evaluations.
        }
    }
}
