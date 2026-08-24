using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.MarketData;

public sealed record CorporateActionRevision
{
    private CorporateActionRevision(
        Guid corporateActionId,
        InstrumentId instrumentId,
        CorporateActionType type,
        CorporateActionStatus status,
        DateOnly effectiveDate,
        DateTimeOffset? announcedAtUtc,
        long? ratioNumerator,
        long? ratioDenominator,
        decimal? cashAmountPerShare,
        CurrencyCode? currency,
        PointInTimeStatus pointInTimeStatus,
        SourceRevisionMetadata audit)
    {
        CorporateActionId = corporateActionId;
        InstrumentId = instrumentId;
        Type = type;
        Status = status;
        EffectiveDate = effectiveDate;
        AnnouncedAtUtc = announcedAtUtc;
        RatioNumerator = ratioNumerator;
        RatioDenominator = ratioDenominator;
        CashAmountPerShare = cashAmountPerShare;
        Currency = currency;
        PointInTimeStatus = pointInTimeStatus;
        Audit = audit;
    }

    public Guid CorporateActionId { get; }
    public InstrumentId InstrumentId { get; }
    public CorporateActionType Type { get; }
    public CorporateActionStatus Status { get; }
    public DateOnly EffectiveDate { get; }
    public DateTimeOffset? AnnouncedAtUtc { get; }
    public long? RatioNumerator { get; }
    public long? RatioDenominator { get; }
    public decimal? CashAmountPerShare { get; }
    public CurrencyCode? Currency { get; }
    public PointInTimeStatus PointInTimeStatus { get; }
    public SourceRevisionMetadata Audit { get; }

    public static CorporateActionRevision SplitOrConsolidation(
        Guid corporateActionId,
        InstrumentId instrumentId,
        CorporateActionType type,
        CorporateActionStatus status,
        DateOnly effectiveDate,
        DateTimeOffset? announcedAtUtc,
        long ratioNumerator,
        long ratioDenominator,
        PointInTimeStatus pointInTimeStatus,
        SourceRevisionMetadata audit)
    {
        if (type is not (CorporateActionType.Split or CorporateActionType.Consolidation))
        {
            throw new ArgumentException("The action type must be Split or Consolidation.", nameof(type));
        }

        DomainGuard.Positive(ratioNumerator, nameof(ratioNumerator));
        DomainGuard.Positive(ratioDenominator, nameof(ratioDenominator));
        ValidateIdentityAndAnnouncement(corporateActionId, announcedAtUtc);
        return new(corporateActionId, instrumentId, type, status, effectiveDate, announcedAtUtc,
            ratioNumerator, ratioDenominator, null, null, pointInTimeStatus, audit);
    }

    public static CorporateActionRevision CashDividend(
        Guid corporateActionId,
        InstrumentId instrumentId,
        CorporateActionStatus status,
        DateOnly effectiveDate,
        DateTimeOffset? announcedAtUtc,
        decimal cashAmountPerShare,
        CurrencyCode currency,
        PointInTimeStatus pointInTimeStatus,
        SourceRevisionMetadata audit)
    {
        ValidateIdentityAndAnnouncement(corporateActionId, announcedAtUtc);
        if (cashAmountPerShare < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(cashAmountPerShare));
        }

        return new(corporateActionId, instrumentId, CorporateActionType.CashDividend, status,
            effectiveDate, announcedAtUtc, null, null, cashAmountPerShare, currency, pointInTimeStatus, audit);
    }

    public static CorporateActionRevision Unsupported(
        Guid corporateActionId,
        InstrumentId instrumentId,
        CorporateActionStatus status,
        DateOnly effectiveDate,
        DateTimeOffset? announcedAtUtc,
        PointInTimeStatus pointInTimeStatus,
        SourceRevisionMetadata audit)
    {
        ValidateIdentityAndAnnouncement(corporateActionId, announcedAtUtc);
        return new(corporateActionId, instrumentId, CorporateActionType.Unsupported, status,
            effectiveDate, announcedAtUtc, null, null, null, null, pointInTimeStatus, audit);
    }

    public bool IsUsableAt(DateOnly evaluationBarDate, DateTimeOffset analyzedAtUtc) =>
        Status != CorporateActionStatus.Cancelled &&
        EffectiveDate <= evaluationBarDate &&
        Audit.Availability.IsAvailableBy(analyzedAtUtc);

    private static void ValidateIdentityAndAnnouncement(Guid corporateActionId, DateTimeOffset? announcedAtUtc)
    {
        if (corporateActionId == Guid.Empty)
        {
            throw new ArgumentException("Corporate action ID cannot be empty.", nameof(corporateActionId));
        }

        if (announcedAtUtc is not null)
        {
            DomainGuard.Utc(announcedAtUtc.Value, nameof(announcedAtUtc));
        }
    }
}
