using System.Globalization;
using System.Text.Json;

namespace SwingAdviser.Domain.Analysis;

internal static class AnalysisCanonicalJson
{
    public static void WriteDecimal(Utf8JsonWriter writer, string propertyName, decimal value) =>
        writer.WriteString(propertyName, FormatDecimal(value));

    public static string FormatDecimal(decimal value)
    {
        var normalized = value == decimal.Zero ? decimal.Zero : value;
        return normalized.ToString("0.############################", CultureInfo.InvariantCulture);
    }
}
