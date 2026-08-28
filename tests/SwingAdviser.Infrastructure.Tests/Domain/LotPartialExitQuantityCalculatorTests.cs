using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.Positions;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class LotPartialExitQuantityCalculatorTests
{
    private static readonly MarginLotId LotId = new(Guid.Parse("72728A76-31F0-4426-9CB4-7F431AA4658D"));

    public static TheoryData<decimal, long, decimal, long, decimal> CandidateCases => new()
    {
        { 400m, 100, 0.50m, 200, 200m },
        { 300m, 100, 0.50m, 100, 200m },
        { 250m, 100, 0.50m, 100, 150m },
        { 200m, 100, 0.50m, 100, 100m },
        { 3m, 1, 0.50m, 1, 2m },
    };

    [Theory]
    [MemberData(nameof(CandidateCases))]
    public void Calculate_FloorsToTradingUnitAndLeavesAtLeastOneUnit(
        decimal currentQuantity,
        long tradingUnit,
        decimal fraction,
        long expectedCandidate,
        decimal expectedRemaining)
    {
        var result = LotPartialExitQuantityCalculator.Calculate(
            LotId,
            currentQuantity,
            new WholeShareQuantity(tradingUnit),
            fraction);

        Assert.Equal(LotId, result.MarginLotId);
        Assert.Equal(currentQuantity, result.CurrentQuantity);
        Assert.Equal(tradingUnit, result.TradingUnit.Value);
        Assert.Equal(fraction, result.PartialTakeProfitFraction);
        Assert.Equal(PartialExitStatus.Candidate, result.Status);
        Assert.Equal(expectedCandidate, result.CandidateQuantity?.Value);
        Assert.Equal(expectedRemaining, result.RemainingQuantity);
        Assert.Equal(expectedCandidate / currentQuantity, result.EffectiveFraction);
        Assert.True(result.RemainingQuantity >= tradingUnit);
    }

    public static TheoryData<decimal, long, decimal> NotFeasibleCases => new()
    {
        { 199m, 100, 0.50m },
        { 150m, 100, 0.99m },
        { 100m, 100, 0.50m },
        { 50m, 100, 0.50m },
    };

    [Theory]
    [MemberData(nameof(NotFeasibleCases))]
    public void Calculate_WhenTheLotCannotBeSplit_ReturnsNotFeasibleWithoutAFullExit(
        decimal currentQuantity,
        long tradingUnit,
        decimal fraction)
    {
        var originalQuantity = currentQuantity;

        var result = LotPartialExitQuantityCalculator.Calculate(
            LotId,
            currentQuantity,
            new WholeShareQuantity(tradingUnit),
            fraction);

        Assert.Equal(PartialExitStatus.NotFeasible, result.Status);
        Assert.Null(result.CandidateQuantity);
        Assert.Null(result.EffectiveFraction);
        Assert.Null(result.RemainingQuantity);
        Assert.Equal(originalQuantity, currentQuantity);
    }

    [Fact]
    public void Calculate_DoesNotAllocateAnotherLotOrChangeTheSourceQuantity()
    {
        var anotherLotId = new MarginLotId(Guid.Parse("12A0F03C-B929-41F0-AC62-BE8DA0E612A6"));
        const decimal currentQuantity = 300m;

        var result = LotPartialExitQuantityCalculator.Calculate(
            LotId,
            currentQuantity,
            new WholeShareQuantity(100),
            0.50m);

        Assert.Equal(LotId, result.MarginLotId);
        Assert.NotEqual(anotherLotId, result.MarginLotId);
        Assert.Equal(300m, currentQuantity);
        Assert.Equal(100, result.CandidateQuantity?.Value);
        Assert.NotEqual((long)currentQuantity, result.CandidateQuantity?.Value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Calculate_RejectsNonPositiveCurrentQuantity(string value)
    {
        var currentQuantity = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LotPartialExitQuantityCalculator.Calculate(
                LotId,
                currentQuantity,
                new WholeShareQuantity(100),
                0.50m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-0.1")]
    [InlineData("1.1")]
    public void Calculate_RejectsAnInvalidFraction(string value)
    {
        var fraction = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LotPartialExitQuantityCalculator.Calculate(
                LotId,
                400m,
                new WholeShareQuantity(100),
                fraction));
    }

    [Fact]
    public void Calculate_RejectsDefaultIdentifiersAndTradingUnits()
    {
        Assert.Throws<ArgumentException>(() =>
            LotPartialExitQuantityCalculator.Calculate(
                default,
                400m,
                new WholeShareQuantity(100),
                0.50m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LotPartialExitQuantityCalculator.Calculate(
                LotId,
                400m,
                default,
                0.50m));
    }

    [Fact]
    public void Calculate_WhenCandidateCannotFitWholeShareType_FailsClosed()
    {
        Assert.Throws<DomainException>(() =>
            LotPartialExitQuantityCalculator.Calculate(
                LotId,
                decimal.MaxValue,
                new WholeShareQuantity(1),
                0.50m));
    }
}
