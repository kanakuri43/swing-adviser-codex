using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.MarginCosts;
using SwingAdviser.Domain.Positions;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class LotProfitAndLossCalculatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 28, 4, 13, 0, TimeSpan.Zero);

    [Fact]
    public void Long_UsesConfirmedCostWithoutDoubleCountingItsEstimate()
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.BuyerInterest, "interest-2026-08");
        var estimate = Append(item, CostValuationKind.Estimate, CostDirection.Charge, CostAmount.Known(500m, CurrencyCode.Jpy));
        var confirmed = Append(item, CostValuationKind.Confirmed, CostDirection.Charge, CostAmount.Known(450m, CurrencyCode.Jpy));

        var result = Calculate(context, currentPrice: 1_100m, [item]);

        Assert.Equal("lot-profit-and-loss-v1", LotProfitAndLossCalculator.AlgorithmVersion);
        Assert.Equal(10_000m, result.PriceProfitAndLoss.Amount);
        Assert.Equal(450m, result.ConfirmedCosts.NetCost);
        Assert.Equal(9_550m, result.ConfirmedCostAdjustedProfitAndLoss.Amount);
        Assert.Equal(450m, result.ReferenceCosts.NetCost);
        Assert.Equal(9_550m, result.EstimatedNetProfitAndLoss.Amount);
        Assert.Equal(0.015m, result.CostToRRatio.Value);
        Assert.Equal([confirmed.Id], result.ConfirmedCosts.CountedObservationIds);
        Assert.Equal([confirmed.Id], result.ReferenceCosts.CountedObservationIds);
        var selection = Assert.Single(result.CostSelections);
        Assert.Equal(estimate.Id, selection.EstimateObservationId);
        Assert.Equal(confirmed.Id, selection.ConfirmedObservationId);
        Assert.Equal(confirmed.Id, selection.ReferenceObservationId);
    }

    [Fact]
    public void Short_ComputesDirectionAndEstimateOnlyReferenceNet()
    {
        var context = Context(PositionSide.Short);
        var item = CostItem(context.Lot.Id, MarginCostType.StockLendingFee, "lending-2026-08");
        var estimate = Append(item, CostValuationKind.Estimate, CostDirection.Charge, CostAmount.Known(300m, CurrencyCode.Jpy));

        var result = Calculate(context, currentPrice: 900m, [item]);

        Assert.Equal(10_000m, result.PriceProfitAndLoss.Amount);
        Assert.False(result.ConfirmedCostAdjustedProfitAndLoss.IsKnown);
        Assert.Equal(AmountStatus.Unknown, result.ConfirmedCostAdjustedProfitAndLoss.Status);
        Assert.Equal(300m, result.ReferenceCosts.NetCost);
        Assert.Equal(9_700m, result.EstimatedNetProfitAndLoss.Amount);
        Assert.Equal(0.012m, result.CostToRRatio.Value);
        Assert.Equal(estimate.Id, Assert.Single(result.CostSelections).ReferenceObservationId);
    }

    [Fact]
    public void UnresolvedConfirmed_FallsBackToEstimateButRemainsVisible()
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.Backwardation, "backwardation-2026-08-28");
        var estimate = Append(item, CostValuationKind.Estimate, CostDirection.Charge, CostAmount.Known(800m, CurrencyCode.Jpy));
        var confirmed = Append(item, CostValuationKind.Confirmed, CostDirection.Charge, CostAmount.Unpublished());

        var result = Calculate(context, currentPrice: 1_100m, [item]);

        Assert.Equal(AmountStatus.Unpublished, result.ConfirmedCosts.Status);
        Assert.Null(result.ConfirmedCosts.NetCost);
        Assert.Equal([AmountStatus.Unpublished], result.ConfirmedCosts.BlockingStatuses);
        Assert.Equal(800m, result.ReferenceCosts.NetCost);
        Assert.Equal(9_200m, result.EstimatedNetProfitAndLoss.Amount);
        var selection = Assert.Single(result.CostSelections);
        Assert.Equal(confirmed.Id, selection.ConfirmedObservationId);
        Assert.Equal(AmountStatus.Unpublished, selection.ConfirmedStatus);
        Assert.Equal(estimate.Id, selection.ReferenceObservationId);
    }

    [Fact]
    public void ResolvedConfirmedZero_SuppressesEstimateAndPreservesKnownZero()
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.BuyerInterest, "interest-2026-08");
        Append(item, CostValuationKind.Estimate, CostDirection.Charge, CostAmount.Known(500m, CurrencyCode.Jpy));
        var confirmed = Append(item, CostValuationKind.Confirmed, CostDirection.Charge, CostAmount.KnownZero(CurrencyCode.Jpy));

        var result = Calculate(context, currentPrice: 1_100m, [item]);

        Assert.Equal(AmountStatus.KnownZero, result.ConfirmedCosts.Status);
        Assert.Equal(0m, result.ConfirmedCosts.NetCost);
        Assert.Equal(AmountStatus.KnownZero, result.ReferenceCosts.Status);
        Assert.Equal(10_000m, result.ConfirmedCostAdjustedProfitAndLoss.Amount);
        Assert.Equal(10_000m, result.EstimatedNetProfitAndLoss.Amount);
        Assert.Equal(AmountStatus.KnownZero, result.CostToRRatio.Status);
        Assert.Equal(0m, result.CostToRRatio.Value);
        Assert.Equal([confirmed.Id], result.ReferenceCosts.CountedObservationIds);
    }

    [Fact]
    public void ConfirmedNotOccurred_SuppressesEstimateWithoutConvertingTheCostStateToKnownZero()
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.Backwardation, "backwardation-2026-08-28");
        Append(item, CostValuationKind.Estimate, CostDirection.Charge, CostAmount.Known(700m, CurrencyCode.Jpy));
        Append(item, CostValuationKind.Confirmed, CostDirection.Charge, CostAmount.NotOccurred());

        var result = Calculate(context, currentPrice: 1_100m, [item]);

        Assert.Equal(AmountStatus.NotOccurred, result.ConfirmedCosts.Status);
        Assert.Equal(AmountStatus.NotOccurred, result.ReferenceCosts.Status);
        Assert.Equal(0m, result.ReferenceCosts.NetCost);
        Assert.Equal(10_000m, result.EstimatedNetProfitAndLoss.Amount);
    }

    [Fact]
    public void Credit_IncreasesNetProfitAndProducesNegativeCostToR()
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.Other, "credit-2026-08");
        Append(item, CostValuationKind.Confirmed, CostDirection.Credit, CostAmount.Known(200m, CurrencyCode.Jpy));

        var result = Calculate(context, currentPrice: 1_100m, [item]);

        Assert.Equal(-200m, result.ReferenceCosts.NetCost);
        Assert.Equal(10_200m, result.EstimatedNetProfitAndLoss.Amount);
        Assert.Equal(-200m / 30_000m, result.CostToRRatio.Value);
    }

    [Theory]
    [InlineData(AmountStatus.Unpublished)]
    [InlineData(AmountStatus.FetchFailed)]
    [InlineData(AmountStatus.Unknown)]
    public void MissingEstimateState_IsNotConvertedToZero(AmountStatus status)
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.BrokerSpecific, $"missing-{status}");
        Append(item, CostValuationKind.Estimate, CostDirection.Charge, MissingAmount(status));

        var result = Calculate(context, currentPrice: 1_100m, [item]);

        Assert.Equal(status, result.ReferenceCosts.Status);
        Assert.Null(result.ReferenceCosts.NetCost);
        Assert.Null(result.EstimatedNetProfitAndLoss.Amount);
        Assert.Null(result.CostToRRatio.Value);
        Assert.Equal([status], result.ReferenceCosts.BlockingStatuses);
    }

    [Fact]
    public void MultipleMissingStates_ArePreservedWithoutInventingAnAggregateAmount()
    {
        var context = Context(PositionSide.Long);
        var unpublished = CostItem(context.Lot.Id, MarginCostType.Backwardation, "backwardation");
        Append(unpublished, CostValuationKind.Estimate, CostDirection.Charge, CostAmount.Unpublished());
        var failed = CostItem(context.Lot.Id, MarginCostType.BrokerSpecific, "broker-specific");
        Append(failed, CostValuationKind.Estimate, CostDirection.Charge, CostAmount.FetchFailed());

        var result = Calculate(context, currentPrice: 1_100m, [unpublished, failed]);

        Assert.Equal(AmountStatus.Unknown, result.ReferenceCosts.Status);
        Assert.Null(result.ReferenceCosts.NetCost);
        Assert.Equal(
            [AmountStatus.Unpublished, AmountStatus.FetchFailed],
            result.ReferenceCosts.BlockingStatuses.Order().ToArray());
    }

    [Fact]
    public void EmptyCostInput_IsUnknownRatherThanZero()
    {
        var context = Context(PositionSide.Long);

        var result = Calculate(context, currentPrice: 1_000m, []);

        Assert.Equal(AmountStatus.KnownZero, result.PriceProfitAndLoss.Status);
        Assert.Equal(0m, result.PriceProfitAndLoss.Amount);
        Assert.Equal(AmountStatus.Unknown, result.ConfirmedCosts.Status);
        Assert.Null(result.ConfirmedCosts.NetCost);
        Assert.Equal(AmountStatus.Unknown, result.ReferenceCosts.Status);
        Assert.Null(result.EstimatedNetProfitAndLoss.Amount);
        Assert.Null(result.CostToRRatio.Value);
    }

    [Fact]
    public void RejectsCurrentPriceWithDifferentShareUnitOrCurrency()
    {
        var context = Context(PositionSide.Long);
        var differentUnit = new RiskPriceUnit(
            context.Position.InstrumentId,
            CurrencyCode.Jpy,
            Hash('f'));

        Assert.Throws<DomainException>(() => LotProfitAndLossCalculator.Calculate(
            context.Bundle.RiskBasis,
            new UnitizedRiskPrice(new PositivePrice(1_100m), differentUnit),
            100m,
            []));
    }

    [Fact]
    public void RejectsCostsFromAnotherLotOrDuplicateLogicalOccurrence()
    {
        var context = Context(PositionSide.Long);
        var otherLotItem = CostItem(MarginLotId.New(), MarginCostType.BuyerInterest, "interest");
        var first = CostItem(context.Lot.Id, MarginCostType.BuyerInterest, "duplicate");
        var second = CostItem(context.Lot.Id, MarginCostType.BuyerInterest, "duplicate");

        Assert.Throws<DomainException>(() => Calculate(context, 1_100m, [otherLotItem]));
        Assert.Throws<DomainException>(() => Calculate(context, 1_100m, [first, second]));
    }

    [Fact]
    public void RejectsCostInAnotherCurrency()
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.BrokerSpecific, "usd-cost");
        Append(item, CostValuationKind.Confirmed, CostDirection.Charge, CostAmount.Known(10m, new CurrencyCode("USD")));

        Assert.Throws<DomainException>(() => Calculate(context, 1_100m, [item]));
    }

    [Fact]
    public void RejectsDefaultCostAmountRatherThanTreatingItAsZero()
    {
        var context = Context(PositionSide.Long);
        var item = CostItem(context.Lot.Id, MarginCostType.BrokerSpecific, "invalid-default");
        Append(item, CostValuationKind.Estimate, CostDirection.Charge, default);

        Assert.Throws<DomainException>(() => Calculate(context, 1_100m, [item]));
    }

    [Fact]
    public void ConvertsDecimalOverflowToDomainFailure()
    {
        var context = Context(PositionSide.Long);

        var exception = Assert.Throws<DomainException>(() => LotProfitAndLossCalculator.Calculate(
            context.Bundle.RiskBasis,
            new UnitizedRiskPrice(new PositivePrice(1_001m), context.Unit),
            decimal.MaxValue,
            []));

        Assert.IsType<OverflowException>(exception.InnerException);
    }

    [Fact]
    public void RejectsQuantityWhenTotalRiskRoundsToZero()
    {
        var context = Context(
            PositionSide.Long,
            fixedAtr: 0.0000000000000000000000000001m);

        Assert.Throws<DomainException>(() => LotProfitAndLossCalculator.Calculate(
            context.Bundle.RiskBasis,
            new UnitizedRiskPrice(new PositivePrice(1_001m), context.Unit),
            0.0000000000000000000000000001m,
            []));
    }

    private static LotProfitAndLossResult Calculate(
        TestContext context,
        decimal currentPrice,
        IReadOnlyCollection<MarginCostItem> items) =>
        LotProfitAndLossCalculator.Calculate(
            context.Bundle.RiskBasis,
            new UnitizedRiskPrice(new PositivePrice(currentPrice), context.Unit),
            100m,
            items);

    private static TestContext Context(PositionSide side, decimal fixedAtr = 100m)
    {
        var position = new Position(
            PositionId.New(),
            InstrumentId.New(),
            side,
            null,
            null,
            Now);
        var executionRevisionId = TradeExecutionRevisionId.New();
        var execution = position.RegisterUserConfirmedExecution(
            TradeExecutionId.New(),
            ExecutionKind.Open,
            new UserConfirmedExecutionInput(
                Now,
                new PositivePrice(1_000m),
                new WholeShareQuantity(100),
                CurrencyCode.Jpy,
                Now),
            executionRevisionId,
            Revision(executionRevisionId.Value, 'a'),
            Now);
        var lot = MarginLot.FromUserConfirmedOpening(MarginLotId.New(), execution, Now);
        var unit = new RiskPriceUnit(position.InstrumentId, CurrencyCode.Jpy, Hash('b'));
        var planId = Guid.NewGuid();
        var bundle = InitialRiskPlanBundle.Create(
            Guid.NewGuid(),
            Revision(planId, 'c'),
            position,
            lot,
            new UnitizedRiskPrice(new PositivePrice(1_000m), unit),
            new DateOnly(2026, 8, 27),
            new UnitizedRiskPrice(new PositivePrice(fixedAtr), unit),
            14,
            "wilder-atr-v1",
            RiskManagementParameters.Initial,
            Hash('d'),
            Now,
            Now);
        return new TestContext(position, lot, unit, bundle);
    }

    private static MarginCostItem CostItem(
        MarginLotId lotId,
        MarginCostType type,
        string occurrenceKey) =>
        new(
            MarginCostItemId.New(),
            lotId,
            type,
            occurrenceKey,
            new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            null,
            Now);

    private static MarginCostObservation Append(
        MarginCostItem item,
        CostValuationKind kind,
        CostDirection direction,
        CostAmount amount)
    {
        var id = MarginCostObservationId.New();
        var observation = new MarginCostObservation(
            id,
            item.Id,
            kind,
            direction,
            amount,
            null,
            null,
            null,
            null,
            null,
            kind == CostValuationKind.Estimate
                ? CostSourceKind.ApplicationEstimate
                : CostSourceKind.BrokerStatement,
            null,
            Revision(id.Value, kind == CostValuationKind.Estimate ? 'e' : 'f'),
            Now,
            kind == CostValuationKind.Confirmed ? Now : null);
        item.AppendObservation(observation);
        return observation;
    }

    private static CostAmount MissingAmount(AmountStatus status) => status switch
    {
        AmountStatus.Unpublished => CostAmount.Unpublished(),
        AmountStatus.FetchFailed => CostAmount.FetchFailed(),
        AmountStatus.Unknown => CostAmount.Unknown(),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static RevisionMetadata Revision(Guid id, char hashCharacter) =>
        new(id, 1, null, Hash(hashCharacter), Now);

    private static Sha256Hash Hash(char value) => new(new string(value, 64));

    private sealed record TestContext(
        Position Position,
        MarginLot Lot,
        RiskPriceUnit Unit,
        InitialRiskPlanBundle Bundle);
}
