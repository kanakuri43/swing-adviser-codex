namespace SwingAdviser.Domain.Common;

public enum AvailabilityStatus
{
    Known,
    Estimated,
    Unknown,
}

public readonly record struct Availability
{
    private Availability(AvailabilityStatus status, DateTimeOffset? availableAtUtc)
    {
        if (status == AvailabilityStatus.Unknown && availableAtUtc is not null)
        {
            throw new ArgumentException("Unknown availability cannot carry an invented timestamp.", nameof(availableAtUtc));
        }

        if (status != AvailabilityStatus.Unknown && availableAtUtc is null)
        {
            throw new ArgumentException("Known or estimated availability requires a timestamp.", nameof(availableAtUtc));
        }

        if (availableAtUtc is not null)
        {
            DomainGuard.Utc(availableAtUtc.Value, nameof(availableAtUtc));
        }

        Status = status;
        AvailableAtUtc = availableAtUtc;
    }

    public AvailabilityStatus Status { get; }
    public DateTimeOffset? AvailableAtUtc { get; }

    public static Availability Known(DateTimeOffset availableAtUtc) =>
        new(AvailabilityStatus.Known, availableAtUtc);

    public static Availability Estimated(DateTimeOffset availableAtUtc) =>
        new(AvailabilityStatus.Estimated, availableAtUtc);

    public static Availability Unknown() => new(AvailabilityStatus.Unknown, null);

    public bool IsAvailableBy(DateTimeOffset cutoffUtc) =>
        Status != AvailabilityStatus.Unknown && AvailableAtUtc <= cutoffUtc;
}
