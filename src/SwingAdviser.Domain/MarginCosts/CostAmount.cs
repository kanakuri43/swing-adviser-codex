using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.MarginCosts;

public readonly record struct CostAmount
{
    private CostAmount(AmountStatus status, decimal? amount, CurrencyCode? currency)
    {
        Status = status;
        Amount = amount;
        Currency = currency;
    }

    public AmountStatus Status { get; }
    public decimal? Amount { get; }
    public CurrencyCode? Currency { get; }
    public bool IsResolved => Status is AmountStatus.KnownAmount or AmountStatus.KnownZero or AmountStatus.NotOccurred or AmountStatus.NotApplicable;

    public static CostAmount Known(decimal amount, CurrencyCode currency)
    {
        if (amount == 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Use KnownZero to represent a confirmed zero amount.");
        }

        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Cost magnitude is non-negative; direction carries the sign.");
        }

        return new(AmountStatus.KnownAmount, amount, currency);
    }

    public static CostAmount KnownZero(CurrencyCode currency) => new(AmountStatus.KnownZero, 0m, currency);
    public static CostAmount NotOccurred() => new(AmountStatus.NotOccurred, null, null);
    public static CostAmount Unpublished() => new(AmountStatus.Unpublished, null, null);
    public static CostAmount FetchFailed() => new(AmountStatus.FetchFailed, null, null);
    public static CostAmount Unknown() => new(AmountStatus.Unknown, null, null);
    public static CostAmount NotApplicable() => new(AmountStatus.NotApplicable, null, null);
}
