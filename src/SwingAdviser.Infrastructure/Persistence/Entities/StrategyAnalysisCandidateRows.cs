namespace SwingAdviser.Infrastructure.Persistence.Entities;

internal sealed class StrategyParameterSnapshotRow
{
    public Guid Id { get; set; }
    public string StrategyKey { get; set; } = null!;
    public string StrategyVersion { get; set; } = null!;
    public string SchemaVersion { get; set; } = null!;
    public string AlgorithmVersion { get; set; } = null!;
    public string ParametersJson { get; set; } = null!;
    public string ParametersSha256 { get; set; } = null!;
    public DateTimeOffset CapturedAtUtc { get; set; }
    public string? SourceDescription { get; set; }
}

internal sealed class AnalysisRunRow
{
    public Guid Id { get; set; }
    public DateOnly EvaluationBarDate { get; set; }
    public DateTimeOffset AnalyzedAtUtc { get; set; }
    public DateTimeOffset RecordedCutoffAtUtc { get; set; }
    public string RunMode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid StrategyParameterSnapshotId { get; set; }
    public string PointInTimeStatus { get; set; } = null!;
    public string PriceSelectorVersion { get; set; } = null!;
    public string AdjustmentEngineVersion { get; set; } = null!;
    public string IndicatorEngineVersion { get; set; } = null!;
    public string CandidateEngineVersion { get; set; } = null!;
    public Guid MarketCalendarVersionId { get; set; }
    public string ApplicationVersion { get; set; } = null!;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long TotalCount { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public string? Summary { get; set; }
}

internal sealed class AnalysisInputManifestRow
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public Guid InstrumentId { get; set; }
    public string PriceProvider { get; set; } = null!;
    public Guid PriceRevisionSetId { get; set; }
    public DateOnly? FirstBarDate { get; set; }
    public DateOnly? LastBarDate { get; set; }
    public long BarCount { get; set; }
    public long RequiredBarCount { get; set; }
    public string HistoryStatus { get; set; } = null!;
    public string PointInTimeStatus { get; set; } = null!;
    public string SelectionBasis { get; set; } = null!;
    public string SelectionRuleVersion { get; set; } = null!;
    public DateTimeOffset SelectedRecordedCutoffAtUtc { get; set; }
    public DateTimeOffset SelectedAvailableCutoffAtUtc { get; set; }
    public string PriceRevisionSetSha256 { get; set; } = null!;
    public string CorporateActionSetSha256 { get; set; } = null!;
    public string ManifestSha256 { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class AnalysisActionApplicationRow
{
    public Guid Id { get; set; }
    public Guid AnalysisInputManifestId { get; set; }
    public Guid CorporateActionRevisionId { get; set; }
    public string ApplicationStatus { get; set; } = null!;
    public Guid? ReferencePriceRevisionId { get; set; }
    public decimal? PriceFactor { get; set; }
    public decimal? VolumeFactor { get; set; }
    public decimal? CumulativePriceFactor { get; set; }
    public decimal? CumulativeVolumeFactor { get; set; }
    public string Reason { get; set; } = null!;
    public long Ordinal { get; set; }
}

internal sealed class TechnicalAnalysisResultRow
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public Guid AnalysisInputManifestId { get; set; }
    public Guid InstrumentId { get; set; }
    public string PositionSide { get; set; } = null!;
    public string SignalPurpose { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public string ReasonSummary { get; set; } = null!;
    public string ReasonsJson { get; set; } = null!;
    public DateOnly? CalculationStartBarDate { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class IndicatorResultRow
{
    public Guid Id { get; set; }
    public Guid TechnicalAnalysisResultId { get; set; }
    public string IndicatorKey { get; set; } = null!;
    public string AlgorithmId { get; set; } = null!;
    public string ParametersJson { get; set; } = null!;
    public string ValuesJson { get; set; } = null!;
    public DateOnly CalculationStartBarDate { get; set; }
    public string InputSha256 { get; set; } = null!;
    public long Ordinal { get; set; }
}

internal sealed class CandidateResultRow
{
    public Guid Id { get; set; }
    public Guid TechnicalAnalysisResultId { get; set; }
    public long Score { get; set; }
    public string Confidence { get; set; } = null!;
    public string PrimaryReason { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class CandidateScoreComponentRow
{
    public Guid Id { get; set; }
    public Guid CandidateResultId { get; set; }
    public string ComponentKey { get; set; } = null!;
    public bool Matched { get; set; }
    public string RawValueJson { get; set; } = null!;
    public decimal Weight { get; set; }
    public decimal AwardedScore { get; set; }
    public string Reason { get; set; } = null!;
    public long Ordinal { get; set; }
}
