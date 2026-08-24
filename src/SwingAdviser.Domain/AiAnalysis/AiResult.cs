using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.AiAnalysis;

public sealed record AiResultSource(
    Uri Url,
    string? Title,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset RetrievedAtUtc,
    int Ordinal);

public sealed record AiResult
{
    private AiResult(
        Guid id,
        AiAttemptId attemptId,
        string schemaVersion,
        string parserVersion,
        AiVerdict? verdict,
        ConfidenceLevel? confidence,
        string? summary,
        string? technicalView,
        string? fundamentalView,
        IReadOnlyList<string> positiveFactors,
        IReadOnlyList<string> riskFactors,
        IReadOnlyList<string> invalidationConditions,
        DateTimeOffset checkedAtUtc,
        string structuredResultJson,
        Sha256Hash structuredResultHash,
        IReadOnlyList<AiResultSource> sources)
    {
        Id = id;
        AttemptId = attemptId;
        SchemaVersion = schemaVersion;
        ParserVersion = parserVersion;
        Verdict = verdict;
        Confidence = confidence;
        Summary = summary;
        TechnicalView = technicalView;
        FundamentalView = fundamentalView;
        PositiveFactors = positiveFactors;
        RiskFactors = riskFactors;
        InvalidationConditions = invalidationConditions;
        CheckedAtUtc = checkedAtUtc;
        StructuredResultJson = structuredResultJson;
        StructuredResultHash = structuredResultHash;
        Sources = sources;
    }

    public Guid Id { get; }
    public AiAttemptId AttemptId { get; }
    public string SchemaVersion { get; }
    public string ParserVersion { get; }
    public AiVerdict? Verdict { get; }
    public ConfidenceLevel? Confidence { get; }
    public string? Summary { get; }
    public string? TechnicalView { get; }
    public string? FundamentalView { get; }
    public IReadOnlyList<string> PositiveFactors { get; }
    public IReadOnlyList<string> RiskFactors { get; }
    public IReadOnlyList<string> InvalidationConditions { get; }
    public DateTimeOffset CheckedAtUtc { get; }
    public string StructuredResultJson { get; }
    public Sha256Hash StructuredResultHash { get; }
    public IReadOnlyList<AiResultSource> Sources { get; }
    public bool HasSufficientInformation => Verdict is not null;

    public static AiResult Succeeded(
        Guid id,
        AiAttempt attempt,
        string schemaVersion,
        string parserVersion,
        AiVerdict verdict,
        ConfidenceLevel confidence,
        string summary,
        string? technicalView,
        string? fundamentalView,
        IReadOnlyList<string> positiveFactors,
        IReadOnlyList<string> riskFactors,
        IReadOnlyList<string> invalidationConditions,
        DateTimeOffset checkedAtUtc,
        string structuredResultJson,
        Sha256Hash structuredResultHash,
        IReadOnlyList<AiResultSource> sources)
    {
        if (attempt.Status != AiAttemptStatus.Succeeded)
        {
            throw new DomainException("A verdict can only be attached to a succeeded AI attempt.");
        }

        return Create(id, attempt.Id, schemaVersion, parserVersion, verdict, confidence, summary,
            technicalView, fundamentalView, positiveFactors, riskFactors, invalidationConditions,
            checkedAtUtc, structuredResultJson, structuredResultHash, sources);
    }

    public static AiResult InsufficientInformation(
        Guid id,
        AiAttempt attempt,
        string schemaVersion,
        string parserVersion,
        string? summary,
        DateTimeOffset checkedAtUtc,
        string structuredResultJson,
        Sha256Hash structuredResultHash,
        IReadOnlyList<AiResultSource> sources)
    {
        if (attempt.Status != AiAttemptStatus.InsufficientInformation)
        {
            throw new DomainException("An insufficient-information result requires the matching terminal attempt state.");
        }

        return Create(id, attempt.Id, schemaVersion, parserVersion, null, null, summary,
            null, null, [], [], [], checkedAtUtc, structuredResultJson, structuredResultHash, sources);
    }

    private static AiResult Create(
        Guid id,
        AiAttemptId attemptId,
        string schemaVersion,
        string parserVersion,
        AiVerdict? verdict,
        ConfidenceLevel? confidence,
        string? summary,
        string? technicalView,
        string? fundamentalView,
        IReadOnlyList<string> positiveFactors,
        IReadOnlyList<string> riskFactors,
        IReadOnlyList<string> invalidationConditions,
        DateTimeOffset checkedAtUtc,
        string structuredResultJson,
        Sha256Hash structuredResultHash,
        IReadOnlyList<AiResultSource> sources)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("AI result ID cannot be empty.", nameof(id));
        }

        if (sources.Any(x => !x.Url.IsAbsoluteUri) || sources.GroupBy(x => x.Ordinal).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("AI sources require absolute URLs and unique ordinals.", nameof(sources));
        }

        return new AiResult(
            id,
            attemptId,
            DomainGuard.Required(schemaVersion, nameof(schemaVersion)),
            DomainGuard.Required(parserVersion, nameof(parserVersion)),
            verdict,
            confidence,
            summary?.Trim(),
            technicalView?.Trim(),
            fundamentalView?.Trim(),
            positiveFactors.ToArray(),
            riskFactors.ToArray(),
            invalidationConditions.ToArray(),
            DomainGuard.Utc(checkedAtUtc, nameof(checkedAtUtc)),
            DomainGuard.Required(structuredResultJson, nameof(structuredResultJson)),
            structuredResultHash,
            sources.OrderBy(x => x.Ordinal).ToArray());
    }
}
