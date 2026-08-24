using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Analysis;

public sealed record AnalysisRun
{
    public AnalysisRun(
        AnalysisRunId id,
        DateOnly evaluationBarDate,
        DateTimeOffset analyzedAtUtc,
        DateTimeOffset recordedCutoffAtUtc,
        AnalysisRunMode mode,
        AnalysisRunStatus status,
        Guid strategyParameterSnapshotId,
        PointInTimeStatus pointInTimeStatus,
        string priceSelectorVersion,
        string adjustmentEngineVersion,
        string indicatorEngineVersion,
        string candidateEngineVersion)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Analysis run ID cannot be empty.", nameof(id));
        }

        if (strategyParameterSnapshotId == Guid.Empty)
        {
            throw new ArgumentException("Strategy snapshot ID cannot be empty.", nameof(strategyParameterSnapshotId));
        }

        Id = id;
        EvaluationBarDate = evaluationBarDate;
        AnalyzedAtUtc = DomainGuard.Utc(analyzedAtUtc, nameof(analyzedAtUtc));
        RecordedCutoffAtUtc = DomainGuard.Utc(recordedCutoffAtUtc, nameof(recordedCutoffAtUtc));
        Mode = mode;
        Status = status;
        StrategyParameterSnapshotId = strategyParameterSnapshotId;
        PointInTimeStatus = pointInTimeStatus;
        PriceSelectorVersion = DomainGuard.Required(priceSelectorVersion, nameof(priceSelectorVersion));
        AdjustmentEngineVersion = DomainGuard.Required(adjustmentEngineVersion, nameof(adjustmentEngineVersion));
        IndicatorEngineVersion = DomainGuard.Required(indicatorEngineVersion, nameof(indicatorEngineVersion));
        CandidateEngineVersion = DomainGuard.Required(candidateEngineVersion, nameof(candidateEngineVersion));
    }

    public AnalysisRunId Id { get; }
    public DateOnly EvaluationBarDate { get; }
    public DateTimeOffset AnalyzedAtUtc { get; }
    public DateTimeOffset RecordedCutoffAtUtc { get; }
    public AnalysisRunMode Mode { get; }
    public AnalysisRunStatus Status { get; }
    public Guid StrategyParameterSnapshotId { get; }
    public PointInTimeStatus PointInTimeStatus { get; }
    public string PriceSelectorVersion { get; }
    public string AdjustmentEngineVersion { get; }
    public string IndicatorEngineVersion { get; }
    public string CandidateEngineVersion { get; }

    public bool CanBeUsedForFormalBacktest =>
        Mode == AnalysisRunMode.Backtest &&
        Status == AnalysisRunStatus.Succeeded &&
        PointInTimeStatus == PointInTimeStatus.Verified;
}
