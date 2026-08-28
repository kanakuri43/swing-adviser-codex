namespace SwingAdviser.Infrastructure.Persistence.Entities;

internal sealed class PositionRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string PositionSide { get; set; } = string.Empty;
    public Guid? StrategyParameterSnapshotId { get; set; }
    public Guid? OriginCandidateResultId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class PositionStateRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid PositionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ReconciliationStatus { get; set; } = string.Empty;
    public DateTimeOffset EffectiveAtUtc { get; set; }
    public string? Memo { get; set; }
    public string Reason { get; set; } = string.Empty;
}

internal sealed class TradeExecutionRow
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string ExecutionKind { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public Guid? CandidateContextId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class TradeExecutionRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public Guid? SourceArtifactId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid TradeExecutionId { get; set; }
    public DateTimeOffset ExecutedAtUtc { get; set; }
    public decimal Price { get; set; }
    public long Quantity { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string RecordDisposition { get; set; } = string.Empty;
    public string ChangeKind { get; set; } = string.Empty;
    public string? Broker { get; set; }
    public string? ExternalReference { get; set; }
    public string? UserNote { get; set; }
    public DateTimeOffset UserConfirmedAtUtc { get; set; }
    public string? CorrectionReason { get; set; }
}

internal sealed class MarginLotRow
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public Guid OpeningTradeExecutionId { get; set; }
    public Guid InitialOpeningTradeExecutionRevisionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class MarginLotContractRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public Guid? SourceArtifactId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid MarginLotId { get; set; }
    public Guid OpeningTradeExecutionRevisionId { get; set; }
    public string MarginType { get; set; } = string.Empty;
    public string Broker { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public DateOnly EffectiveFromDate { get; set; }
    public DateOnly? EffectiveToDate { get; set; }
    public string TermType { get; set; } = string.Empty;
    public DateTimeOffset? FinalRepaymentAtUtc { get; set; }
    public decimal? BuyerInterestRate { get; set; }
    public decimal? StockLendingRate { get; set; }
    public string? RateUnit { get; set; }
    public string ContractCurrency { get; set; } = string.Empty;
    public string? DayCountConvention { get; set; }
    public string SpecialFeePolicyJson { get; set; } = string.Empty;
    public string RightsProcessingJson { get; set; } = string.Empty;
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public string ChangeKind { get; set; } = string.Empty;
}

internal sealed class LotAllocationRevisionRow
{
    public Guid Id { get; set; }
    public Guid AllocationKey { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public Guid ClosingTradeExecutionId { get; set; }
    public Guid ClosingTradeExecutionRevisionId { get; set; }
    public Guid MarginLotId { get; set; }
    public long Quantity { get; set; }
    public string RecordDisposition { get; set; } = string.Empty;
    public string ChangeKind { get; set; } = string.Empty;
    public DateTimeOffset UserConfirmedAtUtc { get; set; }
    public string? CorrectionReason { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed class PositionAdjustmentRow
{
    public Guid Id { get; set; }
    public Guid AdjustmentKey { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public Guid? ReplacesAdjustmentKey { get; set; }
    public Guid PositionId { get; set; }
    public Guid MarginLotId { get; set; }
    public Guid CorporateActionRevisionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public decimal? QuantityFactor { get; set; }
    public decimal? PriceFactor { get; set; }
    public decimal BeforeQuantity { get; set; }
    public decimal? AfterQuantity { get; set; }
    public decimal BeforeBasisPrice { get; set; }
    public decimal? AfterBasisPrice { get; set; }
    public decimal? BeforeFixedAtr { get; set; }
    public decimal? AfterFixedAtr { get; set; }
    public decimal? BeforeStopPrice { get; set; }
    public decimal? AfterStopPrice { get; set; }
    public decimal? BeforeTakeProfitPrice { get; set; }
    public decimal? AfterTakeProfitPrice { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed class RiskBasisSnapshotRow
{
    public Guid Id { get; set; }
    public Guid MarginLotId { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public Guid OpeningTradeExecutionRevisionId { get; set; }
    public Guid? OriginCandidateResultId { get; set; }
    public Guid? StrategyParameterSnapshotId { get; set; }
    public Guid? AnalysisInputManifestId { get; set; }
    public string? PriceCurrency { get; set; }
    public string? PriceUnitBasisSha256 { get; set; }
    public decimal EntryBasisPrice { get; set; }
    public DateOnly AtrReferenceBarDate { get; set; }
    public decimal FixedAtr { get; set; }
    public long AtrPeriod { get; set; }
    public string AtrAlgorithmId { get; set; } = string.Empty;
    public decimal StopMultiplier { get; set; }
    public decimal RiskAmountR { get; set; }
    public decimal PartialTakeProfitRMultiple { get; set; }
    public decimal PartialTakeProfitFraction { get; set; }
    public decimal InitialStopPrice { get; set; }
    public decimal InitialTakeProfitPrice { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class RiskPlanRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid RiskBasisSnapshotId { get; set; }
    public decimal StopPrice { get; set; }
    public decimal TakeProfitPrice { get; set; }
    public Guid? TriggerTradeExecutionId { get; set; }
    public Guid? TriggerLotAllocationRevisionId { get; set; }
    public Guid? TriggerPositionAdjustmentId { get; set; }
    public string PlanReason { get; set; } = string.Empty;
    public DateTimeOffset EffectiveAtUtc { get; set; }
    public bool IsCostAdjusted { get; set; }
}

internal sealed class PositionEvaluationInputManifestRow
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public Guid PositionId { get; set; }
    public Guid AnalysisInputManifestId { get; set; }
    public Guid CurrentPriceRevisionId { get; set; }
    public string TradeExecutionRevisionIdsJson { get; set; } = string.Empty;
    public string LotAllocationRevisionIdsJson { get; set; } = string.Empty;
    public string PositionAdjustmentIdsJson { get; set; } = string.Empty;
    public string ContractRevisionIdsJson { get; set; } = string.Empty;
    public string RiskBasisSnapshotIdsJson { get; set; } = string.Empty;
    public string RiskPlanRevisionIdsJson { get; set; } = string.Empty;
    public string MarginCostObservationIdsJson { get; set; } = string.Empty;
    public string ProjectionVersion { get; set; } = string.Empty;
    public DateTimeOffset RecordedCutoffAtUtc { get; set; }
    public string ManifestSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class PositionEvaluationRow
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public Guid PositionId { get; set; }
    public Guid PositionEvaluationInputManifestId { get; set; }
    public DateOnly EvaluationBarDate { get; set; }
    public string EvaluationOutcome { get; set; } = string.Empty;
    public string? ExitDecision { get; set; }
    public string ReasonSummary { get; set; } = string.Empty;
    public string ReasonsJson { get; set; } = string.Empty;
    public string LotEvaluationsJson { get; set; } = string.Empty;
    public decimal? CurrentQuantity { get; set; }
    public decimal? PricePnl { get; set; }
    public decimal? ConfirmedCostPnl { get; set; }
    public decimal? EstimatedNetPnl { get; set; }
    public decimal? CostToRRatio { get; set; }
    public long? PartialExitQuantity { get; set; }
    public string PartialExitStatus { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
