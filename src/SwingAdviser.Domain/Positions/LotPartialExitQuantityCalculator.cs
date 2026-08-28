using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public sealed record LotPartialExitQuantityEvaluation
{
    private LotPartialExitQuantityEvaluation(
        MarginLotId marginLotId,
        decimal currentQuantity,
        WholeShareQuantity tradingUnit,
        decimal partialTakeProfitFraction,
        PartialExitStatus status,
        WholeShareQuantity? candidateQuantity,
        decimal? effectiveFraction,
        decimal? remainingQuantity)
    {
        MarginLotId = marginLotId;
        CurrentQuantity = currentQuantity;
        TradingUnit = tradingUnit;
        PartialTakeProfitFraction = partialTakeProfitFraction;
        Status = status;
        CandidateQuantity = candidateQuantity;
        EffectiveFraction = effectiveFraction;
        RemainingQuantity = remainingQuantity;
    }

    public MarginLotId MarginLotId { get; }
    public decimal CurrentQuantity { get; }
    public WholeShareQuantity TradingUnit { get; }
    public decimal PartialTakeProfitFraction { get; }
    public PartialExitStatus Status { get; }
    public WholeShareQuantity? CandidateQuantity { get; }
    public decimal? EffectiveFraction { get; }
    public decimal? RemainingQuantity { get; }

    internal static LotPartialExitQuantityEvaluation Candidate(
        MarginLotId marginLotId,
        decimal currentQuantity,
        WholeShareQuantity tradingUnit,
        decimal partialTakeProfitFraction,
        WholeShareQuantity candidateQuantity,
        decimal remainingQuantity) =>
        new(
            marginLotId,
            currentQuantity,
            tradingUnit,
            partialTakeProfitFraction,
            PartialExitStatus.Candidate,
            candidateQuantity,
            candidateQuantity.Value / currentQuantity,
            remainingQuantity);

    internal static LotPartialExitQuantityEvaluation NotFeasible(
        MarginLotId marginLotId,
        decimal currentQuantity,
        WholeShareQuantity tradingUnit,
        decimal partialTakeProfitFraction) =>
        new(
            marginLotId,
            currentQuantity,
            tradingUnit,
            partialTakeProfitFraction,
            PartialExitStatus.NotFeasible,
            null,
            null,
            null);
}

public static class LotPartialExitQuantityCalculator
{
    public static LotPartialExitQuantityEvaluation Calculate(
        MarginLotId marginLotId,
        decimal currentQuantity,
        WholeShareQuantity tradingUnit,
        decimal partialTakeProfitFraction)
    {
        if (marginLotId.Value == Guid.Empty)
        {
            throw new ArgumentException("A partial-exit candidate requires a margin lot ID.", nameof(marginLotId));
        }

        if (currentQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentQuantity),
                "Current lot quantity must be positive.");
        }

        if (tradingUnit.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tradingUnit),
                "Trading unit must be positive.");
        }

        if (partialTakeProfitFraction is <= 0m or >= 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(partialTakeProfitFraction),
                "Partial take-profit fraction must be between zero and one.");
        }

        var unitCount = decimal.Floor(
            currentQuantity * partialTakeProfitFraction / tradingUnit.Value);
        var candidateQuantity = unitCount * tradingUnit.Value;
        var remainingQuantity = currentQuantity - candidateQuantity;

        if (candidateQuantity <= 0m || remainingQuantity < tradingUnit.Value)
        {
            return LotPartialExitQuantityEvaluation.NotFeasible(
                marginLotId,
                currentQuantity,
                tradingUnit,
                partialTakeProfitFraction);
        }

        if (candidateQuantity > long.MaxValue)
        {
            throw new DomainException("The partial-exit candidate exceeds the supported whole-share quantity.");
        }

        return LotPartialExitQuantityEvaluation.Candidate(
            marginLotId,
            currentQuantity,
            tradingUnit,
            partialTakeProfitFraction,
            new WholeShareQuantity((long)candidateQuantity),
            remainingQuantity);
    }
}
