using System.Text.Json;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Analysis;

public sealed record CandidateScoreComponent
{
    public CandidateScoreComponent(
        string key,
        bool matched,
        string rawValueJson,
        decimal weight,
        decimal awardedScore,
        string reason,
        int ordinal)
    {
        if (weight is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "A score component weight must be between 0 and 100.");
        }

        if (awardedScore < 0m || awardedScore > weight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(awardedScore),
                "Awarded score must be between zero and the component weight.");
        }

        if (!matched && awardedScore != 0m)
        {
            throw new ArgumentException("An unmatched component cannot award score.", nameof(awardedScore));
        }

        Key = DomainGuard.Required(key, nameof(key));
        RawValueJson = DomainGuard.Required(rawValueJson, nameof(rawValueJson));
        using var _ = JsonDocument.Parse(RawValueJson);
        Matched = matched;
        Weight = weight;
        AwardedScore = awardedScore;
        Reason = DomainGuard.Required(reason, nameof(reason));
        Ordinal = DomainGuard.Positive(ordinal, nameof(ordinal));
    }

    public string Key { get; }
    public bool Matched { get; }
    public string RawValueJson { get; }
    public decimal Weight { get; }
    public decimal AwardedScore { get; }
    public string Reason { get; }
    public int Ordinal { get; }
}

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
        Components = Array.AsReadOnly(components.ToArray());
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
        ArgumentNullException.ThrowIfNull(technicalResult);
        ArgumentNullException.ThrowIfNull(components);
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

        if (!Enum.IsDefined(confidence))
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        if (components.GroupBy(x => x.Key, StringComparer.Ordinal).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Candidate score component keys must be unique.", nameof(components));
        }

        if (components.GroupBy(x => x.Ordinal).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Candidate score component ordinals must be unique.", nameof(components));
        }

        if (components.Sum(component => component.Weight) != 100m)
        {
            throw new ArgumentException("Candidate score component weights must total exactly 100.", nameof(components));
        }

        var roundedComponentTotal = decimal.ToInt32(decimal.Round(
            components.Sum(component => component.AwardedScore),
            0,
            MidpointRounding.AwayFromZero));
        if (roundedComponentTotal != score)
        {
            throw new ArgumentException(
                "Candidate score must equal the once-rounded sum of its component scores.",
                nameof(score));
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
