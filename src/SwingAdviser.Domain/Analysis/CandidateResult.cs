using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Analysis;

public sealed record CandidateScoreComponent(
    string Key,
    bool Matched,
    string RawValueJson,
    decimal Weight,
    decimal AwardedScore,
    string Reason,
    int Ordinal);

public sealed record CandidateResult
{
    private CandidateResult(
        CandidateResultId id,
        Guid technicalAnalysisResultId,
        int score,
        ConfidenceLevel confidence,
        string primaryReason,
        IReadOnlyList<CandidateScoreComponent> components,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TechnicalAnalysisResultId = technicalAnalysisResultId;
        Score = score;
        Confidence = confidence;
        PrimaryReason = primaryReason;
        Components = components;
        CreatedAtUtc = createdAtUtc;
    }

    public CandidateResultId Id { get; }
    public Guid TechnicalAnalysisResultId { get; }
    public int Score { get; }
    public ConfidenceLevel Confidence { get; }
    public string PrimaryReason { get; }
    public IReadOnlyList<CandidateScoreComponent> Components { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public static CandidateResult Create(
        CandidateResultId id,
        TechnicalAnalysisResult technicalResult,
        int score,
        ConfidenceLevel confidence,
        string primaryReason,
        IReadOnlyList<CandidateScoreComponent> components,
        DateTimeOffset createdAtUtc)
    {
        if (technicalResult.Outcome != TechnicalAnalysisOutcome.Candidate ||
            technicalResult.SignalPurpose != SignalPurpose.Entry)
        {
            throw new DomainException("Only a qualified Entry technical result can become a candidate.");
        }

        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Candidate ID cannot be empty.", nameof(id));
        }

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Candidate score must be between 0 and 100.");
        }

        if (components.GroupBy(x => x.Key, StringComparer.Ordinal).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Candidate score component keys must be unique.", nameof(components));
        }

        return new CandidateResult(
            id,
            technicalResult.Id,
            score,
            confidence,
            DomainGuard.Required(primaryReason, nameof(primaryReason)),
            components.OrderBy(x => x.Ordinal).ToArray(),
            DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc)));
    }
}
