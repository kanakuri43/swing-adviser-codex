using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Common;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;
using SwingAdviser.Infrastructure.TradingWorkspace;

namespace SwingAdviser.Infrastructure.Tests.TradingWorkspace;

public sealed class SqliteTradingWorkspaceRepositoryTests
{
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

        await service.RegisterManualExecutionAsync(invalid with
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
        Assert.Empty((await service.LoadAsync()).Positions);
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
            context.Add(new PositionAdjustmentRow
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
    }

    private static DateTimeOffset Utc(int hour) =>
        new(2026, 8, 26, hour, 0, 0, TimeSpan.Zero);

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(SqliteConnection connection, DbContextOptions<SwingAdviserDbContext> options, Guid instrumentId)
        {
            Connection = connection;
            Options = options;
            InstrumentId = instrumentId;
        }

        private SqliteConnection Connection { get; }
        public DbContextOptions<SwingAdviserDbContext> Options { get; }
        public Guid InstrumentId { get; }

        public static async Task<Fixture> CreateAsync()
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
            return new Fixture(connection, options, instrumentId);
        }

        public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
    }
}
