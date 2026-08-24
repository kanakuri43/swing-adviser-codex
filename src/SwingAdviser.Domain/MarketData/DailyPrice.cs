using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.MarketData;

public sealed record DailyPriceRevision
{
    public DailyPriceRevision(
        Guid dailyPriceId,
        InstrumentId instrumentId,
        DateOnly barDate,
        string provider,
        string providerSymbol,
        PositivePrice open,
        PositivePrice high,
        PositivePrice low,
        PositivePrice close,
        long volume,
        decimal? providerAdjustedClose,
        CurrencyCode currency,
        BarStatus status,
        SourceRevisionMetadata audit)
    {
        if (dailyPriceId == Guid.Empty)
        {
            throw new ArgumentException("Daily price ID cannot be empty.", nameof(dailyPriceId));
        }

        if (volume < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume cannot be negative.");
        }

        if (providerAdjustedClose <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(providerAdjustedClose), "A supplied adjusted close must be positive.");
        }

        if (high.Value < open.Value || high.Value < low.Value || high.Value < close.Value)
        {
            throw new DomainException("High must be greater than or equal to open, low, and close.");
        }

        if (low.Value > open.Value || low.Value > high.Value || low.Value > close.Value)
        {
            throw new DomainException("Low must be less than or equal to open, high, and close.");
        }

        DailyPriceId = dailyPriceId;
        InstrumentId = instrumentId;
        BarDate = barDate;
        Provider = DomainGuard.Required(provider, nameof(provider));
        ProviderSymbol = DomainGuard.Required(providerSymbol, nameof(providerSymbol));
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        ProviderAdjustedClose = providerAdjustedClose;
        Currency = currency;
        Status = status;
        Audit = audit;
    }

    public Guid DailyPriceId { get; }
    public InstrumentId InstrumentId { get; }
    public DateOnly BarDate { get; }
    public string Provider { get; }
    public string ProviderSymbol { get; }
    public PositivePrice Open { get; }
    public PositivePrice High { get; }
    public PositivePrice Low { get; }
    public PositivePrice Close { get; }
    public long Volume { get; }

    // Stored for diagnostics only. Analysis input builders must use point-in-time corporate-action adjustment.
    public decimal? ProviderAdjustedClose { get; }
    public CurrencyCode Currency { get; }
    public BarStatus Status { get; }
    public SourceRevisionMetadata Audit { get; }
}
