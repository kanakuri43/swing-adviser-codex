namespace SwingAdviser.Infrastructure.Persistence.Entities;

internal sealed class DailyPriceRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public DateOnly BarDate { get; set; }
    public string Provider { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class DailyPriceRevisionRow : IRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = null!;
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public string AvailabilityStatus { get; set; } = null!;
    public DateTimeOffset FirstObservedAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid? SourceArtifactId { get; set; }
    public Guid DailyPriceId { get; set; }
    public string ProviderSymbol { get; set; } = null!;
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? ProviderAdjclose { get; set; }
    public string Currency { get; set; } = null!;
    public string BarStatus { get; set; } = null!;
    public string? ProviderEventId { get; set; }
}

internal sealed class PriceHistoryAssessmentRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Provider { get; set; } = null!;
    public DateOnly? FirstValidBarDate { get; set; }
    public DateOnly? LastValidBarDate { get; set; }
    public long ValidBarCount { get; set; }
    public string CompletenessStatus { get; set; } = null!;
    public string? ListingDateEvidence { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset AssessedAtUtc { get; set; }
    public string AlgorithmVersion { get; set; } = null!;
    public Guid? SourceArtifactId { get; set; }
}

internal sealed class PriceRevisionSetRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Provider { get; set; } = null!;
    public Guid? ParentSetId { get; set; }
    public DateOnly? FirstBarDate { get; set; }
    public DateOnly? LastBarDate { get; set; }
    public long BarCount { get; set; }
    public string SetSha256 { get; set; } = null!;
    public string SelectorVersion { get; set; } = null!;
    public DateTimeOffset SelectedAvailableCutoffAtUtc { get; set; }
    public DateTimeOffset SelectedRecordedCutoffAtUtc { get; set; }
    public string PointInTimeStatus { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class PriceRevisionSetChangeRow
{
    public Guid Id { get; set; }
    public Guid PriceRevisionSetId { get; set; }
    public string Operation { get; set; } = null!;
    public Guid? DailyPriceRevisionId { get; set; }
    public Guid? ReplacedDailyPriceRevisionId { get; set; }
    public DateOnly BarDate { get; set; }
    public long Ordinal { get; set; }
}

internal sealed class CorporateActionRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Provider { get; set; } = null!;
    public string? SourceEventId { get; set; }
    public string DerivedEventKey { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class CorporateActionRevisionRow : IRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = null!;
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public string AvailabilityStatus { get; set; } = null!;
    public DateTimeOffset FirstObservedAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid? SourceArtifactId { get; set; }
    public Guid CorporateActionId { get; set; }
    public string ActionType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateOnly EffectiveDate { get; set; }
    public DateTimeOffset? AnnouncedAtUtc { get; set; }
    public long? RatioNumerator { get; set; }
    public long? RatioDenominator { get; set; }
    public decimal? CashAmountPerShare { get; set; }
    public string? Currency { get; set; }
    public string PointInTimeStatus { get; set; } = null!;
    public string? Notes { get; set; }
}

internal sealed class MarginEligibilityRecordRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Provider { get; set; } = null!;
    public string SourceRecordKey { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class MarginEligibilityRevisionRow : IRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = null!;
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public string AvailabilityStatus { get; set; } = null!;
    public DateTimeOffset FirstObservedAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid? SourceArtifactId { get; set; }
    public Guid MarginEligibilityRecordId { get; set; }
    public DateOnly EffectiveFromDate { get; set; }
    public DateOnly? EffectiveToDate { get; set; }
    public string StandardizedMarginStatus { get; set; } = null!;
    public string LoanStockStatus { get; set; } = null!;
    public string LongOpenStatus { get; set; } = null!;
    public string ShortOpenStatus { get; set; } = null!;
    public string RegulationCodesJson { get; set; } = null!;
    public string? Notes { get; set; }
}

internal sealed class PublishedMarginCostRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Provider { get; set; } = null!;
    public string CostType { get; set; } = null!;
    public string SourceRecordKey { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class PublishedMarginCostRevisionRow : IRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = null!;
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public string AvailabilityStatus { get; set; } = null!;
    public DateTimeOffset FirstObservedAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid? SourceArtifactId { get; set; }
    public Guid PublishedMarginCostId { get; set; }
    public DateOnly ApplicationDate { get; set; }
    public DateOnly? PeriodStartDate { get; set; }
    public DateOnly? PeriodEndDate { get; set; }
    public long? IncludedDays { get; set; }
    public string PublicationStatus { get; set; } = null!;
    public decimal? AmountPerShare { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string? Unit { get; set; }
}

internal sealed class FundamentalRecordRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Provider { get; set; } = null!;
    public string SourceRecordKey { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class FundamentalRevisionRow : IRevisionRow
{
    public Guid Id { get; set; }
    public long RevisionNo { get; set; }
    public Guid? SupersedesId { get; set; }
    public string ContentSha256 { get; set; } = null!;
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public string AvailabilityStatus { get; set; } = null!;
    public DateTimeOffset FirstObservedAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public Guid? SourceArtifactId { get; set; }
    public Guid FundamentalRecordId { get; set; }
    public DateOnly AsOfDate { get; set; }
    public DateOnly? FiscalPeriodEndDate { get; set; }
    public decimal? Per { get; set; }
    public decimal? Pbr { get; set; }
    public decimal? MarketCap { get; set; }
    public string? Currency { get; set; }
    public string MissingFieldsJson { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
}
