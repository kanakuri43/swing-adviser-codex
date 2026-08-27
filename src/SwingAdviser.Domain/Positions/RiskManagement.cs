using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public readonly record struct RiskPriceUnit
{
    public RiskPriceUnit(
        InstrumentId instrumentId,
        CurrencyCode currency,
        Sha256Hash basisHash)
    {
        if (instrumentId.Value == Guid.Empty)
        {
            throw new ArgumentException("The risk price unit requires an instrument ID.", nameof(instrumentId));
        }

        if (string.IsNullOrWhiteSpace(currency.Value))
        {
            throw new ArgumentException("The risk price unit requires a currency.", nameof(currency));
        }

        if (string.IsNullOrWhiteSpace(basisHash.Value))
        {
            throw new ArgumentException("The risk price unit requires a share-unit basis hash.", nameof(basisHash));
        }

        InstrumentId = instrumentId;
        Currency = currency;
        BasisHash = basisHash;
    }

    public InstrumentId InstrumentId { get; }
    public CurrencyCode Currency { get; }

    // Hash of the versioned split/consolidation basis defining the per-share unit.
    public Sha256Hash BasisHash { get; }
}

public readonly record struct UnitizedRiskPrice
{
    public UnitizedRiskPrice(PositivePrice amount, RiskPriceUnit unit)
    {
        if (amount.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "A unitized risk price must be positive.");
        }

        if (unit.InstrumentId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(unit.Currency.Value) ||
            string.IsNullOrWhiteSpace(unit.BasisHash.Value))
        {
            throw new ArgumentException("A unitized risk price requires a complete price unit.", nameof(unit));
        }

        Amount = amount;
        Unit = unit;
    }

    public PositivePrice Amount { get; }
    public RiskPriceUnit Unit { get; }
}

public sealed record RiskManagementParameters
{
    public const string SchemaVersion = "risk-management-parameters-v1";

    public RiskManagementParameters(
        decimal longStopMultiplier,
        decimal shortStopMultiplier,
        decimal partialTakeProfitRMultiple,
        decimal partialTakeProfitFraction)
    {
        LongStopMultiplier = DomainGuard.Positive(longStopMultiplier, nameof(longStopMultiplier));
        ShortStopMultiplier = DomainGuard.Positive(shortStopMultiplier, nameof(shortStopMultiplier));
        PartialTakeProfitRMultiple = DomainGuard.Positive(
            partialTakeProfitRMultiple,
            nameof(partialTakeProfitRMultiple));
        if (partialTakeProfitFraction is <= 0m or >= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partialTakeProfitFraction),
                "Partial take-profit fraction must be between zero and one.");
        }

        PartialTakeProfitFraction = partialTakeProfitFraction;
    }

    public static RiskManagementParameters Initial { get; } = new(
        longStopMultiplier: 3.0m,
        shortStopMultiplier: 2.5m,
        partialTakeProfitRMultiple: 1.5m,
        partialTakeProfitFraction: 0.50m);

    public decimal LongStopMultiplier { get; }
    public decimal ShortStopMultiplier { get; }
    public decimal PartialTakeProfitRMultiple { get; }
    public decimal PartialTakeProfitFraction { get; }

    public decimal StopMultiplierFor(PositionSide side) => side switch
    {
        PositionSide.Long => LongStopMultiplier,
        PositionSide.Short => ShortStopMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };
}

public sealed record RiskBasisSnapshot
{
    private RiskBasisSnapshot(
        Guid id,
        MarginLotId marginLotId,
        TradeExecutionRevisionId openingExecutionRevisionId,
        PositionSide side,
        RiskPriceUnit priceUnit,
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
        PriceUnit = priceUnit;
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
    public RiskPriceUnit PriceUnit { get; }
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

    internal static RiskBasisSnapshot Create(
        Guid id,
        Position position,
        MarginLot lot,
        UnitizedRiskPrice entryBasisPrice,
        DateOnly atrReferenceBarDate,
        UnitizedRiskPrice fixedAtr,
        int atrPeriod,
        string atrAlgorithmId,
        RiskManagementParameters parameters,
        Sha256Hash contentHash,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(lot);
        ArgumentNullException.ThrowIfNull(parameters);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Risk basis ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(contentHash.Value))
        {
            throw new ArgumentException("The risk basis requires a content hash.", nameof(contentHash));
        }

        if (lot.PositionId != position.Id)
        {
            throw new DomainException("The risk basis lot must belong to the supplied position.");
        }

        if (entryBasisPrice.Unit != fixedAtr.Unit)
        {
            throw new DomainException("Entry price and fixed ATR must use the same currency and share unit.");
        }

        if (entryBasisPrice.Unit.InstrumentId != position.InstrumentId)
        {
            throw new DomainException("The risk price unit must belong to the position instrument.");
        }

        var openingExecution = position.Executions.SingleOrDefault(
            execution => execution.Id == lot.OpeningExecutionId && execution.Kind == ExecutionKind.Open);
        if (openingExecution is null ||
            openingExecution.CurrentRevision.Id != lot.InitialOpeningRevisionId ||
            openingExecution.CurrentRevision.Disposition != RecordDisposition.Effective)
        {
            throw new DomainException("The risk basis requires the lot's exact effective opening execution revision.");
        }

        var openingRevision = openingExecution.CurrentRevision;

        if (entryBasisPrice.Amount != openingRevision.Input.Price ||
            entryBasisPrice.Unit.Currency != openingRevision.Input.Currency)
        {
            throw new DomainException("The entry basis must match the exact opening execution price and currency.");
        }

        var side = position.Side;
        var stopMultiplier = parameters.StopMultiplierFor(side);
        var partialTakeProfitRMultiple = parameters.PartialTakeProfitRMultiple;
        var partialTakeProfitFraction = parameters.PartialTakeProfitFraction;
        DomainGuard.Positive(atrPeriod, nameof(atrPeriod));

        decimal risk;
        decimal stop;
        decimal target;
        try
        {
            risk = stopMultiplier * fixedAtr.Amount.Value;
            stop = side == PositionSide.Long
                ? entryBasisPrice.Amount.Value - risk
                : entryBasisPrice.Amount.Value + risk;
            target = side == PositionSide.Long
                ? entryBasisPrice.Amount.Value + (risk * partialTakeProfitRMultiple)
                : entryBasisPrice.Amount.Value - (risk * partialTakeProfitRMultiple);
        }
        catch (OverflowException exception)
        {
            throw new DomainException("The configured risk basis exceeds the supported decimal price range.", exception);
        }

        if (stop <= 0m || target <= 0m)
        {
            throw new DomainException("The configured risk basis produces a non-positive price line.");
        }

        return new RiskBasisSnapshot(
            id,
            lot.Id,
            lot.InitialOpeningRevisionId,
            side,
            entryBasisPrice.Unit,
            entryBasisPrice.Amount,
            atrReferenceBarDate,
            fixedAtr.Amount,
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

    internal RiskPlanRevision CreateInitialPlan(
        RevisionMetadata audit,
        DateTimeOffset effectiveAtUtc)
    {
        ArgumentNullException.ThrowIfNull(audit);
        if (audit.RevisionNumber != 1 || audit.SupersedesId is not null)
        {
            throw new DomainException("An initial risk plan must be revision 1 without a predecessor.");
        }

        if (audit.RecordedAtUtc < CreatedAtUtc)
        {
            throw new DomainException("An initial risk plan cannot be recorded before its risk basis.");
        }

        return new RiskPlanRevision(
            Id,
            audit,
            InitialStopPrice,
            InitialTakeProfitPrice,
            RiskPlanReason.Initial,
            effectiveAtUtc);
    }
}

public sealed record InitialRiskPlanBundle
{
    public const string FactoryVersion = "initial-risk-plan-factory-v1";

    private InitialRiskPlanBundle(
        RiskBasisSnapshot riskBasis,
        RiskPlanRevision riskPlan)
    {
        RiskBasis = riskBasis;
        RiskPlan = riskPlan;
    }

    public RiskBasisSnapshot RiskBasis { get; }
    public RiskPlanRevision RiskPlan { get; }

    public static InitialRiskPlanBundle Create(
        Guid riskBasisId,
        RevisionMetadata initialRiskPlanAudit,
        Position position,
        MarginLot lot,
        UnitizedRiskPrice entryBasisPrice,
        DateOnly atrReferenceBarDate,
        UnitizedRiskPrice fixedAtr,
        int atrPeriod,
        string atrAlgorithmId,
        RiskManagementParameters parameters,
        Sha256Hash riskBasisContentHash,
        DateTimeOffset effectiveAtUtc,
        DateTimeOffset createdAtUtc)
    {
        var basis = RiskBasisSnapshot.Create(
            riskBasisId,
            position,
            lot,
            entryBasisPrice,
            atrReferenceBarDate,
            fixedAtr,
            atrPeriod,
            atrAlgorithmId,
            parameters,
            riskBasisContentHash,
            createdAtUtc);
        var plan = basis.CreateInitialPlan(initialRiskPlanAudit, effectiveAtUtc);
        return new InitialRiskPlanBundle(basis, plan);
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

        ArgumentNullException.ThrowIfNull(audit);

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        if (string.IsNullOrWhiteSpace(audit.ContentHash.Value))
        {
            throw new ArgumentException("A risk plan revision requires a content hash.", nameof(audit));
        }

        if ((audit.RevisionNumber == 1) != (reason == RiskPlanReason.Initial))
        {
            throw new DomainException("Revision 1 must be the initial plan, and later revisions cannot be initial.");
        }

        if (reason is RiskPlanReason.Initial or RiskPlanReason.UserCorrection &&
            (triggerTradeExecutionId is not null || triggerLotAllocationRevisionId is not null ||
             triggerPositionAdjustmentId is not null))
        {
            throw new DomainException("Initial and user-correction plans cannot carry trigger evidence.");
        }

        if (reason == RiskPlanReason.PartialExitBreakeven &&
            (triggerTradeExecutionId is null || triggerLotAllocationRevisionId is null ||
             triggerPositionAdjustmentId is not null))
        {
            throw new DomainException("A breakeven plan requires an explicitly registered close and lot allocation.");
        }

        if (reason == RiskPlanReason.CorporateActionConversion &&
            (triggerPositionAdjustmentId is null || triggerTradeExecutionId is not null ||
             triggerLotAllocationRevisionId is not null))
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
