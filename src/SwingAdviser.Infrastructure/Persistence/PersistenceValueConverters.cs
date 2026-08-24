using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SwingAdviser.Infrastructure.Persistence;

internal static class PersistenceValueFormats
{
    public const string UtcInstantFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    public const string MarketDateFormat = "yyyy-MM-dd";

    public static string FormatGuid(Guid value) => value.ToString("D").ToLowerInvariant();

    public static Guid ParseGuid(string value)
    {
        if (!Guid.TryParseExact(value, "D", out var parsed) ||
            !string.Equals(value, FormatGuid(parsed), StringComparison.Ordinal))
        {
            throw new FormatException($"'{value}' is not a canonical lowercase UUID.");
        }

        return parsed;
    }

    public static string FormatInstant(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(UtcInstantFormat, CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseInstant(string value)
    {
        if (!DateTimeOffset.TryParseExact(
                value,
                UtcInstantFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) ||
            !string.Equals(value, FormatInstant(parsed), StringComparison.Ordinal))
        {
            throw new FormatException($"'{value}' is not a canonical UTC instant.");
        }

        return parsed;
    }

    public static string FormatMarketDate(DateOnly value) =>
        value.ToString(MarketDateFormat, CultureInfo.InvariantCulture);

    public static DateOnly ParseMarketDate(string value)
    {
        if (!DateOnly.TryParseExact(
                value,
                MarketDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed) ||
            !string.Equals(value, FormatMarketDate(parsed), StringComparison.Ordinal))
        {
            throw new FormatException($"'{value}' is not a canonical market date.");
        }

        return parsed;
    }

    public static string FormatDecimal(decimal value)
    {
        var normalized = value == decimal.Zero ? decimal.Zero : value;
        return normalized.ToString("0.############################", CultureInfo.InvariantCulture);
    }

    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            value[0] == '+' ||
            value.Contains("e", StringComparison.OrdinalIgnoreCase) ||
            !decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            !string.Equals(value, FormatDecimal(parsed), StringComparison.Ordinal))
        {
            throw new FormatException($"'{value}' is not a canonical decimal.");
        }

        return parsed;
    }
}

internal sealed class CanonicalGuidConverter()
    : ValueConverter<Guid, string>(
        value => PersistenceValueFormats.FormatGuid(value),
        value => PersistenceValueFormats.ParseGuid(value));

internal sealed class UtcInstantConverter()
    : ValueConverter<DateTimeOffset, string>(
        value => PersistenceValueFormats.FormatInstant(value),
        value => PersistenceValueFormats.ParseInstant(value));

internal sealed class MarketDateConverter()
    : ValueConverter<DateOnly, string>(
        value => PersistenceValueFormats.FormatMarketDate(value),
        value => PersistenceValueFormats.ParseMarketDate(value));

internal sealed class CanonicalDecimalConverter()
    : ValueConverter<decimal, string>(
        value => PersistenceValueFormats.FormatDecimal(value),
        value => PersistenceValueFormats.ParseDecimal(value));
