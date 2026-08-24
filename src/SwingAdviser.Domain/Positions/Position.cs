using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public sealed class Position
{
    private readonly List<TradeExecution> _executions = [];

    public Position(
        PositionId id,
        InstrumentId instrumentId,
        PositionSide side,
        Guid? strategyParameterSnapshotId,
        CandidateResultId? originCandidateResultId,
        DateTimeOffset createdAtUtc)
    {
        if (id.Value == Guid.Empty || instrumentId.Value == Guid.Empty)
        {
            throw new ArgumentException("Position and instrument IDs cannot be empty.");
        }

        Id = id;
        InstrumentId = instrumentId;
        Side = side;
        StrategyParameterSnapshotId = strategyParameterSnapshotId;
        OriginCandidateResultId = originCandidateResultId;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public PositionId Id { get; }
    public InstrumentId InstrumentId { get; }
    public PositionSide Side { get; }
    public Guid? StrategyParameterSnapshotId { get; }

    // Provenance only. It never authorizes or creates a broker execution.
    public CandidateResultId? OriginCandidateResultId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<TradeExecution> Executions => _executions.AsReadOnly();

    public TradeExecution RegisterUserConfirmedExecution(
        TradeExecutionId executionId,
        ExecutionKind kind,
        UserConfirmedExecutionInput input,
        TradeExecutionRevisionId revisionId,
        RevisionMetadata revisionAudit,
        DateTimeOffset createdAtUtc,
        CandidateResultId? candidateContextId = null)
    {
        if (_executions.Any(x => x.Id == executionId))
        {
            throw new DomainException("The execution is already registered in this position.");
        }

        var execution = TradeExecution.RegisterUserConfirmed(
            executionId,
            Id,
            kind,
            candidateContextId,
            input,
            revisionId,
            revisionAudit,
            createdAtUtc);
        _executions.Add(execution);
        return execution;
    }
}

public sealed record PositionStateRevision
{
    public PositionStateRevision(
        PositionId positionId,
        RevisionMetadata audit,
        PositionStatus status,
        ReconciliationStatus reconciliationStatus,
        DateTimeOffset effectiveAtUtc,
        string reason,
        string? memo)
    {
        PositionId = positionId;
        Audit = audit;
        Status = status;
        ReconciliationStatus = reconciliationStatus;
        EffectiveAtUtc = DomainGuard.Utc(effectiveAtUtc, nameof(effectiveAtUtc));
        Reason = DomainGuard.Required(reason, nameof(reason));
        Memo = memo?.Trim();
    }

    public PositionId PositionId { get; }
    public RevisionMetadata Audit { get; }
    public PositionStatus Status { get; }
    public ReconciliationStatus ReconciliationStatus { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public string Reason { get; }
    public string? Memo { get; }
}
