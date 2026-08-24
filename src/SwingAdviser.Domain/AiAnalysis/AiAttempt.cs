using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.AiAnalysis;

public sealed record AiAttemptEvent(
    AiAttemptStatus? From,
    AiAttemptStatus To,
    DateTimeOffset OccurredAtUtc,
    string? Reason,
    int Ordinal);

public sealed class AiAttempt
{
    private readonly List<AiAttemptEvent> _events = [];

    public AiAttempt(
        AiAttemptId id,
        AiCheckJobId jobId,
        int attemptNumber,
        AiAttemptKind kind,
        AiRequestOrigin requestOrigin,
        int priorityAtQueue,
        int timeoutSeconds,
        DateTimeOffset queuedAtUtc)
    {
        if (id.Value == Guid.Empty || jobId.Value == Guid.Empty)
        {
            throw new ArgumentException("AI attempt and job IDs cannot be empty.");
        }

        Id = id;
        JobId = jobId;
        AttemptNumber = DomainGuard.Positive(attemptNumber, nameof(attemptNumber));
        Kind = kind;
        RequestOrigin = requestOrigin;
        PriorityAtQueue = priorityAtQueue;
        TimeoutSeconds = DomainGuard.Positive(timeoutSeconds, nameof(timeoutSeconds));
        Status = AiAttemptStatus.Queued;
        _events.Add(new AiAttemptEvent(null, Status, DomainGuard.Utc(queuedAtUtc, nameof(queuedAtUtc)), null, 1));
    }

    public AiAttemptId Id { get; }
    public AiCheckJobId JobId { get; }
    public int AttemptNumber { get; }
    public AiAttemptKind Kind { get; }
    public AiRequestOrigin RequestOrigin { get; }
    public int PriorityAtQueue { get; }
    public int TimeoutSeconds { get; }
    public AiAttemptStatus Status { get; private set; }
    public IReadOnlyList<AiAttemptEvent> Events => _events.AsReadOnly();

    public void Start(DateTimeOffset occurredAtUtc) => Transition(AiAttemptStatus.Running, occurredAtUtc, null);
    public void Succeed(DateTimeOffset occurredAtUtc) => Transition(AiAttemptStatus.Succeeded, occurredAtUtc, null);
    public void Fail(DateTimeOffset occurredAtUtc, string reason) => Transition(AiAttemptStatus.Failed, occurredAtUtc, reason);
    public void TimeOut(DateTimeOffset occurredAtUtc, string reason) => Transition(AiAttemptStatus.TimedOut, occurredAtUtc, reason);
    public void MarkInsufficientInformation(DateTimeOffset occurredAtUtc, string reason) =>
        Transition(AiAttemptStatus.InsufficientInformation, occurredAtUtc, reason);
    public void Cancel(DateTimeOffset occurredAtUtc, string reason) => Transition(AiAttemptStatus.Cancelled, occurredAtUtc, reason);

    private void Transition(AiAttemptStatus to, DateTimeOffset occurredAtUtc, string? reason)
    {
        var allowed = Status switch
        {
            AiAttemptStatus.Queued => to is AiAttemptStatus.Running or AiAttemptStatus.Cancelled,
            AiAttemptStatus.Running => to is AiAttemptStatus.Succeeded or AiAttemptStatus.Failed or
                AiAttemptStatus.TimedOut or AiAttemptStatus.InsufficientInformation or AiAttemptStatus.Cancelled,
            _ => false,
        };

        if (!allowed)
        {
            throw new DomainException($"Invalid AI attempt transition: {Status} -> {to}.");
        }

        if (to is AiAttemptStatus.Failed or AiAttemptStatus.TimedOut or
            AiAttemptStatus.InsufficientInformation or AiAttemptStatus.Cancelled)
        {
            DomainGuard.Required(reason, nameof(reason));
        }

        var atUtc = DomainGuard.Utc(occurredAtUtc, nameof(occurredAtUtc));
        if (atUtc < _events[^1].OccurredAtUtc)
        {
            throw new DomainException("AI attempt events cannot move backwards in time.");
        }

        _events.Add(new AiAttemptEvent(Status, to, atUtc, reason?.Trim(), _events.Count + 1));
        Status = to;
    }
}
