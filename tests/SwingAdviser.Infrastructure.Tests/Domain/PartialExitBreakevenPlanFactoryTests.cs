using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.Positions;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class PartialExitBreakevenPlanFactoryTests
{
    private static readonly DateTimeOffset OpenedAtUtc =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(PositionSide.Long, 700, 1_000)]
    [InlineData(PositionSide.Short, 1_250, 1_000)]
    public void Create_AppendsSideSpecificBreakevenPlanWithExactEvidence(
        PositionSide side,
        decimal expectedPriorStop,
        decimal expectedStop)
    {
        var scenario = CreateScenario(side);
        var priorStop = scenario.Bundle.RiskPlan.StopPrice;
        var priorTarget = scenario.Bundle.RiskPlan.TakeProfitPrice;

        var result = CreatePlan(scenario);

        Assert.Equal(PartialExitBreakevenPlanFactory.FactoryVersion,
            "partial-exit-breakeven-plan-factory-v1");
        Assert.Equal(expectedPriorStop, priorStop.Value);
        Assert.Equal(expectedStop, result.StopPrice.Value);
        Assert.Equal(priorTarget, result.TakeProfitPrice);
        Assert.Equal(RiskPlanReason.PartialExitBreakeven, result.Reason);
        Assert.Equal(2, result.Audit.RevisionNumber);
        Assert.Equal(scenario.Bundle.RiskPlan.Audit.Id, result.Audit.SupersedesId);
        Assert.Equal(scenario.Close.Id, result.TriggerTradeExecutionId);
        Assert.Equal(scenario.Allocation.Audit.Id, result.TriggerLotAllocationRevisionId);
        Assert.Null(result.TriggerPositionAdjustmentId);
        Assert.Equal(scenario.Close.CurrentRevision.Input.ExecutedAtUtc, result.EffectiveAtUtc);
        Assert.False(result.IsCostAdjusted);

        Assert.Equal(priorStop, scenario.Bundle.RiskPlan.StopPrice);
        Assert.Equal(priorTarget, scenario.Bundle.RiskPlan.TakeProfitPrice);
        Assert.Single(scenario.Close.Revisions);
        Assert.Equal(80, scenario.Allocation.Quantity.Value);
    }

    [Theory]
    [InlineData(PositionSide.Long, 1_100)]
    [InlineData(PositionSide.Short, 900)]
    public void Create_NeverRelaxesAnAlreadyMoreFavorableStop(
        PositionSide side,
        decimal favorableStop)
    {
        var scenario = CreateScenario(side);
        var predecessor = NextUserPlan(
            scenario.Bundle,
            scenario.Bundle.RiskPlan,
            favorableStop,
            OpenedAtUtc.AddHours(2));

        var result = PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            predecessor,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            NextAudit(predecessor, OpenedAtUtc.AddHours(6)));

        Assert.Equal(favorableStop, result.StopPrice.Value);
        Assert.Equal(predecessor.TakeProfitPrice, result.TakeProfitPrice);
        Assert.Equal(predecessor.Audit.Id, result.Audit.SupersedesId);
    }

    [Fact]
    public void Create_UsesTheCurrentAdjustedEntryBasis()
    {
        var scenario = CreateScenario(PositionSide.Long);
        var convertedPredecessor = NextUserPlan(
            scenario.Bundle,
            scenario.Bundle.RiskPlan,
            470m,
            OpenedAtUtc.AddHours(2));

        var result = PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            convertedPredecessor,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            new PositivePrice(500m),
            200m,
            NextAudit(convertedPredecessor, OpenedAtUtc.AddHours(6)));

        Assert.Equal(500m, result.StopPrice.Value);
        Assert.Equal(convertedPredecessor.TakeProfitPrice, result.TakeProfitPrice);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(79)]
    public void Create_RejectsAFullOrOverAllocatedClose(decimal quantityBeforeClose)
    {
        var scenario = CreateScenario(PositionSide.Long);

        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            scenario.Bundle.RiskPlan,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            quantityBeforeClose,
            NextAudit(scenario.Bundle.RiskPlan, OpenedAtUtc.AddHours(6))));
    }

    [Fact]
    public void Create_RejectsAStaleAllocationAfterTheCloseIsCorrected()
    {
        var scenario = CreateScenario(PositionSide.Long);
        var correctedRevisionId = TradeExecutionRevisionId.New();
        scenario.Close.AppendCorrection(
            correctedRevisionId,
            new RevisionMetadata(
                correctedRevisionId.Value,
                2,
                scenario.Close.CurrentRevision.Id.Value,
                Hash('f'),
                OpenedAtUtc.AddHours(6)),
            new UserConfirmedExecutionInput(
                OpenedAtUtc.AddHours(4),
                new PositivePrice(1_205m),
                new WholeShareQuantity(80),
                CurrencyCode.Jpy,
                OpenedAtUtc.AddHours(6)),
            "broker correction");

        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            scenario.Bundle.RiskPlan,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            NextAudit(scenario.Bundle.RiskPlan, OpenedAtUtc.AddHours(7))));
    }

    [Fact]
    public void Create_RejectsAnotherLotOrRiskBasis()
    {
        var scenario = CreateScenario(PositionSide.Long);
        var another = CreateScenario(PositionSide.Long);

        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            scenario.Bundle.RiskPlan,
            another.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            NextAudit(scenario.Bundle.RiskPlan, OpenedAtUtc.AddHours(6))));
        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            another.Bundle.RiskPlan,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            NextAudit(another.Bundle.RiskPlan, OpenedAtUtc.AddHours(6))));
    }

    [Fact]
    public void Create_RejectsANonClosingExecution()
    {
        var scenario = CreateScenario(PositionSide.Long);
        var opening = scenario.Position.Executions.Single(x => x.Kind == ExecutionKind.Open);

        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            scenario.Bundle.RiskPlan,
            scenario.Lot,
            opening,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            NextAudit(scenario.Bundle.RiskPlan, OpenedAtUtc.AddHours(6))));
    }

    [Fact]
    public void Create_RejectsANonDirectRevisionOrEvidenceRecordedLater()
    {
        var scenario = CreateScenario(PositionSide.Long);
        var predecessor = scenario.Bundle.RiskPlan;
        var wrongPredecessor = new RevisionMetadata(
            Guid.NewGuid(),
            2,
            Guid.NewGuid(),
            Hash('f'),
            OpenedAtUtc.AddHours(6));
        var recordedTooEarly = new RevisionMetadata(
            Guid.NewGuid(),
            2,
            predecessor.Audit.Id,
            Hash('f'),
            OpenedAtUtc.AddHours(4));

        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            predecessor,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            wrongPredecessor));
        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            predecessor,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            recordedTooEarly));
    }

    [Fact]
    public void Create_RejectsACloseEffectiveBeforeTheCurrentPlan()
    {
        var scenario = CreateScenario(PositionSide.Long);
        var laterPlan = NextUserPlan(
            scenario.Bundle,
            scenario.Bundle.RiskPlan,
            900m,
            OpenedAtUtc.AddHours(5));

        Assert.Throws<DomainException>(() => PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            laterPlan,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            NextAudit(laterPlan, OpenedAtUtc.AddHours(6))));
    }

    [Fact]
    public void TargetPriceReachAlone_DoesNotAppendABreakevenPlan()
    {
        var scenario = CreateScenario(PositionSide.Long);
        var plans = new List<RiskPlanRevision> { scenario.Bundle.RiskPlan };
        var unit = scenario.Bundle.RiskBasis.PriceUnit;

        var result = LotRiskEvaluator.Evaluate(
            scenario.Bundle.RiskBasis,
            plans,
            new DateOnly(2026, 8, 28),
            OpenedAtUtc.AddHours(3),
            new UnitizedRiskPrice(scenario.Bundle.RiskPlan.TakeProfitPrice, unit),
            new UnitizedRiskPrice(new PositivePrice(900m), unit));

        Assert.Equal(ExitDecision.TakeProfit, result.Decision);
        Assert.Single(plans);
        Assert.Equal(RiskPlanReason.Initial, plans[0].Reason);
        Assert.Null(plans[0].TriggerTradeExecutionId);
        Assert.Null(plans[0].TriggerLotAllocationRevisionId);
    }

    private static RiskPlanRevision CreatePlan(Scenario scenario) =>
        PartialExitBreakevenPlanFactory.Create(
            scenario.Bundle.RiskBasis,
            scenario.Bundle.RiskPlan,
            scenario.Lot,
            scenario.Close,
            scenario.Allocation,
            scenario.Bundle.RiskBasis.EntryBasisPrice,
            200m,
            NextAudit(scenario.Bundle.RiskPlan, OpenedAtUtc.AddHours(6)));

    private static Scenario CreateScenario(PositionSide side)
    {
        var position = new Position(
            PositionId.New(),
            InstrumentId.New(),
            side,
            null,
            null,
            OpenedAtUtc);
        var opening = RegisterExecution(
            position,
            ExecutionKind.Open,
            1_000m,
            200,
            OpenedAtUtc,
            OpenedAtUtc.AddMinutes(1));
        var lot = MarginLot.FromUserConfirmedOpening(
            MarginLotId.New(),
            opening,
            OpenedAtUtc.AddMinutes(1));
        var unit = new RiskPriceUnit(position.InstrumentId, CurrencyCode.Jpy, Hash('b'));
        var initialPlanId = Guid.NewGuid();
        var bundle = InitialRiskPlanBundle.Create(
            Guid.NewGuid(),
            new RevisionMetadata(
                initialPlanId,
                1,
                null,
                Hash('c'),
                OpenedAtUtc.AddMinutes(2)),
            position,
            lot,
            new UnitizedRiskPrice(new PositivePrice(1_000m), unit),
            new DateOnly(2026, 8, 26),
            new UnitizedRiskPrice(new PositivePrice(100m), unit),
            14,
            "wilder-atr-v1",
            RiskManagementParameters.Initial,
            Hash('d'),
            OpenedAtUtc,
            OpenedAtUtc.AddMinutes(2));
        var close = RegisterExecution(
            position,
            ExecutionKind.Close,
            side == PositionSide.Long ? 1_200m : 800m,
            80,
            OpenedAtUtc.AddHours(4),
            OpenedAtUtc.AddHours(5));
        var allocationId = Guid.NewGuid();
        var allocation = LotAllocationRevision.RegisterUserConfirmed(
            Guid.NewGuid(),
            close,
            lot,
            new WholeShareQuantity(80),
            new RevisionMetadata(
                allocationId,
                1,
                null,
                Hash('e'),
                OpenedAtUtc.AddHours(5)),
            OpenedAtUtc.AddHours(5));
        return new Scenario(position, lot, bundle, close, allocation);
    }

    private static TradeExecution RegisterExecution(
        Position position,
        ExecutionKind kind,
        decimal price,
        long quantity,
        DateTimeOffset executedAtUtc,
        DateTimeOffset confirmedAtUtc)
    {
        var executionId = TradeExecutionId.New();
        var revisionId = TradeExecutionRevisionId.New();
        return position.RegisterUserConfirmedExecution(
            executionId,
            kind,
            new UserConfirmedExecutionInput(
                executedAtUtc,
                new PositivePrice(price),
                new WholeShareQuantity(quantity),
                CurrencyCode.Jpy,
                confirmedAtUtc),
            revisionId,
            new RevisionMetadata(
                revisionId.Value,
                1,
                null,
                Hash('a'),
                confirmedAtUtc),
            confirmedAtUtc);
    }

    private static RiskPlanRevision NextUserPlan(
        InitialRiskPlanBundle bundle,
        RiskPlanRevision predecessor,
        decimal stopPrice,
        DateTimeOffset effectiveAndRecordedAtUtc)
    {
        var id = Guid.NewGuid();
        return new RiskPlanRevision(
            bundle.RiskBasis.Id,
            new RevisionMetadata(
                id,
                predecessor.Audit.RevisionNumber + 1,
                predecessor.Audit.Id,
                Hash('f'),
                effectiveAndRecordedAtUtc),
            new PositivePrice(stopPrice),
            predecessor.TakeProfitPrice,
            RiskPlanReason.UserCorrection,
            effectiveAndRecordedAtUtc);
    }

    private static RevisionMetadata NextAudit(
        RiskPlanRevision predecessor,
        DateTimeOffset recordedAtUtc)
    {
        var id = Guid.NewGuid();
        return new RevisionMetadata(
            id,
            predecessor.Audit.RevisionNumber + 1,
            predecessor.Audit.Id,
            Hash('f'),
            recordedAtUtc);
    }

    private static Sha256Hash Hash(char value) => new(new string(value, 64));

    private sealed record Scenario(
        Position Position,
        MarginLot Lot,
        InitialRiskPlanBundle Bundle,
        TradeExecution Close,
        LotAllocationRevision Allocation);
}
