using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.AiAnalysis;

public sealed record AiCheckJob
{
    private AiCheckJob(
        AiCheckJobId id,
        CandidateResultId candidateResultId,
        AiRequestOrigin requestOrigin,
        int priority,
        PositionSide candidateSide,
        DateOnly evaluationBarDate,
        string normalizedInputJson,
        Sha256Hash inputHash,
        Sha256Hash technicalManifestHash,
        Sha256Hash strategySnapshotHash,
        Guid promptTemplateSnapshotId,
        Guid aiProfileSnapshotId,
        int? automaticSelectionRank,
        string? automaticSelectionPolicyVersion,
        DateTimeOffset requestedAtUtc)
    {
        Id = id;
        CandidateResultId = candidateResultId;
        RequestOrigin = requestOrigin;
        Priority = priority;
        CandidateSide = candidateSide;
        EvaluationBarDate = evaluationBarDate;
        NormalizedInputJson = normalizedInputJson;
        InputHash = inputHash;
        TechnicalManifestHash = technicalManifestHash;
        StrategySnapshotHash = strategySnapshotHash;
        PromptTemplateSnapshotId = promptTemplateSnapshotId;
        AiProfileSnapshotId = aiProfileSnapshotId;
        AutomaticSelectionRank = automaticSelectionRank;
        AutomaticSelectionPolicyVersion = automaticSelectionPolicyVersion;
        RequestedAtUtc = requestedAtUtc;
    }

    public AiCheckJobId Id { get; }
    public CandidateResultId CandidateResultId { get; }
    public AiRequestOrigin RequestOrigin { get; }
    public int Priority { get; }
    public PositionSide CandidateSide { get; }
    public DateOnly EvaluationBarDate { get; }
    public string NormalizedInputJson { get; }
    public Sha256Hash InputHash { get; }
    public Sha256Hash TechnicalManifestHash { get; }
    public Sha256Hash StrategySnapshotHash { get; }
    public Guid PromptTemplateSnapshotId { get; }
    public Guid AiProfileSnapshotId { get; }
    public int? AutomaticSelectionRank { get; }
    public string? AutomaticSelectionPolicyVersion { get; }
    public DateTimeOffset RequestedAtUtc { get; }

    public static AiCheckJob UserRequested(
        AiCheckJobId id,
        CandidateResultId candidateResultId,
        int priority,
        PositionSide candidateSide,
        DateOnly evaluationBarDate,
        string normalizedInputJson,
        Sha256Hash inputHash,
        Sha256Hash technicalManifestHash,
        Sha256Hash strategySnapshotHash,
        Guid promptTemplateSnapshotId,
        Guid aiProfileSnapshotId,
        DateTimeOffset requestedAtUtc) =>
        Create(id, candidateResultId, AiRequestOrigin.User, priority, candidateSide, evaluationBarDate,
            normalizedInputJson, inputHash, technicalManifestHash, strategySnapshotHash,
            promptTemplateSnapshotId, aiProfileSnapshotId, null, null, requestedAtUtc);

    public static AiCheckJob AutomaticallySelected(
        AiCheckJobId id,
        CandidateResultId candidateResultId,
        int priority,
        PositionSide candidateSide,
        DateOnly evaluationBarDate,
        string normalizedInputJson,
        Sha256Hash inputHash,
        Sha256Hash technicalManifestHash,
        Sha256Hash strategySnapshotHash,
        Guid promptTemplateSnapshotId,
        Guid aiProfileSnapshotId,
        int automaticSelectionRank,
        string automaticSelectionPolicyVersion,
        DateTimeOffset requestedAtUtc) =>
        Create(id, candidateResultId, AiRequestOrigin.Automatic, priority, candidateSide, evaluationBarDate,
            normalizedInputJson, inputHash, technicalManifestHash, strategySnapshotHash,
            promptTemplateSnapshotId, aiProfileSnapshotId, automaticSelectionRank,
            automaticSelectionPolicyVersion, requestedAtUtc);

    private static AiCheckJob Create(
        AiCheckJobId id,
        CandidateResultId candidateResultId,
        AiRequestOrigin origin,
        int priority,
        PositionSide candidateSide,
        DateOnly evaluationBarDate,
        string normalizedInputJson,
        Sha256Hash inputHash,
        Sha256Hash technicalManifestHash,
        Sha256Hash strategySnapshotHash,
        Guid promptTemplateSnapshotId,
        Guid aiProfileSnapshotId,
        int? automaticSelectionRank,
        string? automaticSelectionPolicyVersion,
        DateTimeOffset requestedAtUtc)
    {
        if (id.Value == Guid.Empty || candidateResultId.Value == Guid.Empty ||
            promptTemplateSnapshotId == Guid.Empty || aiProfileSnapshotId == Guid.Empty)
        {
            throw new ArgumentException("AI job and referenced IDs cannot be empty.");
        }

        if (origin == AiRequestOrigin.Automatic &&
            (automaticSelectionRank is null or <= 0 || string.IsNullOrWhiteSpace(automaticSelectionPolicyVersion)))
        {
            throw new ArgumentException("Automatic jobs require a positive selection rank and frozen policy version.");
        }

        if (origin == AiRequestOrigin.User &&
            (automaticSelectionRank is not null || automaticSelectionPolicyVersion is not null))
        {
            throw new ArgumentException("User jobs cannot carry automatic selection metadata.");
        }

        return new AiCheckJob(
            id,
            candidateResultId,
            origin,
            priority,
            candidateSide,
            evaluationBarDate,
            DomainGuard.Required(normalizedInputJson, nameof(normalizedInputJson)),
            inputHash,
            technicalManifestHash,
            strategySnapshotHash,
            promptTemplateSnapshotId,
            aiProfileSnapshotId,
            automaticSelectionRank,
            automaticSelectionPolicyVersion?.Trim(),
            DomainGuard.Utc(requestedAtUtc, nameof(requestedAtUtc)));
    }
}
