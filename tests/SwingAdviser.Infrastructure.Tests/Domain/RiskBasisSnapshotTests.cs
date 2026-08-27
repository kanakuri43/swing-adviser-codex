using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.Positions;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class RiskBasisSnapshotTests
{
    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc = ExecutedAtUtc.AddMinutes(1);
    private static readonly DateOnly AtrReferenceBarDate = new(2026, 8, 26);

    [Fact]
    public void LongInitialPlan_FreezesConfiguredRiskAndUsesExactBasisLines()
    {
        var context = Context(PositionSide.Long, 1_000m);

        var result = CreateInitial(context, fixedAtr: 100m);

        Assert.Equal("initial-risk-plan-factory-v1", InitialRiskPlanBundle.FactoryVersion);
        Assert.Equal("risk-management-parameters-v1", RiskManagementParameters.SchemaVersion);
        Assert.Equal(PositionSide.Long, result.RiskBasis.Side);
        Assert.Equal(context.Unit, result.RiskBasis.PriceUnit);
        Assert.Equal(1_000m, result.RiskBasis.EntryBasisPrice.Value);
        Assert.Equal(100m, result.RiskBasis.FixedAtr.Value);
        Assert.Equal(14, result.RiskBasis.AtrPeriod);
        Assert.Equal("wilder-atr-v1", result.RiskBasis.AtrAlgorithmId);
        Assert.Equal(3.0m, result.RiskBasis.StopMultiplier);
        Assert.Equal(300m, result.RiskBasis.RiskAmountR);
        Assert.Equal(1.5m, result.RiskBasis.PartialTakeProfitRMultiple);
        Assert.Equal(0.50m, result.RiskBasis.PartialTakeProfitFraction);
        Assert.Equal(700m, result.RiskBasis.InitialStopPrice.Value);
        Assert.Equal(1_450m, result.RiskBasis.InitialTakeProfitPrice.Value);

        Assert.Equal(result.RiskBasis.Id, result.RiskPlan.RiskBasisSnapshotId);
        Assert.Equal(result.RiskBasis.InitialStopPrice, result.RiskPlan.StopPrice);
        Assert.Equal(result.RiskBasis.InitialTakeProfitPrice, result.RiskPlan.TakeProfitPrice);
        Assert.Equal(RiskPlanReason.Initial, result.RiskPlan.Reason);
        Assert.Equal(1, result.RiskPlan.Audit.RevisionNumber);
        Assert.Null(result.RiskPlan.Audit.SupersedesId);
        Assert.Null(result.RiskPlan.TriggerTradeExecutionId);
        Assert.Null(result.RiskPlan.TriggerLotAllocationRevisionId);
        Assert.Null(result.RiskPlan.TriggerPositionAdjustmentId);
        Assert.False(result.RiskPlan.IsCostAdjusted);
    }

    [Fact]
    public void ShortInitialPlan_UsesAsymmetricStopMultiplier()
    {
        var context = Context(PositionSide.Short, 1_000m);

        var result = CreateInitial(context, fixedAtr: 100m);

        Assert.Equal(2.5m, result.RiskBasis.StopMultiplier);
        Assert.Equal(250m, result.RiskBasis.RiskAmountR);
        Assert.Equal(1_250m, result.RiskBasis.InitialStopPrice.Value);
        Assert.Equal(625m, result.RiskBasis.InitialTakeProfitPrice.Value);
        Assert.Equal(result.RiskBasis.InitialStopPrice, result.RiskPlan.StopPrice);
        Assert.Equal(result.RiskBasis.InitialTakeProfitPrice, result.RiskPlan.TakeProfitPrice);
    }

    [Theory]
    [InlineData(PositionSide.Long, "300")]
    [InlineData(PositionSide.Long, "299")]
    [InlineData(PositionSide.Short, "375")]
    [InlineData(PositionSide.Short, "374")]
    public void InitialPlan_RejectsANonPositiveCalculatedPriceLine(
        PositionSide side,
        string entryPriceText)
    {
        var entryPrice = decimal.Parse(entryPriceText, System.Globalization.CultureInfo.InvariantCulture);
        var context = Context(side, entryPrice);

        Assert.Throws<DomainException>(() => CreateInitial(context, fixedAtr: 100m));
    }

    [Fact]
    public void UnitizedRiskPrice_RejectsDefaultPriceOrUnitValues()
    {
        var context = Context(PositionSide.Long, 1_000m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UnitizedRiskPrice(default, context.Unit));
        Assert.Throws<ArgumentException>(() =>
            new UnitizedRiskPrice(new PositivePrice(100m), default));
        Assert.Throws<ArgumentException>(() =>
            new RiskPriceUnit(context.Position.InstrumentId, default, context.Unit.BasisHash));
        Assert.Throws<ArgumentException>(() =>
            new RiskPriceUnit(context.Position.InstrumentId, CurrencyCode.Jpy, default));
    }

    [Fact]
    public void InitialPlan_RejectsCurrencyOrShareUnitMismatch()
    {
        var context = Context(PositionSide.Long, 1_000m);
        var differentCurrency = new RiskPriceUnit(
            context.Position.InstrumentId,
            new CurrencyCode("USD"),
            context.Unit.BasisHash);
        var differentShareUnit = new RiskPriceUnit(
            context.Position.InstrumentId,
            CurrencyCode.Jpy,
            Hash('d'));

        Assert.Throws<DomainException>(() => CreateInitial(
            context,
            fixedAtr: 100m,
            atrUnit: differentCurrency));
        Assert.Throws<DomainException>(() => CreateInitial(
            context,
            fixedAtr: 100m,
            atrUnit: differentShareUnit));
    }

    [Fact]
    public void InitialPlan_RejectsUnitForAnotherInstrument()
    {
        var context = Context(PositionSide.Long, 1_000m);
        var otherInstrumentUnit = new RiskPriceUnit(
            InstrumentId.New(),
            CurrencyCode.Jpy,
            context.Unit.BasisHash);

        Assert.Throws<DomainException>(() => CreateInitial(
            context,
            fixedAtr: 100m,
            entryUnit: otherInstrumentUnit,
            atrUnit: otherInstrumentUnit));
    }

    [Fact]
    public void InitialPlan_RejectsEntryValueDifferentFromOpeningExecution()
    {
        var context = Context(PositionSide.Long, 1_000m);

        Assert.Throws<DomainException>(() => InitialRiskPlanBundle.Create(
            Guid.NewGuid(),
            InitialPlanAudit(),
            context.Position,
            context.Lot,
            new UnitizedRiskPrice(new PositivePrice(1_001m), context.Unit),
            AtrReferenceBarDate,
            new UnitizedRiskPrice(new PositivePrice(100m), context.Unit),
            14,
            "wilder-atr-v1",
            RiskManagementParameters.Initial,
            Hash('b'),
            ExecutedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void InitialPlan_RejectsLotFromAnotherPosition()
    {
        var first = Context(PositionSide.Long, 1_000m);
        var second = Context(PositionSide.Long, 1_000m);

        Assert.Throws<DomainException>(() => InitialRiskPlanBundle.Create(
            Guid.NewGuid(),
            InitialPlanAudit(),
            first.Position,
            second.Lot,
            new UnitizedRiskPrice(new PositivePrice(1_000m), first.Unit),
            AtrReferenceBarDate,
            new UnitizedRiskPrice(new PositivePrice(100m), first.Unit),
            14,
            "wilder-atr-v1",
            RiskManagementParameters.Initial,
            Hash('b'),
            ExecutedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void InitialPlan_RequiresInitialRevisionMetadata()
    {
        var context = Context(PositionSide.Long, 1_000m);
        var planId = Guid.NewGuid();
        var nonInitialAudit = new RevisionMetadata(
            planId,
            2,
            Guid.NewGuid(),
            Hash('c'),
            CreatedAtUtc);

        Assert.Throws<DomainException>(() => CreateInitial(
            context,
            fixedAtr: 100m,
            initialPlanAudit: nonInitialAudit));
    }

    [Fact]
    public void InitialPlan_RejectsSupersededOpeningRevision()
    {
        var context = Context(PositionSide.Long, 1_000m);
        var execution = Assert.Single(context.Position.Executions);
        var priorRevision = execution.CurrentRevision;
        var correctionId = TradeExecutionRevisionId.New();
        execution.AppendCorrection(
            correctionId,
            new RevisionMetadata(
                correctionId.Value,
                2,
                priorRevision.Id.Value,
                Hash('d'),
                CreatedAtUtc),
            priorRevision.Input,
            "test correction");

        Assert.Throws<DomainException>(() => CreateInitial(context, fixedAtr: 100m));
    }

    [Fact]
    public void InitialPlan_RejectsUnsupportedSide()
    {
        var context = Context((PositionSide)99, 1_000m);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateInitial(context, fixedAtr: 100m));
    }

    [Fact]
    public void InitialPlan_ConvertsDecimalOverflowToDomainFailure()
    {
        var context = Context(PositionSide.Short, decimal.MaxValue);

        var exception = Assert.Throws<DomainException>(() =>
            CreateInitial(context, fixedAtr: decimal.MaxValue));

        Assert.IsType<OverflowException>(exception.InnerException);
    }

    private static InitialRiskPlanBundle CreateInitial(
        RiskContext context,
        decimal fixedAtr,
        RiskPriceUnit? entryUnit = null,
        RiskPriceUnit? atrUnit = null,
        RevisionMetadata? initialPlanAudit = null) =>
        InitialRiskPlanBundle.Create(
            Guid.NewGuid(),
            initialPlanAudit ?? InitialPlanAudit(),
            context.Position,
            context.Lot,
            new UnitizedRiskPrice(new PositivePrice(context.EntryPrice), entryUnit ?? context.Unit),
            AtrReferenceBarDate,
            new UnitizedRiskPrice(new PositivePrice(fixedAtr), atrUnit ?? context.Unit),
            14,
            "wilder-atr-v1",
            RiskManagementParameters.Initial,
            Hash('b'),
            ExecutedAtUtc,
            CreatedAtUtc);

    private static RiskContext Context(PositionSide side, decimal entryPrice)
    {
        var position = new Position(
            PositionId.New(),
            InstrumentId.New(),
            side,
            null,
            null,
            ExecutedAtUtc);
        var executionId = TradeExecutionId.New();
        var executionRevisionId = TradeExecutionRevisionId.New();
        var execution = position.RegisterUserConfirmedExecution(
            executionId,
            ExecutionKind.Open,
            new UserConfirmedExecutionInput(
                ExecutedAtUtc,
                new PositivePrice(entryPrice),
                new WholeShareQuantity(100),
                CurrencyCode.Jpy,
                ExecutedAtUtc),
            executionRevisionId,
            new RevisionMetadata(
                executionRevisionId.Value,
                1,
                null,
                Hash('a'),
                ExecutedAtUtc),
            ExecutedAtUtc);
        var lot = MarginLot.FromUserConfirmedOpening(
            MarginLotId.New(),
            execution,
            ExecutedAtUtc);
        var unit = new RiskPriceUnit(position.InstrumentId, CurrencyCode.Jpy, Hash('c'));
        return new RiskContext(position, lot, unit, entryPrice);
    }

    private static RevisionMetadata InitialPlanAudit()
    {
        var id = Guid.NewGuid();
        return new RevisionMetadata(id, 1, null, Hash('c'), CreatedAtUtc);
    }

    private static Sha256Hash Hash(char value) => new(new string(value, 64));

    private sealed record RiskContext(
        Position Position,
        MarginLot Lot,
        RiskPriceUnit Unit,
        decimal EntryPrice);
}
