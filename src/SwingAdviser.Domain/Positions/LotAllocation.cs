using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public sealed record LotAllocationRevision
{
    private LotAllocationRevision(
        Guid allocationKey,
        TradeExecutionId closingExecutionId,
        TradeExecutionRevisionId closingExecutionRevisionId,
        MarginLotId marginLotId,
        WholeShareQuantity quantity,
        RevisionMetadata audit,
        DateTimeOffset userConfirmedAtUtc,
        RecordDisposition disposition,
        ExecutionChangeKind changeKind,
        string? correctionReason)
    {
        AllocationKey = allocationKey;
        ClosingExecutionId = closingExecutionId;
        ClosingExecutionRevisionId = closingExecutionRevisionId;
        MarginLotId = marginLotId;
        Quantity = quantity;
        Audit = audit;
        UserConfirmedAtUtc = userConfirmedAtUtc;
        Disposition = disposition;
        ChangeKind = changeKind;
        CorrectionReason = correctionReason;
    }

    public Guid AllocationKey { get; }
    public TradeExecutionId ClosingExecutionId { get; }
    public TradeExecutionRevisionId ClosingExecutionRevisionId { get; }
    public MarginLotId MarginLotId { get; }
    public WholeShareQuantity Quantity { get; }
    public RevisionMetadata Audit { get; }
    public DateTimeOffset UserConfirmedAtUtc { get; }
    public RecordDisposition Disposition { get; }
    public ExecutionChangeKind ChangeKind { get; }
    public string? CorrectionReason { get; }

    public static LotAllocationRevision RegisterUserConfirmed(
        Guid allocationKey,
        TradeExecution closingExecution,
        MarginLot marginLot,
        WholeShareQuantity quantity,
        RevisionMetadata audit,
        DateTimeOffset userConfirmedAtUtc)
    {
        if (allocationKey == Guid.Empty)
        {
            throw new ArgumentException("Allocation key cannot be empty.", nameof(allocationKey));
        }

        if (closingExecution.Kind != ExecutionKind.Close ||
            closingExecution.Origin != ExecutionOrigin.UserConfirmed ||
            closingExecution.PositionId != marginLot.PositionId)
        {
            throw new DomainException("Allocation requires a user-confirmed close in the same position as the selected lot.");
        }

        if (audit.RevisionNumber != 1)
        {
            throw new ArgumentException("An initial allocation requires revision number 1.", nameof(audit));
        }

        return new LotAllocationRevision(
            allocationKey,
            closingExecution.Id,
            closingExecution.CurrentRevision.Id,
            marginLot.Id,
            quantity,
            audit,
            DomainGuard.Utc(userConfirmedAtUtc, nameof(userConfirmedAtUtc)),
            RecordDisposition.Effective,
            ExecutionChangeKind.Initial,
            null);
    }
}
