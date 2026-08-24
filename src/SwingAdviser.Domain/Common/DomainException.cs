namespace SwingAdviser.Domain.Common;

public sealed class DomainException : InvalidOperationException
{
    public DomainException(string message)
        : base(message)
    {
    }
}

internal static class DomainGuard
{
    public static string Required(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    public static decimal Positive(decimal value, string parameterName)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value must be positive.");
        }

        return value;
    }

    public static int Positive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value must be positive.");
        }

        return value;
    }

    public static long Positive(long value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value must be positive.");
        }

        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The instant must be normalized to UTC.", parameterName);
        }

        return value;
    }
}
