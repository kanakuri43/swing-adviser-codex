using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public sealed record UserConfirmedExecutionInput
{
    public UserConfirmedExecutionInput(
        DateTimeOffset executedAtUtc,
        PositivePrice price,
        WholeShareQuantity quantity,
        CurrencyCode currency,
        DateTimeOffset userConfirmedAtUtc,
        string? broker = null,
        string? externalReference = null,
        string? userNote = null)
    {
        ExecutedAtUtc = DomainGuard.Utc(executedAtUtc, nameof(executedAtUtc));
        Price = price;
        Quantity = quantity;
        Currency = currency;
        UserConfirmedAtUtc = DomainGuard.Utc(userConfirmedAtUtc, nameof(userConfirmedAtUtc));
        Broker = broker?.Trim();
        ExternalReference = externalReference?.Trim();
        UserNote = userNote?.Trim();
    }

    public DateTimeOffset ExecutedAtUtc { get; }
    public PositivePrice Price { get; }
    public WholeShareQuantity Quantity { get; }
    public CurrencyCode Currency { get; }
    public DateTimeOffset UserConfirmedAtUtc { get; }
    public string? Broker { get; }
    public string? ExternalReference { get; }
    public string? UserNote { get; }
}

public sealed record TradeExecutionRevision
{
    internal TradeExecutionRevision(
        TradeExecutionRevisionId id,
        TradeExecutionId tradeExecutionId,
        RevisionMetadata audit,
        UserConfirmedExecutionInput input,
        RecordDisposition disposition,
        ExecutionChangeKind changeKind,
        string? correctionReason)
    {
        if (id.Value == Guid.Empty || tradeExecutionId.Value == Guid.Empty)
        {
            throw new ArgumentException("Execution and revision IDs cannot be empty.");
        }

        if (id.Value != audit.Id)
        {
            throw new ArgumentException("The typed revision ID must match its audit metadata ID.", nameof(audit));
        }

        if (changeKind == ExecutionChangeKind.Initial && audit.RevisionNumber != 1)
        {
            throw new DomainException("Only revision 1 can be the initial execution revision.");
        }

        if ((audit.RevisionNumber > 1 || disposition == RecordDisposition.Voided) &&
            string.IsNullOrWhiteSpace(correctionReason))
        {
            throw new ArgumentException("Corrections and voids require a reason.", nameof(correctionReason));
        }

        if (changeKind == ExecutionChangeKind.Void && disposition != RecordDisposition.Voided)
        {
            throw new DomainException("A void revision must have Voided disposition.");
        }

        Id = id;
        TradeExecutionId = tradeExecutionId;
        Audit = audit;
        Input = input;
        Disposition = disposition;
        ChangeKind = changeKind;
        CorrectionReason = correctionReason?.Trim();
    }

    public TradeExecutionRevisionId Id { get; }
    public TradeExecutionId TradeExecutionId { get; }
    public RevisionMetadata Audit { get; }
    public UserConfirmedExecutionInput Input { get; }
    public RecordDisposition Disposition { get; }
    public ExecutionChangeKind ChangeKind { get; }
    public string? CorrectionReason { get; }
}

public sealed class TradeExecution
{
    private readonly List<TradeExecutionRevision> _revisions = [];

    private TradeExecution(
        TradeExecutionId id,
        PositionId positionId,
        ExecutionKind kind,
        CandidateResultId? candidateContextId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        PositionId = positionId;
        Kind = kind;
        Origin = ExecutionOrigin.UserConfirmed;
        CandidateContextId = candidateContextId;
        CreatedAtUtc = createdAtUtc;
    }

    public TradeExecutionId Id { get; }
    public PositionId PositionId { get; }
    public ExecutionKind Kind { get; }
    public ExecutionOrigin Origin { get; }

    // Context may prefill instrument/direction in the UI, but never execution values.
    public CandidateResultId? CandidateContextId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<TradeExecutionRevision> Revisions => _revisions.AsReadOnly();
    public TradeExecutionRevision CurrentRevision => _revisions[^1];

    internal static TradeExecution RegisterUserConfirmed(
        TradeExecutionId id,
        PositionId positionId,
        ExecutionKind kind,
        CandidateResultId? candidateContextId,
        UserConfirmedExecutionInput input,
        TradeExecutionRevisionId revisionId,
        RevisionMetadata revisionAudit,
        DateTimeOffset createdAtUtc)
    {
        if (id.Value == Guid.Empty || positionId.Value == Guid.Empty)
        {
            throw new ArgumentException("Execution and position IDs cannot be empty.");
        }

        if (revisionAudit.RevisionNumber != 1)
        {
            throw new ArgumentException("An initial execution requires revision number 1.", nameof(revisionAudit));
        }

        var execution = new TradeExecution(
            id,
            positionId,
            kind,
            candidateContextId,
            DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc)));
        execution._revisions.Add(new TradeExecutionRevision(
            revisionId,
            id,
            revisionAudit,
            input,
            RecordDisposition.Effective,
            ExecutionChangeKind.Initial,
            null));
        return execution;
    }

    public TradeExecutionRevision AppendCorrection(
        TradeExecutionRevisionId revisionId,
        RevisionMetadata audit,
        UserConfirmedExecutionInput correctedInput,
        string correctionReason)
    {
        EnsureNextRevision(audit);
        var revision = new TradeExecutionRevision(
            revisionId,
            Id,
            audit,
            correctedInput,
            RecordDisposition.Effective,
            ExecutionChangeKind.Correction,
            correctionReason);
        _revisions.Add(revision);
        return revision;
    }

    public TradeExecutionRevision AppendVoid(
        TradeExecutionRevisionId revisionId,
        RevisionMetadata audit,
        DateTimeOffset userConfirmedAtUtc,
        string reason)
    {
        EnsureNextRevision(audit);
        var prior = CurrentRevision.Input;
        var confirmation = new UserConfirmedExecutionInput(
            prior.ExecutedAtUtc,
            prior.Price,
            prior.Quantity,
            prior.Currency,
            userConfirmedAtUtc,
            prior.Broker,
            prior.ExternalReference,
            prior.UserNote);
        var revision = new TradeExecutionRevision(
            revisionId,
            Id,
            audit,
            confirmation,
            RecordDisposition.Voided,
            ExecutionChangeKind.Void,
            reason);
        _revisions.Add(revision);
        return revision;
    }

    private void EnsureNextRevision(RevisionMetadata audit)
    {
        if (audit.RevisionNumber != CurrentRevision.Audit.RevisionNumber + 1 ||
            audit.SupersedesId != CurrentRevision.Audit.Id)
        {
            throw new DomainException("An execution correction must directly supersede the current leaf revision.");
        }
    }
}
