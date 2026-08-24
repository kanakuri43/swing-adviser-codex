using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.MarketData;

public sealed record MarginEligibilityRevision(
    Guid LogicalRecordId,
    InstrumentId InstrumentId,
    DateRange EffectivePeriod,
    EligibilityStatus StandardizedMarginStatus,
    EligibilityStatus LoanStockStatus,
    OpenPermissionStatus LongOpenStatus,
    OpenPermissionStatus ShortOpenStatus,
    IReadOnlyList<string> RegulationCodes,
    string? Notes,
    SourceRevisionMetadata Audit);

public sealed record FundamentalSnapshot
{
    public FundamentalSnapshot(
        Guid logicalRecordId,
        InstrumentId instrumentId,
        DateOnly asOfDate,
        decimal? priceEarningsRatio,
        decimal? priceBookRatio,
        decimal? marketCapitalization,
        CurrencyCode? marketCapitalizationCurrency,
        IReadOnlySet<string> missingFields,
        SourceRevisionMetadata audit)
    {
        if (logicalRecordId == Guid.Empty)
        {
            throw new ArgumentException("Logical record ID cannot be empty.", nameof(logicalRecordId));
        }

        if ((marketCapitalization is null) != (marketCapitalizationCurrency is null))
        {
            throw new ArgumentException("Market capitalization and its currency must be supplied together.");
        }

        LogicalRecordId = logicalRecordId;
        InstrumentId = instrumentId;
        AsOfDate = asOfDate;
        PriceEarningsRatio = priceEarningsRatio;
        PriceBookRatio = priceBookRatio;
        MarketCapitalization = marketCapitalization;
        MarketCapitalizationCurrency = marketCapitalizationCurrency;
        MissingFields = new HashSet<string>(missingFields, StringComparer.Ordinal);
        Audit = audit;
    }

    public Guid LogicalRecordId { get; }
    public InstrumentId InstrumentId { get; }
    public DateOnly AsOfDate { get; }
    public decimal? PriceEarningsRatio { get; }
    public decimal? PriceBookRatio { get; }
    public decimal? MarketCapitalization { get; }
    public CurrencyCode? MarketCapitalizationCurrency { get; }
    public IReadOnlySet<string> MissingFields { get; }
    public SourceRevisionMetadata Audit { get; }
}
