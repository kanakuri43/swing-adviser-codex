namespace SwingAdviser.Infrastructure.Persistence.Entities;

internal sealed class PromptTemplateSnapshotRow
{
    public Guid Id { get; set; }
    public string TemplateVersion { get; set; } = string.Empty;
    public string TemplateText { get; set; } = string.Empty;
    public string TemplateSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class AiProfileSnapshotRow
{
    public Guid Id { get; set; }
    public string ProfileName { get; set; } = string.Empty;
    public string ExecutableIdentity { get; set; } = string.Empty;
    public string? RequestedModel { get; set; }
    public int TimeoutSeconds { get; set; }
    public string ArgumentsJson { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public string ProfileSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class AiCheckJobRow
{
    public Guid Id { get; set; }
    public Guid CandidateResultId { get; set; }
    public string RequestOrigin { get; set; } = string.Empty;
    public long Priority { get; set; }
    public string CandidateSide { get; set; } = string.Empty;
    public DateOnly EvaluationBarDate { get; set; }
    public string InputSnapshotJson { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public string TechnicalManifestSha256 { get; set; } = string.Empty;
    public string StrategySnapshotSha256 { get; set; } = string.Empty;
    public Guid PromptTemplateSnapshotId { get; set; }
    public Guid AiProfileSnapshotId { get; set; }
    public long? AutomaticSelectionRank { get; set; }
    public string? SelectionPolicyVersion { get; set; }
    public string? AutomaticConfigurationJson { get; set; }
    public string? AutomaticConfigurationSha256 { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
}

internal sealed class AiJobRequestEventRow
{
    public Guid Id { get; set; }
    public Guid AiCheckJobId { get; set; }
    public string EventKind { get; set; } = string.Empty;
    public string RequestOrigin { get; set; } = string.Empty;
    public long RequestedPriority { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public long Ordinal { get; set; }
}

internal sealed class AiAttemptRow
{
    public Guid Id { get; set; }
    public Guid AiCheckJobId { get; set; }
    public long AttemptNo { get; set; }
    public string AttemptKind { get; set; } = string.Empty;
    public string RequestOrigin { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public long PriorityAtQueue { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset QueuedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CliVersion { get; set; }
    public string? ActualModel { get; set; }
    public int TimeoutSeconds { get; set; }
    public string ArgumentsJson { get; set; } = string.Empty;
    public int? ExitCode { get; set; }
    public string? ErrorKind { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SanitizedStderr { get; set; }
    public string? RawResponseSha256 { get; set; }
}

internal sealed class AiAttemptEventRow
{
    public Guid Id { get; set; }
    public Guid AiAttemptId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
    public long Ordinal { get; set; }
}

internal sealed class AiResultRow
{
    public Guid Id { get; set; }
    public Guid AiAttemptId { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string? Verdict { get; set; }
    public string? Confidence { get; set; }
    public string? Summary { get; set; }
    public string? TechnicalView { get; set; }
    public string? FundamentalView { get; set; }
    public string PositiveFactorsJson { get; set; } = string.Empty;
    public string RiskFactorsJson { get; set; } = string.Empty;
    public string InvalidationConditionsJson { get; set; } = string.Empty;
    public DateTimeOffset CheckedAtUtc { get; set; }
    public string StructuredResultJson { get; set; } = string.Empty;
    public string StructuredResultSha256 { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class AiResultSourceRow
{
    public Guid Id { get; set; }
    public Guid AiResultId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public long Ordinal { get; set; }
}
