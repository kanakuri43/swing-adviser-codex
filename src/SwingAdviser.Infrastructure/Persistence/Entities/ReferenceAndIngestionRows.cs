namespace SwingAdviser.Infrastructure.Persistence.Entities;

internal interface IRevisionRow
{
    Guid Id { get; set; }
    long RevisionNo { get; set; }
    Guid? SupersedesId { get; set; }
    string ContentSha256 { get; set; }
    DateTimeOffset? AvailableAtUtc { get; set; }
    string AvailabilityStatus { get; set; }
    DateTimeOffset FirstObservedAtUtc { get; set; }
    DateTimeOffset RecordedAtUtc { get; set; }
    Guid? SourceArtifactId { get; set; }
}

internal sealed class InstrumentRow
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class InstrumentIdentifierRow
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Scheme { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class InstrumentIdentifierRevisionRow : IRevisionRow
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
    public Guid InstrumentIdentifierId { get; set; }
    public string Value { get; set; } = null!;
    public DateOnly? ValidFromDate { get; set; }
    public DateOnly? ValidToDate { get; set; }
    public string RecordDisposition { get; set; } = null!;
    public string ChangeKind { get; set; } = null!;
}

internal sealed class InstrumentMasterRevisionRow : IRevisionRow
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
    public Guid InstrumentId { get; set; }
    public string Provider { get; set; } = null!;
    public DateOnly EffectiveFromDate { get; set; }
    public DateOnly? EffectiveToDate { get; set; }
    public string Name { get; set; } = null!;
    public string ExchangeCode { get; set; } = null!;
    public string MarketSegment { get; set; } = null!;
    public string SecurityType { get; set; } = null!;
    public long? TradingUnit { get; set; }
    public string Currency { get; set; } = null!;
    public DateOnly? ListingDate { get; set; }
    public DateOnly? DelistingDate { get; set; }
    public string ListingStatus { get; set; } = null!;
    public string ScanEligibility { get; set; } = null!;
    public string? ExclusionReason { get; set; }
    public string ChangeKind { get; set; } = null!;
}

internal sealed class MarketCalendarVersionRow
{
    public Guid Id { get; set; }
    public string MarketCode { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string VersionName { get; set; } = null!;
    public string TimeZoneId { get; set; } = null!;
    public string AlgorithmVersion { get; set; } = null!;
    public string ContentSha256 { get; set; } = null!;
    public Guid? SourceArtifactId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed class MarketCalendarDayRow
{
    public DateOnly TradingDate { get; set; }
    public string SessionStatus { get; set; } = null!;
    public string? Reason { get; set; }
    public Guid MarketCalendarVersionId { get; set; }
    public Guid? SourceArtifactId { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed class SourceArtifactRow
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = null!;
    public string DatasetKind { get; set; } = null!;
    public string? SourceUri { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public DateTimeOffset? SourcePublishedAtUtc { get; set; }
    public DateTimeOffset? AvailableAtUtc { get; set; }
    public string AvailabilityStatus { get; set; } = null!;
    public string ContentSha256 { get; set; } = null!;
    public string? MediaType { get; set; }
    public string RetentionStatus { get; set; } = null!;
    public byte[]? ContentBlob { get; set; }
    public string? ExternalLocation { get; set; }
    public string? ContentEncoding { get; set; }
    public string MetadataJson { get; set; } = null!;
}

internal sealed class DataUpdateRunRow
{
    public Guid Id { get; set; }
    public string DatasetKind { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long? RequestedCount { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public long UnchangedCount { get; set; }
    public string ConfigurationSnapshotJson { get; set; } = null!;
    public string ConfigurationSha256 { get; set; } = null!;
    public string? Summary { get; set; }
}

internal sealed class DataUpdateItemRow
{
    public Guid Id { get; set; }
    public Guid DataUpdateRunId { get; set; }
    public Guid? SourceArtifactId { get; set; }
    public Guid? InstrumentId { get; set; }
    public string ItemKey { get; set; } = null!;
    public long ItemAttemptNo { get; set; }
    public string Outcome { get; set; } = null!;
    public string? ResolvedEntityType { get; set; }
    public Guid? ResolvedRevisionId { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}

internal sealed class DataUpdateFailureRow
{
    public Guid Id { get; set; }
    public Guid DataUpdateRunId { get; set; }
    public Guid? DataUpdateItemId { get; set; }
    public Guid? InstrumentId { get; set; }
    public string? ItemKey { get; set; }
    public string ErrorKind { get; set; } = null!;
    public string Message { get; set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; set; }
}
