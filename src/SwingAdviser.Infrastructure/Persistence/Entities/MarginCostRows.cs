namespace SwingAdviser.Infrastructure.Persistence.Entities;

internal sealed class MarginCostItemRow
{
    public Guid Id { get; set; }
    public Guid MarginLotId { get; set; }
    public string CostType { get; set; } = string.Empty;
    public string OccurrenceKey { get; set; } = string.Empty;
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public string? BrokerStatementLineId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class MarginCostObservationRow
{
    public Guid Id { get; set; }
    public Guid MarginCostItemId { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public Guid? ReconcilesEstimateId { get; set; }
    public string ValuationKind { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string AmountStatus { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public string? RateUnit { get; set; }
    public long? IncludedDays { get; set; }
    public string? DayCountConvention { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? FormulaVersion { get; set; }
    public Guid? MarginLotContractRevisionId { get; set; }
    public Guid? PublishedMarginCostRevisionId { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public Guid? SourceArtifactId { get; set; }
    public DateTimeOffset? SourcePublishedAtUtc { get; set; }
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public DateTimeOffset? BookedAtUtc { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed class MarginCostAmountComponentRow
{
    public Guid Id { get; set; }
    public Guid MarginCostObservationId { get; set; }
    public string ComponentType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string AmountStatus { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public long Ordinal { get; set; }
}
