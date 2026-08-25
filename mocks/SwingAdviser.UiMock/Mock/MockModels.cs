using SwingAdviser.Domain.Common;

namespace SwingAdviser.UiMock.Mock;

/// <summary>
/// AI 添付状態。<see cref="AiAttemptStatus"/> に加え、画面固有の区分（中断・旧結果・未実行）を持つ。
/// </summary>
public enum MockAiState
{
    NotRun,
    Queued,
    Running,
    Succeeded,
    Failed,
    FailedInterrupted,
    TimedOut,
    InsufficientInformation,
    Cancelled,
    Stale,
}

public sealed record MockCandidateSeed(
    string Code,
    string Name,
    PositionSide Side,
    int Score,
    ConfidenceLevel Confidence,
    DateOnly EvaluationBarDate,
    DateTimeOffset AnalyzedAtUtc,
    string StrategyLabel,
    string PrimaryReason,
    MockAiState AiState,
    AiVerdict? AiVerdict,
    DateOnly? AiStaleEvaluationBarDate,
    string? AiFailureDetail,
    OpenPermissionStatus? ShortOpenStatus,
    string? ShortRestrictionNote);

public sealed record MockExclusionSeed(
    string Code,
    string Name,
    TechnicalAnalysisOutcome Outcome,
    int? BarCount,
    int? RequiredBarCount,
    string Reason);

public sealed record MockCostLineSeed(
    MarginCostType CostType,
    string Label,
    CostDirection Direction,
    CostValuationKind ValuationKind,
    AmountStatus AmountStatus,
    decimal? Amount);

public sealed record MockPositionSeed(
    string Code,
    string Name,
    PositionSide Side,
    long Quantity,
    string AppliedStrategy,
    ExitDecision? Decision,
    string DecisionReason,
    DateOnly EvaluationBarDate,
    decimal FixedAtr,
    DateOnly AtrReferenceBarDate,
    int AtrPeriod,
    decimal StopMultiplier,
    decimal? CurrentAtrReferenceOnly,
    decimal StopPrice,
    decimal TakeProfitPrice,
    PartialExitStatus PartialExitStatus,
    long? PartialExitQuantity,
    decimal? PartialExitEffectiveFraction,
    string? PartialExitNote,
    ReconciliationStatus ReconciliationStatus,
    string? CorporateActionNote,
    MarginTermType TermType,
    DateOnly? FinalRepaymentDate,
    int? RemainingBusinessDays,
    string? DeadlineChangeNote,
    IReadOnlyList<MockCostLineSeed> CostLines,
    decimal? PriceProfitAndLoss,
    decimal? ConfirmedCostProfitAndLoss,
    decimal? EstimatedNetProfitAndLoss,
    decimal? CostToRRatio);

public sealed record MockExecutionRevisionSeed(
    int RevisionNumber,
    ExecutionChangeKind ChangeKind,
    DateTimeOffset ExecutedAtUtc,
    decimal Price,
    long Quantity,
    DateTimeOffset UserConfirmedAtUtc,
    string? Note);

public sealed record MockExecutionSeed(
    string Code,
    string Name,
    PositionSide Side,
    ExecutionKind Kind,
    IReadOnlyList<MockExecutionRevisionSeed> Revisions,
    string? LotAllocationNote);

public sealed record MockUpdateProgressSeed(
    int Total,
    int Completed,
    int Failed,
    AnalysisRunStatus Status);

public sealed record MockAiQueueProgressSeed(
    int Total,
    int Running,
    int Queued,
    int Completed,
    int Failed);

public sealed record MockScenario(
    string Key,
    string DisplayName,
    IReadOnlyList<MockCandidateSeed> Candidates,
    IReadOnlyList<MockExclusionSeed> Exclusions,
    IReadOnlyList<MockPositionSeed> Positions,
    IReadOnlyList<MockExecutionSeed> Executions,
    MockUpdateProgressSeed UpdateProgress,
    MockAiQueueProgressSeed AiQueueProgress,
    string? EmptyStateNote);
