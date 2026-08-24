using System.Globalization;
using System.Text.RegularExpressions;

namespace SwingAdviser.Domain.Common;

public readonly record struct CurrencyCode
{
    private static readonly Regex Pattern = new("^[A-Z]{3}$", RegexOptions.CultureInvariant);

    public CurrencyCode(string value)
    {
        value = DomainGuard.Required(value, nameof(value)).ToUpperInvariant();
        if (!Pattern.IsMatch(value))
        {
            throw new ArgumentException("Currency must be a three-letter ISO 4217 code.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static CurrencyCode Jpy { get; } = new("JPY");

    public override string ToString() => Value;
}

public readonly record struct Sha256Hash
{
    private static readonly Regex Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

    public Sha256Hash(string value)
    {
        value = DomainGuard.Required(value, nameof(value));
        if (!Pattern.IsMatch(value))
        {
            throw new ArgumentException("SHA-256 must be 64 lowercase hexadecimal characters.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct PositivePrice
{
    public PositivePrice(decimal value) => Value = DomainGuard.Positive(value, nameof(value));

    public decimal Value { get; }

    public string ToCanonicalString() => Value.ToString("0.############################", CultureInfo.InvariantCulture);
}

public readonly record struct WholeShareQuantity
{
    public WholeShareQuantity(long value) => Value = DomainGuard.Positive(value, nameof(value));

    public long Value { get; }
}

public readonly record struct DateRange
{
    public DateRange(DateOnly from, DateOnly? to)
    {
        if (to is not null && to < from)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(to));
        }

        From = from;
        To = to;
    }

    public DateOnly From { get; }
    public DateOnly? To { get; }

    public bool Contains(DateOnly date) => date >= From && (To is null || date <= To.Value);
}
