using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public enum DailyBarPriceField
{
    High,
    Low,
}

public enum RiskLineKind
{
    StopLoss,
    TakeProfit,
}

public enum PriceLineComparison
{
    LessThanOrEqual,
    GreaterThanOrEqual,
}

public sealed record LotRiskEvaluationReason(
    RiskLineKind LineKind,
    DailyBarPriceField ObservedField,
    PriceLineComparison Comparison,
    PositivePrice ObservedPrice,
    PositivePrice LinePrice,
    bool Reached);

public sealed record LotRiskEvaluation
{
    internal LotRiskEvaluation(
        RiskBasisSnapshot riskBasis,
        RiskPlanRevision riskPlan,
        DateOnly evaluationBarDate,
        DateTimeOffset riskPlanCutoffAtUtc,
        UnitizedRiskPrice high,
        UnitizedRiskPrice low,
        ExitDecision decision,
        IReadOnlyList<LotRiskEvaluationReason> reasons)
    {
        RiskBasisSnapshotId = riskBasis.Id;
        MarginLotId = riskBasis.MarginLotId;
        RiskPlanRevisionId = riskPlan.Audit.Id;
        Side = riskBasis.Side;
        PriceUnit = riskBasis.PriceUnit;
        EvaluationBarDate = evaluationBarDate;
        RiskPlanCutoffAtUtc = riskPlanCutoffAtUtc;
        High = high.Amount;
        Low = low.Amount;
        StopPrice = riskPlan.StopPrice;
        TakeProfitPrice = riskPlan.TakeProfitPrice;
        Decision = decision;
        Reasons = reasons.ToArray();
    }

    public Guid RiskBasisSnapshotId { get; }
    public MarginLotId MarginLotId { get; }
    public Guid RiskPlanRevisionId { get; }
    public PositionSide Side { get; }
    public RiskPriceUnit PriceUnit { get; }
    public DateOnly EvaluationBarDate { get; }
    public DateTimeOffset RiskPlanCutoffAtUtc { get; }
    public PositivePrice High { get; }
    public PositivePrice Low { get; }
    public PositivePrice StopPrice { get; }
    public PositivePrice TakeProfitPrice { get; }
    public ExitDecision Decision { get; }
    public IReadOnlyList<LotRiskEvaluationReason> Reasons { get; }
}

public static class LotRiskEvaluator
{
    public const string AlgorithmVersion = "holding-risk-evaluation-v1";

    public static LotRiskEvaluation Evaluate(
        RiskBasisSnapshot riskBasis,
        IReadOnlyCollection<RiskPlanRevision> riskPlanRevisions,
        DateOnly evaluationBarDate,
        DateTimeOffset riskPlanCutoffAtUtc,
        UnitizedRiskPrice high,
        UnitizedRiskPrice low)
    {
        ArgumentNullException.ThrowIfNull(riskBasis);
        ArgumentNullException.ThrowIfNull(riskPlanRevisions);
        riskPlanCutoffAtUtc = DomainGuard.Utc(riskPlanCutoffAtUtc, nameof(riskPlanCutoffAtUtc));

        if (high.Unit != riskBasis.PriceUnit || low.Unit != riskBasis.PriceUnit)
        {
            throw new DomainException("The evaluation bar and risk basis must use the same price unit.");
        }

        if (high.Amount.Value < low.Amount.Value)
        {
            throw new DomainException("The evaluation bar high cannot be below its low.");
        }

        var activePlan = SelectActiveLeaf(riskBasis, riskPlanRevisions, riskPlanCutoffAtUtc);
        var (stopReason, takeProfitReason) = riskBasis.Side switch
        {
            PositionSide.Long => EvaluateLong(activePlan, high, low),
            PositionSide.Short => EvaluateShort(activePlan, high, low),
            _ => throw new ArgumentOutOfRangeException(nameof(riskBasis), "Unsupported position side."),
        };
        var decision = stopReason.Reached
            ? ExitDecision.StopLoss
            : takeProfitReason.Reached
                ? ExitDecision.TakeProfit
                : ExitDecision.Hold;

        return new LotRiskEvaluation(
            riskBasis,
            activePlan,
            evaluationBarDate,
            riskPlanCutoffAtUtc,
            high,
            low,
            decision,
            [stopReason, takeProfitReason]);
    }

    private static RiskPlanRevision SelectActiveLeaf(
        RiskBasisSnapshot riskBasis,
        IReadOnlyCollection<RiskPlanRevision> revisions,
        DateTimeOffset cutoffAtUtc)
    {
        if (revisions.Count == 0)
        {
            throw new DomainException("A lot risk evaluation requires a risk-plan revision.");
        }

        if (revisions.Any(revision => revision.RiskBasisSnapshotId != riskBasis.Id))
        {
            throw new DomainException("Every risk-plan revision must belong to the evaluated risk basis.");
        }

        var byId = new Dictionary<Guid, RiskPlanRevision>();
        foreach (var revision in revisions)
        {
            if (!byId.TryAdd(revision.Audit.Id, revision))
            {
                throw new DomainException("Risk-plan revision IDs must be unique.");
            }
        }

        var eligible = revisions
            .Where(revision => revision.EffectiveAtUtc <= cutoffAtUtc &&
                               revision.Audit.RecordedAtUtc <= cutoffAtUtc)
            .ToList();
        if (eligible.Count == 0)
        {
            throw new DomainException("No risk-plan revision was effective and recorded at the evaluation cutoff.");
        }

        var supersededIds = eligible
            .Select(revision => revision.Audit.SupersedesId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        var leaves = eligible.Where(revision => !supersededIds.Contains(revision.Audit.Id)).ToList();
        if (leaves.Count != 1)
        {
            throw new DomainException("The risk-plan revision graph must have exactly one active leaf.");
        }

        var current = leaves[0];
        var visited = new HashSet<Guid>();
        while (true)
        {
            if (!visited.Add(current.Audit.Id))
            {
                throw new DomainException("The risk-plan revision graph contains a cycle.");
            }

            if (current.Audit.SupersedesId is not { } predecessorId)
            {
                if (current.Audit.RevisionNumber != 1)
                {
                    throw new DomainException("The risk-plan revision chain must begin at revision 1.");
                }

                if (current.Reason != RiskPlanReason.Initial)
                {
                    throw new DomainException("The risk-plan revision chain must begin with an initial plan.");
                }

                break;
            }

            if (!byId.TryGetValue(predecessorId, out var predecessor) ||
                predecessor.Audit.RevisionNumber != current.Audit.RevisionNumber - 1 ||
                predecessor.Audit.RecordedAtUtc > current.Audit.RecordedAtUtc ||
                predecessor.EffectiveAtUtc > current.EffectiveAtUtc)
            {
                throw new DomainException("The risk-plan revision chain is incomplete or out of order.");
            }

            current = predecessor;
        }

        if (visited.Count != eligible.Count)
        {
            throw new DomainException("The risk-plan revision graph contains a disconnected branch.");
        }

        return leaves[0];
    }

    private static (LotRiskEvaluationReason Stop, LotRiskEvaluationReason TakeProfit) EvaluateLong(
        RiskPlanRevision plan,
        UnitizedRiskPrice high,
        UnitizedRiskPrice low) =>
        (
            new LotRiskEvaluationReason(
                RiskLineKind.StopLoss,
                DailyBarPriceField.Low,
                PriceLineComparison.LessThanOrEqual,
                low.Amount,
                plan.StopPrice,
                low.Amount.Value <= plan.StopPrice.Value),
            new LotRiskEvaluationReason(
                RiskLineKind.TakeProfit,
                DailyBarPriceField.High,
                PriceLineComparison.GreaterThanOrEqual,
                high.Amount,
                plan.TakeProfitPrice,
                high.Amount.Value >= plan.TakeProfitPrice.Value));

    private static (LotRiskEvaluationReason Stop, LotRiskEvaluationReason TakeProfit) EvaluateShort(
        RiskPlanRevision plan,
        UnitizedRiskPrice high,
        UnitizedRiskPrice low) =>
        (
            new LotRiskEvaluationReason(
                RiskLineKind.StopLoss,
                DailyBarPriceField.High,
                PriceLineComparison.GreaterThanOrEqual,
                high.Amount,
                plan.StopPrice,
                high.Amount.Value >= plan.StopPrice.Value),
            new LotRiskEvaluationReason(
                RiskLineKind.TakeProfit,
                DailyBarPriceField.Low,
                PriceLineComparison.LessThanOrEqual,
                low.Amount,
                plan.TakeProfitPrice,
                low.Amount.Value <= plan.TakeProfitPrice.Value));
}
