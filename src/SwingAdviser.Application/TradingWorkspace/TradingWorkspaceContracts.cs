using SwingAdviser.Domain.Common;

namespace SwingAdviser.Application.TradingWorkspace;

public sealed record TradingWorkspaceEnvironment(string DatabasePath, bool UsesDevelopmentData)
{
    public string Notice => UsesDevelopmentData
        ? $"開発データモード: {DatabasePath}（実運用DBとは別ファイル）"
        : $"ローカルDB: {DatabasePath}";
}

public sealed record CandidateListItem(
    Guid CandidateId,
    Guid InstrumentId,
    string Code,
    string Name,
    PositionSide Side,
    long Score,
    ConfidenceLevel Confidence,
    DateOnly EvaluationBarDate,
    DateTimeOffset AnalyzedAtUtc,
    string StrategyLabel,
    string PrimaryReason,
    AiAttemptStatus? AiStatus,
    AiVerdict? AiVerdict,
    string? AiFailureDetail,
    OpenPermissionStatus? ShortOpenStatus,
    string? ShortRestrictionNote);

public sealed record MarginLotListItem(
    Guid MarginLotId,
    Guid PositionId,
    string DisplayLabel,
    decimal RemainingQuantity,
    DateTimeOffset OpenedAtUtc);

public sealed record PositionListItem(
    Guid PositionId,
    Guid InstrumentId,
    string Code,
    string Name,
    PositionSide Side,
    decimal Quantity,
    decimal? EntryBasisPrice,
    decimal? CurrentPrice,
    DateOnly? EvaluationBarDate,
    PositionEvaluationOutcome? EvaluationOutcome,
    DateTimeOffset? EvaluationCreatedAtUtc,
    bool IsEvaluationStale,
    string StrategyLabel,
    ExitDecision? Decision,
    string DecisionReason,
    decimal? PriceProfitAndLoss,
    decimal? ConfirmedCostProfitAndLoss,
    decimal? EstimatedNetProfitAndLoss,
    decimal? CostToRRatio,
    long? PartialExitQuantity,
    PartialExitStatus? PartialExitStatus,
    decimal? StopPrice,
    decimal? TakeProfitPrice,
    MarginTermType TermType,
    DateTimeOffset? FinalRepaymentAtUtc,
    ReconciliationStatus ReconciliationStatus,
    IReadOnlyList<MarginLotListItem> Lots);

public sealed record TradeExecutionRevisionListItem(
    Guid RevisionId,
    long RevisionNumber,
    ExecutionChangeKind ChangeKind,
    RecordDisposition Disposition,
    DateTimeOffset ExecutedAtUtc,
    decimal Price,
    long Quantity,
    string Currency,
    DateTimeOffset UserConfirmedAtUtc,
    string? Broker,
    string? ExternalReference,
    string? UserNote,
    string? CorrectionReason);

public sealed record TradeExecutionListItem(
    Guid ExecutionId,
    Guid PositionId,
    Guid InstrumentId,
    string Code,
    string Name,
    PositionSide Side,
    ExecutionKind Kind,
    ExecutionOrigin Origin,
    IReadOnlyList<TradeExecutionRevisionListItem> Revisions)
{
    public TradeExecutionRevisionListItem CurrentRevision => Revisions[^1];
}

public sealed record TradingWorkspaceSnapshot(
    IReadOnlyList<CandidateListItem> Candidates,
    IReadOnlyList<PositionListItem> Positions,
    IReadOnlyList<TradeExecutionListItem> Executions,
    DateTimeOffset LoadedAtUtc,
    string? DataNotice = null);

public sealed record ManualLotAllocation(Guid MarginLotId, long Quantity);

public sealed record RegisterManualExecutionRequest(
    Guid InstrumentId,
    Guid? PositionId,
    Guid? CandidateContextId,
    PositionSide Side,
    ExecutionKind Kind,
    DateTimeOffset ExecutedAtUtc,
    decimal Price,
    long Quantity,
    string Currency,
    DateTimeOffset UserConfirmedAtUtc,
    bool IsUserConfirmed,
    IReadOnlyList<ManualLotAllocation> LotAllocations,
    string? Broker = null,
    string? ExternalReference = null,
    string? UserNote = null);

public sealed record CorrectManualExecutionRequest(
    Guid ExecutionId,
    Guid ExpectedCurrentRevisionId,
    DateTimeOffset ExecutedAtUtc,
    decimal Price,
    long Quantity,
    string Currency,
    DateTimeOffset UserConfirmedAtUtc,
    bool IsUserConfirmed,
    string CorrectionReason,
    string? Broker = null,
    string? ExternalReference = null,
    string? UserNote = null);

public sealed record ManualExecutionResult(
    Guid ExecutionId,
    Guid RevisionId,
    Guid PositionId,
    long RevisionNumber);

public interface ITradingWorkspaceRepository
{
    Task<TradingWorkspaceSnapshot> LoadAsync(CancellationToken cancellationToken = default);

    Task<ManualExecutionResult> RegisterManualExecutionAsync(
        RegisterManualExecutionRequest request,
        CancellationToken cancellationToken = default);

    Task<ManualExecutionResult> CorrectManualExecutionAsync(
        CorrectManualExecutionRequest request,
        CancellationToken cancellationToken = default);
}
