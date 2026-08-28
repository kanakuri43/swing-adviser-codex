using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public static class PartialExitBreakevenPlanFactory
{
    public const string FactoryVersion = "partial-exit-breakeven-plan-factory-v1";

    public static RiskPlanRevision Create(
        RiskBasisSnapshot riskBasis,
        RiskPlanRevision predecessor,
        MarginLot lot,
        TradeExecution closingExecution,
        LotAllocationRevision allocation,
        PositivePrice currentEntryBasisPrice,
        decimal lotQuantityBeforeClose,
        RevisionMetadata nextAudit)
    {
        ArgumentNullException.ThrowIfNull(riskBasis);
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(lot);
        ArgumentNullException.ThrowIfNull(closingExecution);
        ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(nextAudit);

        if (riskBasis.MarginLotId != lot.Id || allocation.MarginLotId != lot.Id)
        {
            throw new DomainException("The partial close, margin lot, and risk basis must identify the same lot.");
        }

        if (closingExecution.PositionId != lot.PositionId ||
            closingExecution.Kind != ExecutionKind.Close ||
            closingExecution.Origin != ExecutionOrigin.UserConfirmed ||
            closingExecution.CurrentRevision.Disposition != RecordDisposition.Effective)
        {
            throw new DomainException("A breakeven plan requires an effective user-confirmed close for the lot's position.");
        }

        if (allocation.Disposition != RecordDisposition.Effective ||
            allocation.ClosingExecutionId != closingExecution.Id ||
            allocation.ClosingExecutionRevisionId != closingExecution.CurrentRevision.Id)
        {
            throw new DomainException("A breakeven plan requires the exact effective allocation for the current close revision.");
        }

        if (lotQuantityBeforeClose <= 0m || allocation.Quantity.Value >= lotQuantityBeforeClose)
        {
            throw new DomainException("A breakeven plan requires a partial close that leaves a positive lot quantity.");
        }

        if (predecessor.RiskBasisSnapshotId != riskBasis.Id)
        {
            throw new DomainException("The predecessor risk plan must belong to the supplied risk basis.");
        }

        if (nextAudit.RevisionNumber != predecessor.Audit.RevisionNumber + 1 ||
            nextAudit.SupersedesId != predecessor.Audit.Id)
        {
            throw new DomainException("A breakeven plan must directly supersede the current risk-plan leaf.");
        }

        var closeRevision = closingExecution.CurrentRevision;
        if (nextAudit.RecordedAtUtc < predecessor.Audit.RecordedAtUtc ||
            nextAudit.RecordedAtUtc < closeRevision.Audit.RecordedAtUtc ||
            nextAudit.RecordedAtUtc < closeRevision.Input.UserConfirmedAtUtc ||
            nextAudit.RecordedAtUtc < allocation.Audit.RecordedAtUtc ||
            nextAudit.RecordedAtUtc < allocation.UserConfirmedAtUtc)
        {
            throw new DomainException("A breakeven plan cannot be recorded before its predecessor or partial-close evidence.");
        }

        var effectiveAtUtc = closeRevision.Input.ExecutedAtUtc;
        if (effectiveAtUtc < predecessor.EffectiveAtUtc)
        {
            throw new DomainException("A breakeven plan cannot become effective before its predecessor.");
        }

        var stopPrice = riskBasis.Side switch
        {
            PositionSide.Long => new PositivePrice(decimal.Max(
                predecessor.StopPrice.Value,
                currentEntryBasisPrice.Value)),
            PositionSide.Short => new PositivePrice(decimal.Min(
                predecessor.StopPrice.Value,
                currentEntryBasisPrice.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(riskBasis), "Unsupported position side."),
        };

        return new RiskPlanRevision(
            riskBasis.Id,
            nextAudit,
            stopPrice,
            predecessor.TakeProfitPrice,
            RiskPlanReason.PartialExitBreakeven,
            effectiveAtUtc,
            closingExecution.Id,
            allocation.Audit.Id);
    }
}
