using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.MarketData;

public sealed record Instrument
{
    public Instrument(InstrumentId id, DateTimeOffset createdAtUtc)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Instrument ID cannot be empty.", nameof(id));
        }

        Id = id;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public InstrumentId Id { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}

public sealed record InstrumentIdentifierRevision
{
    public InstrumentIdentifierRevision(
        Guid logicalIdentifierId,
        InstrumentId instrumentId,
        string scheme,
        string value,
        DateOnly? validFrom,
        DateOnly? validTo,
        RecordDisposition disposition,
        SourceRevisionMetadata audit)
    {
        if (logicalIdentifierId == Guid.Empty)
        {
            throw new ArgumentException("Logical identifier ID cannot be empty.", nameof(logicalIdentifierId));
        }

        if (validFrom is not null && validTo is not null && validTo < validFrom)
        {
            throw new ArgumentException("Identifier validity end cannot precede its start.", nameof(validTo));
        }

        LogicalIdentifierId = logicalIdentifierId;
        InstrumentId = instrumentId;
        Scheme = DomainGuard.Required(scheme, nameof(scheme));
        Value = DomainGuard.Required(value, nameof(value));
        ValidFrom = validFrom;
        ValidTo = validTo;
        Disposition = disposition;
        Audit = audit;
    }

    public Guid LogicalIdentifierId { get; }
    public InstrumentId InstrumentId { get; }
    public string Scheme { get; }
    public string Value { get; }
    public DateOnly? ValidFrom { get; }
    public DateOnly? ValidTo { get; }
    public RecordDisposition Disposition { get; }
    public SourceRevisionMetadata Audit { get; }
}

public sealed record InstrumentMasterRevision
{
    public InstrumentMasterRevision(
        InstrumentId instrumentId,
        string provider,
        DateRange effectivePeriod,
        string name,
        string exchangeCode,
        string marketSegment,
        SecurityType securityType,
        long? tradingUnit,
        CurrencyCode currency,
        DateOnly? listingDate,
        DateOnly? delistingDate,
        ListingStatus listingStatus,
        ScanEligibility scanEligibility,
        string? exclusionReason,
        SourceRevisionMetadata audit)
    {
        if (tradingUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tradingUnit), "A known trading unit must be positive.");
        }

        if (delistingDate is not null && listingDate is not null && delistingDate < listingDate)
        {
            throw new ArgumentException("Delisting date cannot precede listing date.", nameof(delistingDate));
        }

        if (scanEligibility == ScanEligibility.Excluded && string.IsNullOrWhiteSpace(exclusionReason))
        {
            throw new ArgumentException("An excluded instrument requires a reason.", nameof(exclusionReason));
        }

        InstrumentId = instrumentId;
        Provider = DomainGuard.Required(provider, nameof(provider));
        EffectivePeriod = effectivePeriod;
        Name = DomainGuard.Required(name, nameof(name));
        ExchangeCode = DomainGuard.Required(exchangeCode, nameof(exchangeCode));
        MarketSegment = DomainGuard.Required(marketSegment, nameof(marketSegment));
        SecurityType = securityType;
        TradingUnit = tradingUnit;
        Currency = currency;
        ListingDate = listingDate;
        DelistingDate = delistingDate;
        ListingStatus = listingStatus;
        ScanEligibility = scanEligibility;
        ExclusionReason = exclusionReason?.Trim();
        Audit = audit;
    }

    public InstrumentId InstrumentId { get; }
    public string Provider { get; }
    public DateRange EffectivePeriod { get; }
    public string Name { get; }
    public string ExchangeCode { get; }
    public string MarketSegment { get; }
    public SecurityType SecurityType { get; }
    public long? TradingUnit { get; }
    public CurrencyCode Currency { get; }
    public DateOnly? ListingDate { get; }
    public DateOnly? DelistingDate { get; }
    public ListingStatus ListingStatus { get; }
    public ScanEligibility ScanEligibility { get; }
    public string? ExclusionReason { get; }
    public SourceRevisionMetadata Audit { get; }
}
