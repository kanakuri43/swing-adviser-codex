using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public enum ReversalConditionKind
{
    MacdCross,
    Ema20State,
}

public enum ReversalConditionStatus
{
    Matched,
    NotMatched,
    Missing,
}

public enum TechnicalReversalStatus
{
    NotApplicable,
    Matched,
    NotMatched,
    Indeterminate,
}

public enum LotHoldingEvaluationOutcome
{
    Evaluated,
    Indeterminate,
}

public enum PriorTakeProfitReachStatus
{
    NotReached,
    Reached,
    Indeterminate,
}

public abstract record LotTechnicalReversalReason(
    ReversalConditionKind Condition,
    ReversalConditionStatus Status);

public sealed record MacdReversalReason(
    ReversalConditionStatus EvaluationStatus,
    CurrentAndPreviousValue? Line,
    CurrentAndPreviousValue? Signal)
    : LotTechnicalReversalReason(ReversalConditionKind.MacdCross, EvaluationStatus);

public sealed record Ema20ReversalReason(
    ReversalConditionStatus EvaluationStatus,
    PositivePrice? Close,
    PositivePrice? Ema20)
    : LotTechnicalReversalReason(ReversalConditionKind.Ema20State, EvaluationStatus);

public sealed record LotTechnicalReversalInput
{
    public LotTechnicalReversalInput(
        RiskPriceUnit priceUnit,
        CurrentAndPreviousValue? macdLine,
        CurrentAndPreviousValue? macdSignal,
        PositivePrice? close,
        PositivePrice? ema20)
    {
        if (priceUnit.InstrumentId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(priceUnit.Currency.Value) ||
            string.IsNullOrWhiteSpace(priceUnit.BasisHash.Value))
        {
            throw new ArgumentException("Technical reversal input requires a complete price unit.", nameof(priceUnit));
        }

        if (close is { Value: <= 0m } || ema20 is { Value: <= 0m })
        {
            throw new ArgumentOutOfRangeException(nameof(close), "Close and EMA20 must be positive when supplied.");
        }

        PriceUnit = priceUnit;
        MacdLine = macdLine;
        MacdSignal = macdSignal;
        Close = close;
        Ema20 = ema20;
    }

    public RiskPriceUnit PriceUnit { get; }
    public CurrentAndPreviousValue? MacdLine { get; }
    public CurrentAndPreviousValue? MacdSignal { get; }
    public PositivePrice? Close { get; }
    public PositivePrice? Ema20 { get; }
}

public sealed record LotTakeProfitReachEvidence
{
    public LotTakeProfitReachEvidence(
        DateOnly reachedBarDate,
        Guid dailyPriceRevisionId,
        Guid riskPlanRevisionId,
        DailyBarPriceField observedField,
        UnitizedRiskPrice observedPrice,
        UnitizedRiskPrice takeProfitPrice)
    {
        if (dailyPriceRevisionId == Guid.Empty || riskPlanRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Take-profit reach evidence requires exact price and risk-plan revision IDs.");
        }

        if (observedPrice.Amount.Value <= 0m || takeProfitPrice.Amount.Value <= 0m ||
            observedPrice.Unit.InstrumentId.Value == Guid.Empty ||
            takeProfitPrice.Unit.InstrumentId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(observedPrice.Unit.Currency.Value) ||
            string.IsNullOrWhiteSpace(takeProfitPrice.Unit.Currency.Value) ||
            string.IsNullOrWhiteSpace(observedPrice.Unit.BasisHash.Value) ||
            string.IsNullOrWhiteSpace(takeProfitPrice.Unit.BasisHash.Value))
        {
            throw new ArgumentException("Take-profit reach evidence requires complete positive prices.");
        }

        if (observedPrice.Unit != takeProfitPrice.Unit)
        {
            throw new DomainException("Take-profit reach evidence prices must use the same unit.");
        }

        ReachedBarDate = reachedBarDate;
        DailyPriceRevisionId = dailyPriceRevisionId;
        RiskPlanRevisionId = riskPlanRevisionId;
        ObservedField = observedField;
        ObservedPrice = observedPrice;
        TakeProfitPrice = takeProfitPrice;
    }

    public DateOnly ReachedBarDate { get; }
    public Guid DailyPriceRevisionId { get; }
    public Guid RiskPlanRevisionId { get; }
    public DailyBarPriceField ObservedField { get; }
    public UnitizedRiskPrice ObservedPrice { get; }
    public UnitizedRiskPrice TakeProfitPrice { get; }
}

public sealed record PriorTakeProfitReach
{
    public PriorTakeProfitReach(
        PriorTakeProfitReachStatus status,
        LotTakeProfitReachEvidence? evidence = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if ((status == PriorTakeProfitReachStatus.Reached) != (evidence is not null))
        {
            throw new DomainException("Only a reached prior target state can carry exact reach evidence.");
        }

        Status = status;
        Evidence = evidence;
    }

    public PriorTakeProfitReachStatus Status { get; }
    public LotTakeProfitReachEvidence? Evidence { get; }

    public static PriorTakeProfitReach NotReached { get; } =
        new(PriorTakeProfitReachStatus.NotReached);

    public static PriorTakeProfitReach Indeterminate { get; } =
        new(PriorTakeProfitReachStatus.Indeterminate);

    public static PriorTakeProfitReach Reached(LotTakeProfitReachEvidence evidence) =>
        new(PriorTakeProfitReachStatus.Reached, evidence);
}

public sealed record TechnicalReversalEvaluation
{
    internal TechnicalReversalEvaluation(
        TechnicalReversalStatus status,
        IReadOnlyList<LotTechnicalReversalReason> reasons)
    {
        Status = status;
        Reasons = reasons.ToArray();
    }

    public TechnicalReversalStatus Status { get; }
    public IReadOnlyList<LotTechnicalReversalReason> Reasons { get; }

    internal static TechnicalReversalEvaluation NotApplicable { get; } =
        new(TechnicalReversalStatus.NotApplicable, []);
}

public sealed record LotHoldingRiskEvaluation
{
    internal LotHoldingRiskEvaluation(
        LotRiskEvaluation priceLineEvaluation,
        LotHoldingEvaluationOutcome outcome,
        ExitDecision? decision,
        bool? takeProfitReached,
        PriorTakeProfitReach priorTakeProfitReach,
        TechnicalReversalEvaluation technicalReversal)
    {
        PriceLineEvaluation = priceLineEvaluation;
        Outcome = outcome;
        Decision = decision;
        TakeProfitReached = takeProfitReached;
        PriorTakeProfitReach = priorTakeProfitReach;
        TechnicalReversal = technicalReversal;
    }

    public MarginLotId MarginLotId => PriceLineEvaluation.MarginLotId;
    public LotRiskEvaluation PriceLineEvaluation { get; }
    public LotHoldingEvaluationOutcome Outcome { get; }
    public ExitDecision? Decision { get; }
    public bool? TakeProfitReached { get; }
    public PriorTakeProfitReach PriorTakeProfitReach { get; }
    public TechnicalReversalEvaluation TechnicalReversal { get; }
}

public static class LotHoldingRiskEvaluator
{
    public const string AlgorithmVersion = LotRiskEvaluator.AlgorithmVersion;

    public static LotHoldingRiskEvaluation Evaluate(
        RiskBasisSnapshot riskBasis,
        IReadOnlyCollection<RiskPlanRevision> riskPlanRevisions,
        DateOnly evaluationBarDate,
        DateTimeOffset riskPlanCutoffAtUtc,
        UnitizedRiskPrice high,
        UnitizedRiskPrice low,
        PriorTakeProfitReach priorTakeProfitReach,
        LotTechnicalReversalInput? technicalInput)
    {
        var priceLines = LotRiskEvaluator.Evaluate(
            riskBasis,
            riskPlanRevisions,
            evaluationBarDate,
            riskPlanCutoffAtUtc,
            high,
            low);
        ArgumentNullException.ThrowIfNull(priorTakeProfitReach);
        ValidatePriorReachEvidence(
            riskBasis,
            riskPlanRevisions,
            evaluationBarDate,
            priorTakeProfitReach.Evidence);

        var currentTargetReached = priceLines.Reasons.Single(
            reason => reason.LineKind == RiskLineKind.TakeProfit).Reached;
        bool? targetReached = currentTargetReached
            ? true
            : priorTakeProfitReach.Status switch
            {
                PriorTakeProfitReachStatus.Reached => true,
                PriorTakeProfitReachStatus.NotReached => false,
                PriorTakeProfitReachStatus.Indeterminate => null,
                _ => throw new ArgumentOutOfRangeException(nameof(priorTakeProfitReach)),
            };
        var technical = targetReached == true
            ? EvaluateTechnicalReversal(riskBasis, technicalInput)
            : TechnicalReversalEvaluation.NotApplicable;

        if (priceLines.Decision == ExitDecision.StopLoss)
        {
            return Evaluated(
                priceLines,
                ExitDecision.StopLoss,
                targetReached,
                priorTakeProfitReach,
                technical);
        }

        if (targetReached is null)
        {
            return new LotHoldingRiskEvaluation(
                priceLines,
                LotHoldingEvaluationOutcome.Indeterminate,
                null,
                null,
                priorTakeProfitReach,
                TechnicalReversalEvaluation.NotApplicable);
        }

        if (targetReached == false)
        {
            return Evaluated(
                priceLines,
                ExitDecision.Hold,
                false,
                priorTakeProfitReach,
                technical);
        }

        return technical.Status switch
        {
            TechnicalReversalStatus.Matched => Evaluated(
                priceLines,
                ExitDecision.Exit,
                true,
                priorTakeProfitReach,
                technical),
            TechnicalReversalStatus.NotMatched => Evaluated(
                priceLines,
                ExitDecision.TakeProfit,
                true,
                priorTakeProfitReach,
                technical),
            TechnicalReversalStatus.Indeterminate => new LotHoldingRiskEvaluation(
                priceLines,
                LotHoldingEvaluationOutcome.Indeterminate,
                null,
                true,
                priorTakeProfitReach,
                technical),
            _ => throw new DomainException("Technical reversal evaluation is required after take-profit reach."),
        };
    }

    private static LotHoldingRiskEvaluation Evaluated(
        LotRiskEvaluation priceLines,
        ExitDecision decision,
        bool? targetReached,
        PriorTakeProfitReach priorTakeProfitReach,
        TechnicalReversalEvaluation technical) =>
        new(
            priceLines,
            LotHoldingEvaluationOutcome.Evaluated,
            decision,
            targetReached,
            priorTakeProfitReach,
            technical);

    private static TechnicalReversalEvaluation EvaluateTechnicalReversal(
        RiskBasisSnapshot riskBasis,
        LotTechnicalReversalInput? input)
    {
        if (input is not null && input.PriceUnit != riskBasis.PriceUnit)
        {
            throw new DomainException("Technical indicators and risk lines must use the same price unit.");
        }

        var macdStatus = input?.MacdLine is { } line && input.MacdSignal is { } signal
            ? riskBasis.Side switch
            {
                PositionSide.Long => line.Previous >= signal.Previous && line.Current < signal.Current
                    ? ReversalConditionStatus.Matched
                    : ReversalConditionStatus.NotMatched,
                PositionSide.Short => line.Previous <= signal.Previous && line.Current > signal.Current
                    ? ReversalConditionStatus.Matched
                    : ReversalConditionStatus.NotMatched,
                _ => throw new ArgumentOutOfRangeException(nameof(riskBasis), "Unsupported position side."),
            }
            : ReversalConditionStatus.Missing;
        var emaStatus = input?.Close is { } close && input.Ema20 is { } ema20
            ? riskBasis.Side switch
            {
                PositionSide.Long => close.Value < ema20.Value
                    ? ReversalConditionStatus.Matched
                    : ReversalConditionStatus.NotMatched,
                PositionSide.Short => close.Value > ema20.Value
                    ? ReversalConditionStatus.Matched
                    : ReversalConditionStatus.NotMatched,
                _ => throw new ArgumentOutOfRangeException(nameof(riskBasis), "Unsupported position side."),
            }
            : ReversalConditionStatus.Missing;
        var status = macdStatus == ReversalConditionStatus.Matched ||
                     emaStatus == ReversalConditionStatus.Matched
            ? TechnicalReversalStatus.Matched
            : macdStatus == ReversalConditionStatus.NotMatched &&
              emaStatus == ReversalConditionStatus.NotMatched
                ? TechnicalReversalStatus.NotMatched
                : TechnicalReversalStatus.Indeterminate;

        return new TechnicalReversalEvaluation(
            status,
            [
                new MacdReversalReason(macdStatus, input?.MacdLine, input?.MacdSignal),
                new Ema20ReversalReason(emaStatus, input?.Close, input?.Ema20),
            ]);
    }

    private static void ValidatePriorReachEvidence(
        RiskBasisSnapshot riskBasis,
        IReadOnlyCollection<RiskPlanRevision> riskPlanRevisions,
        DateOnly evaluationBarDate,
        LotTakeProfitReachEvidence? evidence)
    {
        if (evidence is null)
        {
            return;
        }

        if (evidence.ReachedBarDate >= evaluationBarDate)
        {
            throw new DomainException("Prior take-profit reach evidence must precede the evaluation bar.");
        }

        if (evidence.ObservedPrice.Unit != riskBasis.PriceUnit ||
            evidence.TakeProfitPrice.Unit != riskBasis.PriceUnit)
        {
            throw new DomainException("Take-profit reach evidence must use the risk basis price unit.");
        }

        var plan = riskPlanRevisions.SingleOrDefault(
            revision => revision.Audit.Id == evidence.RiskPlanRevisionId)
            ?? throw new DomainException("Take-profit reach evidence references an unknown risk-plan revision.");
        if (plan.RiskBasisSnapshotId != riskBasis.Id ||
            plan.TakeProfitPrice != evidence.TakeProfitPrice.Amount)
        {
            throw new DomainException("Take-profit reach evidence does not match its exact risk plan.");
        }

        var validReach = riskBasis.Side switch
        {
            PositionSide.Long => evidence.ObservedField == DailyBarPriceField.High &&
                                 evidence.ObservedPrice.Amount.Value >= evidence.TakeProfitPrice.Amount.Value,
            PositionSide.Short => evidence.ObservedField == DailyBarPriceField.Low &&
                                  evidence.ObservedPrice.Amount.Value <= evidence.TakeProfitPrice.Amount.Value,
            _ => false,
        };
        if (!validReach)
        {
            throw new DomainException("Take-profit reach evidence does not satisfy the side-specific boundary.");
        }
    }
}
