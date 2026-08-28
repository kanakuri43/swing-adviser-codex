using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;
using SwingAdviser.Infrastructure.TradingWorkspace;

namespace SwingAdviser.Infrastructure.Tests.TradingWorkspace;

public sealed class SqliteTradingWorkspaceRepositoryTests
{
    [Fact]
    public async Task Opening_AppendsFrozenRiskBasisAndInitialPlanInTheSameRegistration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));

        var opening = await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, fixture.LongCandidateId, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 1000m, 100, "JPY", Utc(2), true, []));

        await using var context = new SwingAdviserDbContext(fixture.Options);
        var lot = Assert.Single(await context.Set<MarginLotRow>().ToListAsync());
        var basis = Assert.Single(await context.Set<RiskBasisSnapshotRow>().ToListAsync());
        var plan = Assert.Single(await context.Set<RiskPlanRevisionRow>().ToListAsync());
        Assert.Equal(lot.Id, basis.MarginLotId);
        Assert.Equal(opening.RevisionId, basis.OpeningTradeExecutionRevisionId);
        Assert.Equal(fixture.LongCandidateId, basis.OriginCandidateResultId);
        Assert.Equal(fixture.StrategySnapshotId, basis.StrategyParameterSnapshotId);
        Assert.Equal(fixture.ManifestId, basis.AnalysisInputManifestId);
        Assert.Equal("JPY", basis.PriceCurrency);
        Assert.Matches("^[0-9a-f]{64}$", basis.PriceUnitBasisSha256!);
        Assert.Equal(new DateOnly(2026, 8, 25), basis.AtrReferenceBarDate);
        Assert.Equal(20m, basis.FixedAtr);
        Assert.Equal(14, basis.AtrPeriod);
        Assert.Equal(TechnicalIndicatorEngine.AtrAlgorithmId, basis.AtrAlgorithmId);
        Assert.Equal(940m, basis.InitialStopPrice);
        Assert.Equal(1090m, basis.InitialTakeProfitPrice);
        Assert.Equal(basis.Id, plan.RiskBasisSnapshotId);
        Assert.Equal(RiskPlanReason.Initial.ToString(), plan.PlanReason);
        Assert.Equal(basis.InitialStopPrice, plan.StopPrice);
        Assert.Equal(basis.InitialTakeProfitPrice, plan.TakeProfitPrice);
    }

    [Fact]
    public async Task ManualOpening_IgnoresAnalysisRecordedAfterExecution()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddAtrAnalysisAsync(new DateOnly(2026, 8, 26), Utc(3), 999m, PositionSide.Long);
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));

        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 4000m, 100, "JPY", Utc(2), true, []));

        await using var context = new SwingAdviserDbContext(fixture.Options);
        var basis = Assert.Single(await context.Set<RiskBasisSnapshotRow>().ToListAsync());
        Assert.Equal(20m, basis.FixedAtr);
        Assert.Equal(new DateOnly(2026, 8, 25), basis.AtrReferenceBarDate);
    }

    [Fact]
    public async Task InvalidRiskLine_RollsBackTheWholeOpeningGraph()
    {
        await using var fixture = await Fixture.CreateAsync(seedAnalysis: false);
        await fixture.AddAtrAnalysisAsync(new DateOnly(2026, 8, 25), Utc(0), 500m, PositionSide.Long);
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));

        await Assert.ThrowsAsync<DomainException>(() => service.RegisterManualExecutionAsync(
            new RegisterManualExecutionRequest(
                fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
                Utc(1), 1000m, 100, "JPY", Utc(2), true, [])));

        await using var context = new SwingAdviserDbContext(fixture.Options);
        Assert.Empty(await context.Set<PositionRow>().ToListAsync());
        Assert.Empty(await context.Set<TradeExecutionRow>().ToListAsync());
        Assert.Empty(await context.Set<MarginLotRow>().ToListAsync());
        Assert.Empty(await context.Set<RiskBasisSnapshotRow>().ToListAsync());
        Assert.Empty(await context.Set<RiskPlanRevisionRow>().ToListAsync());
    }

    [Fact]
    public async Task OpeningWithoutEligibleAtr_IsRejectedWithoutSavingAPosition()
    {
        await using var fixture = await Fixture.CreateAsync(seedAnalysis: false);
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterManualExecutionAsync(
            new RegisterManualExecutionRequest(
                fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
                Utc(1), 1000m, 100, "JPY", Utc(2), true, [])));

        await using var context = new SwingAdviserDbContext(fixture.Options);
        Assert.Empty(await context.Set<PositionRow>().ToListAsync());
    }

    [Fact]
    public async Task OpenExecution_IsSavedOnlyThroughManualUseCase_AndCanBeCorrectedAsRevision()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));

        var registered = await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId,
            null,
            null,
            PositionSide.Long,
            ExecutionKind.Open,
            Utc(1),
            2450m,
            100,
            "JPY",
            Utc(2),
            true,
            [],
            Broker: "Test Broker",
            UserNote: "約定通知を確認"));

        var afterRegister = await service.LoadAsync();
        var position = Assert.Single(afterRegister.Positions);
        var execution = Assert.Single(afterRegister.Executions);
        Assert.Equal(registered.PositionId, position.PositionId);
        Assert.Equal(100m, position.Quantity);
        Assert.Equal(2450m, position.EntryBasisPrice);
        Assert.Equal(ExecutionOrigin.UserConfirmed, execution.Origin);
        Assert.Equal(ExecutionChangeKind.Initial, execution.CurrentRevision.ChangeKind);
        Assert.Equal("7203", execution.Code);

        await service.CorrectManualExecutionAsync(new CorrectManualExecutionRequest(
            execution.ExecutionId,
            execution.CurrentRevision.RevisionId,
            Utc(1),
            2455m,
            100,
            "JPY",
            Utc(3),
            true,
            "証券会社の約定通知を再確認",
            Broker: "Test Broker"));

        var afterCorrection = await service.LoadAsync();
        var corrected = Assert.Single(afterCorrection.Executions);
        Assert.Equal(2, corrected.Revisions.Count);
        Assert.Equal(ExecutionChangeKind.Correction, corrected.CurrentRevision.ChangeKind);
        Assert.Equal(2455m, corrected.CurrentRevision.Price);
        Assert.Equal(ReconciliationStatus.Required, Assert.Single(afterCorrection.Positions).ReconciliationStatus);
    }

    [Fact]
    public async Task CloseExecution_RejectsUnselectedOrOverAllocatedLot_AndPersistsExplicitAllocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));
        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Short, ExecutionKind.Open,
            Utc(1), 3000m, 200, "JPY", Utc(2), true, []));
        var openSnapshot = await service.LoadAsync();
        var position = Assert.Single(openSnapshot.Positions);
        var lot = Assert.Single(position.Lots);

        var invalid = new RegisterManualExecutionRequest(
            fixture.InstrumentId,
            position.PositionId,
            null,
            PositionSide.Short,
            ExecutionKind.Close,
            Utc(4),
            2800m,
            201,
            "JPY",
            Utc(5),
            true,
            [new ManualLotAllocation(lot.MarginLotId, 201)]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterManualExecutionAsync(invalid));

        var closing = await service.RegisterManualExecutionAsync(invalid with
        {
            Quantity = 80,
            LotAllocations = [new ManualLotAllocation(lot.MarginLotId, 80)],
        });

        var afterClose = await service.LoadAsync();
        Assert.Equal(120m, Assert.Single(afterClose.Positions).Quantity);
        Assert.Equal(2, afterClose.Executions.Count);

        await using var context = new SwingAdviserDbContext(fixture.Options);
        var allocation = Assert.Single(await context.Set<LotAllocationRevisionRow>().ToListAsync());
        Assert.Equal(lot.MarginLotId, allocation.MarginLotId);
        Assert.Equal(80, allocation.Quantity);
        var plans = await context.Set<RiskPlanRevisionRow>()
            .OrderBy(x => x.RevisionNo)
            .ToListAsync();
        Assert.Equal(2, plans.Count);
        Assert.Equal(RiskPlanReason.Initial.ToString(), plans[0].PlanReason);
        Assert.Equal(3050m, plans[0].StopPrice);
        Assert.Equal(2925m, plans[0].TakeProfitPrice);
        Assert.Equal(RiskPlanReason.PartialExitBreakeven.ToString(), plans[1].PlanReason);
        Assert.Equal(2, plans[1].RevisionNo);
        Assert.Equal(plans[0].Id, plans[1].SupersedesId);
        Assert.Equal(3000m, plans[1].StopPrice);
        Assert.Equal(plans[0].TakeProfitPrice, plans[1].TakeProfitPrice);
        Assert.Equal(closing.ExecutionId, plans[1].TriggerTradeExecutionId);
        Assert.Equal(allocation.Id, plans[1].TriggerLotAllocationRevisionId);
        Assert.Equal(Utc(4), plans[1].EffectiveAtUtc);
        Assert.False(plans[1].IsCostAdjusted);
    }

    [Fact]
    public async Task CorrectingClosedExecution_PreservesClosedStateAndMarksReconciliationRequired()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));
        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 1000m, 100, "JPY", Utc(2), true, []));
        var position = Assert.Single((await service.LoadAsync()).Positions);
        var lot = Assert.Single(position.Lots);
        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, position.PositionId, null, PositionSide.Long, ExecutionKind.Close,
            Utc(4), 1100m, 100, "JPY", Utc(5), true,
            [new ManualLotAllocation(lot.MarginLotId, 100)]));
        var close = (await service.LoadAsync()).Executions.Single(x => x.Kind == ExecutionKind.Close);

        await service.CorrectManualExecutionAsync(new CorrectManualExecutionRequest(
            close.ExecutionId, close.CurrentRevision.RevisionId, Utc(4), 1101m, 100, "JPY",
            Utc(6), true, "約定通知を再確認"));

        await using var context = new SwingAdviserDbContext(fixture.Options);
        var state = await context.Set<PositionStateRevisionRow>()
            .Where(x => x.PositionId == position.PositionId)
            .OrderByDescending(x => x.RevisionNo)
            .FirstAsync();
        Assert.Equal(PositionStatus.Closed.ToString(), state.Status);
        Assert.Equal(ReconciliationStatus.Required.ToString(), state.ReconciliationStatus);
        Assert.Single(await context.Set<RiskPlanRevisionRow>().ToListAsync());
        Assert.Empty((await service.LoadAsync()).Positions);
    }

    [Fact]
    public async Task InvalidBreakevenTransition_RollsBackCloseAllocationAndPlanTogether()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));
        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 1000m, 200, "JPY", Utc(2), true, []));
        var position = Assert.Single((await service.LoadAsync()).Positions);
        var lot = Assert.Single(position.Lots);

        await using (var context = new SwingAdviserDbContext(fixture.Options))
        {
            var basis = Assert.Single(await context.Set<RiskBasisSnapshotRow>().ToListAsync());
            var initial = Assert.Single(await context.Set<RiskPlanRevisionRow>().ToListAsync());
            context.Add(new RiskPlanRevisionRow
            {
                Id = Guid.NewGuid(),
                RevisionNo = 2,
                SupersedesId = initial.Id,
                ContentSha256 = new string('f', 64),
                RecordedAtUtc = Utc(3),
                RiskBasisSnapshotId = basis.Id,
                StopPrice = 950m,
                TakeProfitPrice = initial.TakeProfitPrice,
                PlanReason = RiskPlanReason.UserCorrection.ToString(),
                EffectiveAtUtc = Utc(6),
                IsCostAdjusted = false,
            });
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<DomainException>(() => service.RegisterManualExecutionAsync(
            new RegisterManualExecutionRequest(
                fixture.InstrumentId, position.PositionId, null, PositionSide.Long, ExecutionKind.Close,
                Utc(4), 1100m, 80, "JPY", Utc(5), true,
                [new ManualLotAllocation(lot.MarginLotId, 80)])));

        await using var verification = new SwingAdviserDbContext(fixture.Options);
        Assert.Single(await verification.Set<TradeExecutionRow>()
            .Where(x => x.ExecutionKind == ExecutionKind.Open.ToString())
            .ToListAsync());
        Assert.Empty(await verification.Set<TradeExecutionRow>()
            .Where(x => x.ExecutionKind == ExecutionKind.Close.ToString())
            .ToListAsync());
        Assert.Empty(await verification.Set<LotAllocationRevisionRow>().ToListAsync());
        Assert.Equal(2, await verification.Set<RiskPlanRevisionRow>().CountAsync());
    }

    [Fact]
    public async Task PositionProjectionAndLotValidation_UseAppliedCorporateActionUnits()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));
        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 1000m, 100, "JPY", Utc(2), true, []));
        var beforeAdjustment = Assert.Single((await service.LoadAsync()).Positions);
        var lot = Assert.Single(beforeAdjustment.Lots);

        await using (var context = new SwingAdviserDbContext(fixture.Options))
        {
            var actionId = Guid.NewGuid();
            var actionRevisionId = Guid.NewGuid();
            context.Add(new CorporateActionRow
            {
                Id = actionId,
                InstrumentId = fixture.InstrumentId,
                Provider = "Test",
                SourceEventId = "split-2-for-1",
                DerivedEventKey = "split-2-for-1",
                CreatedAtUtc = Utc(3),
            });
            context.Add(new CorporateActionRevisionRow
            {
                Id = actionRevisionId,
                RevisionNo = 1,
                ContentSha256 = new string('c', 64),
                AvailableAtUtc = Utc(3),
                AvailabilityStatus = "Known",
                FirstObservedAtUtc = Utc(3),
                RecordedAtUtc = Utc(3),
                CorporateActionId = actionId,
                ActionType = CorporateActionType.Split.ToString(),
                Status = CorporateActionStatus.Confirmed.ToString(),
                EffectiveDate = new DateOnly(2026, 8, 26),
                RatioNumerator = 2,
                RatioDenominator = 1,
                PointInTimeStatus = PointInTimeStatus.Verified.ToString(),
            });
            var adjustment = new PositionAdjustmentRow
            {
                Id = Guid.NewGuid(),
                AdjustmentKey = Guid.NewGuid(),
                RevisionNo = 1,
                PositionId = beforeAdjustment.PositionId,
                MarginLotId = lot.MarginLotId,
                CorporateActionRevisionId = actionRevisionId,
                Status = PositionAdjustmentStatus.Applied.ToString(),
                EffectiveDate = new DateOnly(2026, 8, 26),
                QuantityFactor = 2m,
                PriceFactor = 0.5m,
                BeforeQuantity = 100m,
                AfterQuantity = 200m,
                BeforeBasisPrice = 1000m,
                AfterBasisPrice = 500m,
                DetailsJson = "{}",
                ConfirmedAtUtc = Utc(3),
                ContentSha256 = new string('d', 64),
                RecordedAtUtc = Utc(3),
            };
            context.Add(adjustment);
            var basis = Assert.Single(await context.Set<RiskBasisSnapshotRow>().ToListAsync());
            var initialPlan = Assert.Single(await context.Set<RiskPlanRevisionRow>().ToListAsync());
            context.Add(new RiskPlanRevisionRow
            {
                Id = Guid.NewGuid(),
                RevisionNo = 2,
                SupersedesId = initialPlan.Id,
                ContentSha256 = new string('e', 64),
                RecordedAtUtc = Utc(3),
                RiskBasisSnapshotId = basis.Id,
                StopPrice = 470m,
                TakeProfitPrice = 545m,
                TriggerPositionAdjustmentId = adjustment.Id,
                PlanReason = RiskPlanReason.CorporateActionConversion.ToString(),
                EffectiveAtUtc = Utc(3),
                IsCostAdjusted = false,
            });
            await context.SaveChangesAsync();
        }

        var adjusted = Assert.Single((await service.LoadAsync()).Positions);
        Assert.Equal(200m, adjusted.Quantity);
        Assert.Equal(500m, adjusted.EntryBasisPrice);
        Assert.Equal(200m, Assert.Single(adjusted.Lots).RemainingQuantity);

        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, adjusted.PositionId, null, PositionSide.Long, ExecutionKind.Close,
            Utc(4), 550m, 150, "JPY", Utc(5), true,
            [new ManualLotAllocation(lot.MarginLotId, 150)]));
        Assert.Equal(50m, Assert.Single((await service.LoadAsync()).Positions).Quantity);
        await using var verification = new SwingAdviserDbContext(fixture.Options);
        var plans = await verification.Set<RiskPlanRevisionRow>()
            .OrderBy(x => x.RevisionNo)
            .ToListAsync();
        Assert.Equal(3, plans.Count);
        Assert.Equal(500m, plans[2].StopPrice);
        Assert.Equal(545m, plans[2].TakeProfitPrice);
        Assert.Equal(plans[1].Id, plans[2].SupersedesId);
        Assert.Equal(RiskPlanReason.PartialExitBreakeven.ToString(), plans[2].PlanReason);
    }

    [Fact]
    public async Task Load_ProjectsPinnedEvaluationAndCostFields_WithoutChangingPositionGraph()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));
        var opening = await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 900m, 100, "JPY", Utc(2), true, []));
        await fixture.AddPositionEvaluationAsync(
            opening.PositionId,
            PositionEvaluationOutcome.Evaluated,
            ExitDecision.TakeProfit,
            currentQuantity: 100m,
            pricePnl: 10_000m,
            confirmedCostPnl: 9_700m,
            estimatedNetPnl: 9_600m,
            costToRRatio: 0.02m,
            partialExitQuantity: 50,
            partialExitStatus: PartialExitStatus.Candidate);
        await fixture.AddUnlinkedLaterPriceAsync(1100m);

        var countsBeforeLoad = await ReadGraphCountsAsync(fixture.Options);
        var item = Assert.Single((await service.LoadAsync()).Positions);
        var countsAfterLoad = await ReadGraphCountsAsync(fixture.Options);

        Assert.Equal(countsBeforeLoad, countsAfterLoad);
        Assert.Equal(100m, item.Quantity);
        Assert.Equal(1000m, item.CurrentPrice);
        Assert.Equal(new DateOnly(2026, 8, 25), item.EvaluationBarDate);
        Assert.Equal(PositionEvaluationOutcome.Evaluated, item.EvaluationOutcome);
        Assert.False(item.IsEvaluationStale);
        Assert.Equal(ExitDecision.TakeProfit, item.Decision);
        Assert.Equal(10_000m, item.PriceProfitAndLoss);
        Assert.Equal(9_700m, item.ConfirmedCostProfitAndLoss);
        Assert.Equal(9_600m, item.EstimatedNetProfitAndLoss);
        Assert.Equal(0.02m, item.CostToRRatio);
        Assert.Equal(50, item.PartialExitQuantity);
        Assert.Equal(PartialExitStatus.Candidate, item.PartialExitStatus);
        Assert.Equal(ReconciliationStatus.Clear, item.ReconciliationStatus);
    }

    [Fact]
    public async Task Load_MarksEvaluationStaleAndRemovesExitSuggestion_AfterUserConfirmedPartialClose()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));
        var opening = await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 1000m, 100, "JPY", Utc(2), true, []));
        await fixture.AddPositionEvaluationAsync(
            opening.PositionId,
            PositionEvaluationOutcome.Evaluated,
            ExitDecision.TakeProfit,
            currentQuantity: 100m,
            pricePnl: 0m,
            confirmedCostPnl: 0m,
            estimatedNetPnl: 0m,
            costToRRatio: 0m,
            partialExitQuantity: 50,
            partialExitStatus: PartialExitStatus.Candidate);
        var lot = Assert.Single(Assert.Single((await service.LoadAsync()).Positions).Lots);

        await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, opening.PositionId, null, PositionSide.Long, ExecutionKind.Close,
            Utc(4), 1050m, 40, "JPY", Utc(5), true,
            [new ManualLotAllocation(lot.MarginLotId, 40)]));

        var item = Assert.Single((await service.LoadAsync()).Positions);

        Assert.Equal(60m, item.Quantity);
        Assert.Equal(PositionEvaluationOutcome.Evaluated, item.EvaluationOutcome);
        Assert.True(item.IsEvaluationStale);
        Assert.Null(item.Decision);
        Assert.Contains("参考表示", item.DecisionReason, StringComparison.Ordinal);
        Assert.Equal(PartialExitStatus.Candidate, item.PartialExitStatus);
    }

    [Fact]
    public async Task Load_ProjectsReconciliationRequiredEvaluation_FailClosed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(fixture.Options));
        var opening = await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            fixture.InstrumentId, null, null, PositionSide.Long, ExecutionKind.Open,
            Utc(1), 1000m, 100, "JPY", Utc(2), true, []));
        await fixture.AddPositionEvaluationAsync(
            opening.PositionId,
            PositionEvaluationOutcome.ReconciliationRequired,
            null,
            currentQuantity: null,
            pricePnl: null,
            confirmedCostPnl: null,
            estimatedNetPnl: null,
            costToRRatio: null,
            partialExitQuantity: null,
            partialExitStatus: PartialExitStatus.NotApplicable);

        var item = Assert.Single((await service.LoadAsync()).Positions);

        Assert.Equal(PositionEvaluationOutcome.ReconciliationRequired, item.EvaluationOutcome);
        Assert.False(item.IsEvaluationStale);
        Assert.Null(item.Decision);
        Assert.Equal(ReconciliationStatus.Required, item.ReconciliationStatus);
    }

    private static async Task<WorkspaceGraphCounts> ReadGraphCountsAsync(DbContextOptions<SwingAdviserDbContext> options)
    {
        await using var context = new SwingAdviserDbContext(options);
        return new WorkspaceGraphCounts(
            await context.Set<PositionRow>().CountAsync(),
            await context.Set<TradeExecutionRow>().CountAsync(),
            await context.Set<TradeExecutionRevisionRow>().CountAsync(),
            await context.Set<MarginLotRow>().CountAsync(),
            await context.Set<LotAllocationRevisionRow>().CountAsync(),
            await context.Set<RiskPlanRevisionRow>().CountAsync(),
            await context.Set<PositionEvaluationRow>().CountAsync());
    }

    private sealed record WorkspaceGraphCounts(
        int Positions,
        int Executions,
        int ExecutionRevisions,
        int Lots,
        int Allocations,
        int RiskPlans,
        int Evaluations);

    private static DateTimeOffset Utc(int hour) =>
        new(2026, 8, 26, hour, 0, 0, TimeSpan.Zero);

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            SqliteConnection connection,
            DbContextOptions<SwingAdviserDbContext> options,
            Guid instrumentId,
            Guid strategySnapshotId,
            Guid manifestId,
            Guid longCandidateId)
        {
            Connection = connection;
            Options = options;
            InstrumentId = instrumentId;
            StrategySnapshotId = strategySnapshotId;
            ManifestId = manifestId;
            LongCandidateId = longCandidateId;
        }

        private SqliteConnection Connection { get; }
        public DbContextOptions<SwingAdviserDbContext> Options { get; }
        public Guid InstrumentId { get; }
        public Guid StrategySnapshotId { get; }
        public Guid ManifestId { get; }
        public Guid LongCandidateId { get; }

        public static async Task<Fixture> CreateAsync(bool seedAnalysis = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
                .UseSwingAdviserSqlite(connection)
                .Options;
            await using var context = new SwingAdviserDbContext(options);
            await context.Database.MigrateAsync();

            var now = Utc(0);
            var instrumentId = Guid.NewGuid();
            var identifierId = Guid.NewGuid();
            context.Add(new InstrumentRow { Id = instrumentId, CreatedAtUtc = now });
            context.Add(new InstrumentIdentifierRow
            {
                Id = identifierId,
                InstrumentId = instrumentId,
                Scheme = "JPX",
                CreatedAtUtc = now,
            });
            context.Add(new InstrumentIdentifierRevisionRow
            {
                Id = Guid.NewGuid(),
                RevisionNo = 1,
                ContentSha256 = new string('a', 64),
                AvailableAtUtc = now,
                AvailabilityStatus = "Known",
                FirstObservedAtUtc = now,
                RecordedAtUtc = now,
                InstrumentIdentifierId = identifierId,
                Value = "7203",
                RecordDisposition = RecordDisposition.Effective.ToString(),
                ChangeKind = "Initial",
            });
            context.Add(new InstrumentMasterRevisionRow
            {
                Id = Guid.NewGuid(),
                RevisionNo = 1,
                ContentSha256 = new string('b', 64),
                AvailableAtUtc = now,
                AvailabilityStatus = "Known",
                FirstObservedAtUtc = now,
                RecordedAtUtc = now,
                InstrumentId = instrumentId,
                Provider = "Development",
                EffectiveFromDate = new DateOnly(2026, 1, 1),
                Name = "トヨタ自動車",
                ExchangeCode = "TSE",
                MarketSegment = "Prime",
                SecurityType = SecurityType.DomesticCommonStock.ToString(),
                TradingUnit = 100,
                Currency = "JPY",
                ListingStatus = ListingStatus.Listed.ToString(),
                ScanEligibility = ScanEligibility.Eligible.ToString(),
                ChangeKind = "EffectiveSnapshot",
            });
            await context.SaveChangesAsync();

            Guid strategyId = Guid.Empty;
            Guid manifestId = Guid.Empty;
            Guid candidateId = Guid.Empty;
            if (seedAnalysis)
            {
                (strategyId, manifestId, candidateId) = await AddAtrAnalysisAsync(
                    context,
                    instrumentId,
                    new DateOnly(2026, 8, 25),
                    now,
                    20m,
                    PositionSide.Long,
                    includeCandidate: true);
                await AddAtrAnalysisAsync(
                    context,
                    instrumentId,
                    new DateOnly(2026, 8, 25),
                    now,
                    20m,
                    PositionSide.Short,
                    includeCandidate: false);
            }

            return new Fixture(connection, options, instrumentId, strategyId, manifestId, candidateId);
        }

        public async Task AddAtrAnalysisAsync(
            DateOnly evaluationDate,
            DateTimeOffset analyzedAtUtc,
            decimal atr,
            PositionSide side)
        {
            await using var context = new SwingAdviserDbContext(Options);
            await AddAtrAnalysisAsync(context, InstrumentId, evaluationDate, analyzedAtUtc, atr, side, false);
        }

        public async Task AddPositionEvaluationAsync(
            Guid positionId,
            PositionEvaluationOutcome outcome,
            ExitDecision? decision,
            decimal? currentQuantity,
            decimal? pricePnl,
            decimal? confirmedCostPnl,
            decimal? estimatedNetPnl,
            decimal? costToRRatio,
            long? partialExitQuantity,
            PartialExitStatus partialExitStatus)
        {
            await using var context = new SwingAdviserDbContext(Options);
            var analysisManifest = await context.Set<AnalysisInputManifestRow>()
                .SingleAsync(x => x.Id == ManifestId);
            var priceRevisionId = await context.Set<PriceRevisionSetChangeRow>()
                .Where(x => x.PriceRevisionSetId == analysisManifest.PriceRevisionSetId)
                .Select(x => x.DailyPriceRevisionId)
                .SingleAsync() ?? throw new InvalidOperationException("The analysis manifest has no selected price revision.");
            var lotIds = await context.Set<MarginLotRow>()
                .Where(x => x.PositionId == positionId)
                .Select(x => x.Id)
                .ToListAsync();
            var riskPlanIds = await context.Set<RiskPlanRevisionRow>()
                .Where(x => context.Set<RiskBasisSnapshotRow>()
                    .Where(basis => lotIds.Contains(basis.MarginLotId))
                    .Select(basis => basis.Id)
                    .Contains(x.RiskBasisSnapshotId))
                .Select(x => x.Id)
                .ToListAsync();
            var manifestId = Guid.NewGuid();
            context.Add(new PositionEvaluationInputManifestRow
            {
                Id = manifestId,
                AnalysisRunId = analysisManifest.AnalysisRunId,
                PositionId = positionId,
                AnalysisInputManifestId = analysisManifest.Id,
                CurrentPriceRevisionId = priceRevisionId,
                TradeExecutionRevisionIdsJson = "[]",
                LotAllocationRevisionIdsJson = "[]",
                PositionAdjustmentIdsJson = "[]",
                ContractRevisionIdsJson = "[]",
                RiskBasisSnapshotIdsJson = "[]",
                RiskPlanRevisionIdsJson = ExactIdsJson(riskPlanIds),
                MarginCostObservationIdsJson = "[]",
                ProjectionVersion = "test",
                RecordedCutoffAtUtc = Utc(6),
                ManifestSha256 = new string('f', 64),
                CreatedAtUtc = Utc(6),
            });
            context.Add(new PositionEvaluationRow
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = analysisManifest.AnalysisRunId,
                PositionId = positionId,
                PositionEvaluationInputManifestId = manifestId,
                EvaluationBarDate = new DateOnly(2026, 8, 25),
                EvaluationOutcome = outcome.ToString(),
                ExitDecision = decision?.ToString(),
                ReasonSummary = "保有再評価のテスト結果です。",
                ReasonsJson = "[]",
                LotEvaluationsJson = "[]",
                CurrentQuantity = currentQuantity,
                PricePnl = pricePnl,
                ConfirmedCostPnl = confirmedCostPnl,
                EstimatedNetPnl = estimatedNetPnl,
                CostToRRatio = costToRRatio,
                PartialExitQuantity = partialExitQuantity,
                PartialExitStatus = partialExitStatus.ToString(),
                CreatedAtUtc = Utc(6),
            });
            await context.SaveChangesAsync();
        }

        public async Task AddUnlinkedLaterPriceAsync(decimal close)
        {
            await using var context = new SwingAdviserDbContext(Options);
            var dailyPriceId = Guid.NewGuid();
            context.Add(new DailyPriceRow
            {
                Id = dailyPriceId,
                InstrumentId = InstrumentId,
                BarDate = new DateOnly(2026, 8, 26),
                Provider = "LaterPrice",
                CreatedAtUtc = Utc(7),
            });
            context.Add(new DailyPriceRevisionRow
            {
                Id = Guid.NewGuid(),
                RevisionNo = 1,
                ContentSha256 = new string('e', 64),
                AvailableAtUtc = Utc(7),
                AvailabilityStatus = "Known",
                FirstObservedAtUtc = Utc(7),
                RecordedAtUtc = Utc(7),
                DailyPriceId = dailyPriceId,
                ProviderSymbol = "7203.T",
                Open = close,
                High = close,
                Low = close,
                Close = close,
                Volume = 1000,
                Currency = "JPY",
                BarStatus = BarStatus.Confirmed.ToString(),
            });
            await context.SaveChangesAsync();
        }

        private static string ExactIdsJson(IEnumerable<Guid> ids) =>
            $"{{\"schemaVersion\":\"position-evaluation-exact-id-list-v1\",\"ids\":[{string.Join(',', ids.Select(id => $"\"{id:D}\""))}]}}";

        private static async Task<(Guid StrategyId, Guid ManifestId, Guid CandidateId)> AddAtrAnalysisAsync(
            SwingAdviserDbContext context,
            Guid instrumentId,
            DateOnly evaluationDate,
            DateTimeOffset analyzedAtUtc,
            decimal atr,
            PositionSide side,
            bool includeCandidate)
        {
            var strategyId = Guid.NewGuid();
            var calendarId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var priceId = Guid.NewGuid();
            var priceRevisionId = Guid.NewGuid();
            var priceSetId = Guid.NewGuid();
            var manifestId = Guid.NewGuid();
            var technicalId = Guid.NewGuid();
            var candidateId = includeCandidate ? Guid.NewGuid() : Guid.Empty;
            var provider = $"Test-{runId:N}";
            var graphHash = runId.ToString("N") + runId.ToString("N");
            context.Add(new StrategyParameterSnapshotRow
            {
                Id = strategyId, StrategyKey = "Test", StrategyVersion = "1", SchemaVersion = "1",
                AlgorithmVersion = "test", ParametersJson = "{}", ParametersSha256 = graphHash,
                CapturedAtUtc = analyzedAtUtc,
            });
            context.Add(new MarketCalendarVersionRow
            {
                Id = calendarId, MarketCode = "TSE", Provider = provider, VersionName = Guid.NewGuid().ToString("N"),
                TimeZoneId = "Tokyo Standard Time", AlgorithmVersion = "test", ContentSha256 = graphHash,
                RecordedAtUtc = analyzedAtUtc,
            });
            context.Add(new AnalysisRunRow
            {
                Id = runId, EvaluationBarDate = evaluationDate, AnalyzedAtUtc = analyzedAtUtc,
                RecordedCutoffAtUtc = analyzedAtUtc, RunMode = AnalysisRunMode.Manual.ToString(),
                Status = AnalysisRunStatus.Succeeded.ToString(), StrategyParameterSnapshotId = strategyId,
                PointInTimeStatus = PointInTimeStatus.Verified.ToString(), PriceSelectorVersion = "test",
                AdjustmentEngineVersion = "test", IndicatorEngineVersion = "test", CandidateEngineVersion = "test",
                MarketCalendarVersionId = calendarId, ApplicationVersion = "test", StartedAtUtc = analyzedAtUtc,
                CompletedAtUtc = analyzedAtUtc, TotalCount = 1, SuccessCount = 1, FailureCount = 0,
            });
            context.Add(new DailyPriceRow
            {
                Id = priceId, InstrumentId = instrumentId, BarDate = evaluationDate, Provider = provider,
                CreatedAtUtc = analyzedAtUtc,
            });
            context.Add(new DailyPriceRevisionRow
            {
                Id = priceRevisionId, RevisionNo = 1, ContentSha256 = graphHash,
                AvailableAtUtc = analyzedAtUtc, AvailabilityStatus = "Known", FirstObservedAtUtc = analyzedAtUtc,
                RecordedAtUtc = analyzedAtUtc, DailyPriceId = priceId, ProviderSymbol = "7203.T",
                Open = 1000m, High = 1010m, Low = 990m, Close = 1000m, Volume = 1000,
                Currency = "JPY", BarStatus = BarStatus.Confirmed.ToString(),
            });
            context.Add(new PriceRevisionSetRow
            {
                Id = priceSetId, InstrumentId = instrumentId, Provider = provider, FirstBarDate = evaluationDate,
                LastBarDate = evaluationDate, BarCount = 1, SetSha256 = graphHash, SelectorVersion = "test",
                SelectedAvailableCutoffAtUtc = analyzedAtUtc, SelectedRecordedCutoffAtUtc = analyzedAtUtc,
                PointInTimeStatus = PointInTimeStatus.Verified.ToString(), CreatedAtUtc = analyzedAtUtc,
            });
            context.Add(new PriceRevisionSetChangeRow
            {
                Id = Guid.NewGuid(), PriceRevisionSetId = priceSetId, Operation = "Add",
                DailyPriceRevisionId = priceRevisionId, BarDate = evaluationDate, Ordinal = 0,
            });
            context.Add(new AnalysisInputManifestRow
            {
                Id = manifestId, AnalysisRunId = runId, InstrumentId = instrumentId, PriceProvider = provider,
                PriceRevisionSetId = priceSetId, FirstBarDate = evaluationDate, LastBarDate = evaluationDate,
                BarCount = 1, RequiredBarCount = 1, HistoryStatus = HistoryStatus.Complete.ToString(),
                PointInTimeStatus = PointInTimeStatus.Verified.ToString(), SelectionBasis = "ObservedAt",
                SelectionRuleVersion = "test", SelectedRecordedCutoffAtUtc = analyzedAtUtc,
                SelectedAvailableCutoffAtUtc = analyzedAtUtc, PriceRevisionSetSha256 = graphHash,
                CorporateActionSetSha256 = graphHash, ManifestSha256 = graphHash,
                CreatedAtUtc = analyzedAtUtc,
            });
            context.Add(new TechnicalAnalysisResultRow
            {
                Id = technicalId, AnalysisRunId = runId, AnalysisInputManifestId = manifestId,
                InstrumentId = instrumentId, PositionSide = side.ToString(), SignalPurpose = SignalPurpose.Entry.ToString(),
                Outcome = TechnicalAnalysisOutcome.Candidate.ToString(), ReasonSummary = "test", ReasonsJson = "[]",
                CalculationStartBarDate = evaluationDate, CreatedAtUtc = analyzedAtUtc,
            });
            context.Add(new IndicatorResultRow
            {
                Id = Guid.NewGuid(), TechnicalAnalysisResultId = technicalId, IndicatorKey = "ATR14",
                AlgorithmId = TechnicalIndicatorEngine.AtrAlgorithmId, ParametersJson = "{\"period\":14}",
                ValuesJson = $"{{\"schemaVersion\":\"1\",\"value\":{{\"evaluationBarDate\":\"{evaluationDate:yyyy-MM-dd}\",\"current\":{atr}}}}}",
                CalculationStartBarDate = evaluationDate, InputSha256 = graphHash, Ordinal = 0,
            });
            if (includeCandidate)
            {
                context.Add(new CandidateResultRow
                {
                    Id = candidateId, TechnicalAnalysisResultId = technicalId, Score = 80,
                    Confidence = ConfidenceLevel.High.ToString(), PrimaryReason = "test", CreatedAtUtc = analyzedAtUtc,
                });
            }

            await context.SaveChangesAsync();
            return (strategyId, manifestId, candidateId);
        }

        public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
    }
}
