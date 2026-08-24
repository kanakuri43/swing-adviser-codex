using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public sealed record RiskBasisSnapshot
{
    private RiskBasisSnapshot(
        Guid id,
        MarginLotId marginLotId,
        TradeExecutionRevisionId openingExecutionRevisionId,
        PositionSide side,
        PositivePrice entryBasisPrice,
        DateOnly atrReferenceBarDate,
        PositivePrice fixedAtr,
        int atrPeriod,
        string atrAlgorithmId,
        decimal stopMultiplier,
        decimal riskAmountR,
        decimal partialTakeProfitRMultiple,
        decimal partialTakeProfitFraction,
        PositivePrice initialStopPrice,
        PositivePrice initialTakeProfitPrice,
        Sha256Hash contentHash,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        MarginLotId = marginLotId;
        OpeningExecutionRevisionId = openingExecutionRevisionId;
        Side = side;
        EntryBasisPrice = entryBasisPrice;
        AtrReferenceBarDate = atrReferenceBarDate;
        FixedAtr = fixedAtr;
        AtrPeriod = atrPeriod;
        AtrAlgorithmId = atrAlgorithmId;
        StopMultiplier = stopMultiplier;
        RiskAmountR = riskAmountR;
        PartialTakeProfitRMultiple = partialTakeProfitRMultiple;
        PartialTakeProfitFraction = partialTakeProfitFraction;
        InitialStopPrice = initialStopPrice;
        InitialTakeProfitPrice = initialTakeProfitPrice;
        ContentHash = contentHash;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public MarginLotId MarginLotId { get; }
    public TradeExecutionRevisionId OpeningExecutionRevisionId { get; }
    public PositionSide Side { get; }
    public PositivePrice EntryBasisPrice { get; }
    public DateOnly AtrReferenceBarDate { get; }
    public PositivePrice FixedAtr { get; }
    public int AtrPeriod { get; }
    public string AtrAlgorithmId { get; }
    public decimal StopMultiplier { get; }
    public decimal RiskAmountR { get; }
    public decimal PartialTakeProfitRMultiple { get; }
    public decimal PartialTakeProfitFraction { get; }
    public PositivePrice InitialStopPrice { get; }
    public PositivePrice InitialTakeProfitPrice { get; }
    public Sha256Hash ContentHash { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public static RiskBasisSnapshot Create(
        Guid id,
        MarginLot lot,
        PositionSide side,
        PositivePrice entryBasisPrice,
        DateOnly atrReferenceBarDate,
        PositivePrice fixedAtr,
        int atrPeriod,
        string atrAlgorithmId,
        decimal stopMultiplier,
        decimal partialTakeProfitRMultiple,
        decimal partialTakeProfitFraction,
        Sha256Hash contentHash,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Risk basis ID cannot be empty.", nameof(id));
        }

        DomainGuard.Positive(atrPeriod, nameof(atrPeriod));
        DomainGuard.Positive(stopMultiplier, nameof(stopMultiplier));
        DomainGuard.Positive(partialTakeProfitRMultiple, nameof(partialTakeProfitRMultiple));
        if (partialTakeProfitFraction is <= 0m or >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(partialTakeProfitFraction), "Partial take-profit fraction must be between zero and one.");
        }

        var risk = stopMultiplier * fixedAtr.Value;
        var stop = side == PositionSide.Long
            ? entryBasisPrice.Value - risk
            : entryBasisPrice.Value + risk;
        var target = side == PositionSide.Long
            ? entryBasisPrice.Value + (risk * partialTakeProfitRMultiple)
            : entryBasisPrice.Value - (risk * partialTakeProfitRMultiple);

        if (stop <= 0m || target <= 0m)
        {
            throw new DomainException("The configured risk basis produces a non-positive price line.");
        }

        return new RiskBasisSnapshot(
            id,
            lot.Id,
            lot.InitialOpeningRevisionId,
            side,
            entryBasisPrice,
            atrReferenceBarDate,
            fixedAtr,
            atrPeriod,
            DomainGuard.Required(atrAlgorithmId, nameof(atrAlgorithmId)),
            stopMultiplier,
            risk,
            partialTakeProfitRMultiple,
            partialTakeProfitFraction,
            new PositivePrice(stop),
            new PositivePrice(target),
            contentHash,
            DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc)));
    }
}

public sealed record RiskPlanRevision
{
    public RiskPlanRevision(
        Guid riskBasisSnapshotId,
        RevisionMetadata audit,
        PositivePrice stopPrice,
        PositivePrice takeProfitPrice,
        RiskPlanReason reason,
        DateTimeOffset effectiveAtUtc,
        TradeExecutionId? triggerTradeExecutionId = null,
        Guid? triggerLotAllocationRevisionId = null,
        Guid? triggerPositionAdjustmentId = null)
    {
        if (riskBasisSnapshotId == Guid.Empty)
        {
            throw new ArgumentException("Risk basis ID cannot be empty.", nameof(riskBasisSnapshotId));
        }

        if (reason == RiskPlanReason.PartialExitBreakeven &&
            (triggerTradeExecutionId is null || triggerLotAllocationRevisionId is null))
        {
            throw new DomainException("A breakeven plan requires an explicitly registered close and lot allocation.");
        }

        if (reason == RiskPlanReason.CorporateActionConversion && triggerPositionAdjustmentId is null)
        {
            throw new DomainException("A corporate-action conversion plan requires its exact adjustment evidence.");
        }

        RiskBasisSnapshotId = riskBasisSnapshotId;
        Audit = audit;
        StopPrice = stopPrice;
        TakeProfitPrice = takeProfitPrice;
        Reason = reason;
        EffectiveAtUtc = DomainGuard.Utc(effectiveAtUtc, nameof(effectiveAtUtc));
        TriggerTradeExecutionId = triggerTradeExecutionId;
        TriggerLotAllocationRevisionId = triggerLotAllocationRevisionId;
        TriggerPositionAdjustmentId = triggerPositionAdjustmentId;
    }

    public Guid RiskBasisSnapshotId { get; }
    public RevisionMetadata Audit { get; }
    public PositivePrice StopPrice { get; }
    public PositivePrice TakeProfitPrice { get; }
    public RiskPlanReason Reason { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public TradeExecutionId? TriggerTradeExecutionId { get; }
    public Guid? TriggerLotAllocationRevisionId { get; }
    public Guid? TriggerPositionAdjustmentId { get; }

    // Risk lines intentionally remain price-based; carrying costs are reported separately.
    public bool IsCostAdjusted => false;
}

public sealed record PositionEvaluation
{
    public PositionEvaluation(
        Guid id,
        AnalysisRunId analysisRunId,
        PositionId positionId,
        Guid inputManifestId,
        DateOnly evaluationBarDate,
        ExitDecision decision,
        string reasonSummary,
        decimal currentQuantity,
        decimal? priceProfitAndLoss,
        decimal? confirmedCostProfitAndLoss,
        decimal? estimatedNetProfitAndLoss,
        decimal? costToRRatio,
        long? partialExitQuantity,
        PartialExitStatus partialExitStatus,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || inputManifestId == Guid.Empty)
        {
            throw new ArgumentException("Evaluation and manifest IDs cannot be empty.");
        }

        if (currentQuantity < 0m || partialExitQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentQuantity));
        }

        if (partialExitStatus == PartialExitStatus.Candidate && partialExitQuantity is null or <= 0)
        {
            throw new DomainException("A partial-exit candidate requires a positive whole-share quantity.");
        }

        if (partialExitStatus != PartialExitStatus.Candidate && partialExitQuantity is not null)
        {
            throw new DomainException("Only a partial-exit candidate can carry a quantity.");
        }

        Id = id;
        AnalysisRunId = analysisRunId;
        PositionId = positionId;
        InputManifestId = inputManifestId;
        EvaluationBarDate = evaluationBarDate;
        Decision = decision;
        ReasonSummary = DomainGuard.Required(reasonSummary, nameof(reasonSummary));
        CurrentQuantity = currentQuantity;
        PriceProfitAndLoss = priceProfitAndLoss;
        ConfirmedCostProfitAndLoss = confirmedCostProfitAndLoss;
        EstimatedNetProfitAndLoss = estimatedNetProfitAndLoss;
        CostToRRatio = costToRRatio;
        PartialExitQuantity = partialExitQuantity;
        PartialExitStatus = partialExitStatus;
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public PositionId PositionId { get; }
    public Guid InputManifestId { get; }
    public DateOnly EvaluationBarDate { get; }
    public ExitDecision Decision { get; }
    public string ReasonSummary { get; }
    public decimal CurrentQuantity { get; }
    public decimal? PriceProfitAndLoss { get; }
    public decimal? ConfirmedCostProfitAndLoss { get; }
    public decimal? EstimatedNetProfitAndLoss { get; }
    public decimal? CostToRRatio { get; }
    public long? PartialExitQuantity { get; }
    public PartialExitStatus PartialExitStatus { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}
