using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;
using SwingAdviser.Infrastructure.Persistence.Repositories;

namespace SwingAdviser.Infrastructure.Tests.Persistence;

public sealed class PositionEvaluationProjectionRepositoryTests
{
    [Fact]
    public async Task Build_SelectsExactLotGraphAndProducesDeterministicCanonicalManifest()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var repository = new PositionEvaluationProjectionRepository(database.Context);

        var first = await repository.BuildAsync(scenario.RunId, scenario.PositionId);
        var second = await repository.BuildAsync(scenario.RunId, scenario.PositionId);

        Assert.True(
            first.Status == PositionProjectionStatus.Ready,
            $"Projection status was {first.Status}: {string.Join(", ", first.StatusReasons)}");
        Assert.Empty(first.StatusReasons);
        var lot = Assert.Single(first.Lots);
        Assert.Equal(100m, lot.CurrentQuantity);
        Assert.Equal(1_000m, lot.EntryBasisPrice);
        Assert.Equal(scenario.OpenRevisionId, lot.OpeningTradeExecutionRevisionId);
        Assert.Equal(scenario.ContractRevisionId, lot.ContractRevisionId);
        Assert.Equal(scenario.RiskBasisId, lot.RiskBasisSnapshotId);
        Assert.Equal(scenario.RiskPlanId, lot.RiskPlanRevisionId);
        Assert.Equal(
            new[] { scenario.ConfirmedCostId, scenario.EstimateCostId }.Order(),
            lot.MarginCostObservationIds.Order());
        Assert.Equal(first.Manifest.CanonicalJson, second.Manifest.CanonicalJson);
        Assert.Equal(first.Manifest.ManifestSha256, second.Manifest.ManifestSha256);
        Assert.Equal(Hash(first.Manifest.CanonicalJson), first.Manifest.ManifestSha256);
        Assert.Equal(
            PositionEvaluationProjectionRepository.ExactIdsJson([scenario.OpenRevisionId]),
            first.Manifest.TradeExecutionRevisionIdsJson);

        using var ids = JsonDocument.Parse(first.Manifest.MarginCostObservationIdsJson);
        Assert.Equal(
            PositionEvaluationProjectionRepository.IdListSchemaVersion,
            ids.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(2, ids.RootElement.GetProperty("ids").GetArrayLength());
    }

    [Fact]
    public async Task Build_AfterLaterCorrections_ReconstructsTheOriginalCutoffAndHash()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var repository = new PositionEvaluationProjectionRepository(database.Context);
        var before = await repository.BuildAsync(scenario.RunId, scenario.PositionId);
        var later = scenario.Cutoff.AddDays(1);

        database.Context.Add(new TradeExecutionRevisionRow
        {
            Id = Guid.NewGuid(),
            RevisionNo = 2,
            SupersedesId = scenario.OpenRevisionId,
            ContentSha256 = Hex('1'),
            RecordedAtUtc = later,
            TradeExecutionId = scenario.OpenExecutionId,
            ExecutedAtUtc = scenario.Cutoff.AddDays(-5),
            Price = 1_100m,
            Quantity = 120,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Correction",
            UserConfirmedAtUtc = later,
            CorrectionReason = "later broker correction",
        });
        database.Context.Add(new MarginLotContractRevisionRow
        {
            Id = Guid.NewGuid(),
            RevisionNo = 2,
            SupersedesId = scenario.ContractRevisionId,
            ContentSha256 = Hex('2'),
            RecordedAtUtc = later,
            MarginLotId = scenario.LotId,
            OpeningTradeExecutionRevisionId = scenario.OpenRevisionId,
            MarginType = "General",
            Broker = "Broker",
            ProductName = "General margin",
            EffectiveFromDate = scenario.BarDate.AddDays(-10),
            TermType = "NoFixedTerm",
            ContractCurrency = "JPY",
            SpecialFeePolicyJson = "{}",
            RightsProcessingJson = "{}",
            ConfirmedAtUtc = later,
            Evidence = "later statement",
            ChangeKind = "ContractAmendment",
        });
        database.Context.Add(new MarginCostObservationRow
        {
            Id = Guid.NewGuid(),
            MarginCostItemId = scenario.CostItemId,
            RevisionNo = 2,
            SupersedesId = scenario.EstimateCostId,
            ValuationKind = "Estimate",
            Direction = "Charge",
            AmountStatus = "KnownAmount",
            Amount = 999m,
            Currency = "JPY",
            SourceKind = "ApplicationEstimate",
            ObservedAtUtc = later,
            ContentSha256 = Hex('3'),
            RecordedAtUtc = later,
        });
        await database.Context.SaveChangesAsync();

        var rebuilt = await repository.BuildAsync(scenario.RunId, scenario.PositionId);

        Assert.Equal(before.Manifest.ManifestSha256, rebuilt.Manifest.ManifestSha256);
        Assert.Equal(before.Manifest.CanonicalJson, rebuilt.Manifest.CanonicalJson);
        Assert.Equal(scenario.OpenRevisionId, Assert.Single(rebuilt.Lots).OpeningTradeExecutionRevisionId);
        Assert.Equal(scenario.ContractRevisionId, Assert.Single(rebuilt.Lots).ContractRevisionId);
        Assert.Contains(scenario.EstimateCostId, Assert.Single(rebuilt.Lots).MarginCostObservationIds);
    }

    [Fact]
    public async Task Build_UsesTheExactEffectiveCloseAndExplicitLotAllocationLeaf()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var closeExecutionId = Guid.NewGuid();
        var closeRevisionId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        database.Context.Add(new TradeExecutionRow
        {
            Id = closeExecutionId,
            PositionId = scenario.PositionId,
            ExecutionKind = "Close",
            Origin = "UserConfirmed",
            CreatedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        database.Context.Add(new TradeExecutionRevisionRow
        {
            Id = closeRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('8'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-1),
            TradeExecutionId = closeExecutionId,
            ExecutedAtUtc = scenario.Cutoff.AddDays(-1),
            Price = 1_080m,
            Quantity = 20,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        database.Context.Add(new LotAllocationRevisionRow
        {
            Id = allocationId,
            AllocationKey = Guid.NewGuid(),
            RevisionNo = 1,
            ClosingTradeExecutionId = closeExecutionId,
            ClosingTradeExecutionRevisionId = closeRevisionId,
            MarginLotId = scenario.LotId,
            Quantity = 20,
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-1),
            ContentSha256 = Hex('9'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        await database.Context.SaveChangesAsync();

        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);

        Assert.Equal(PositionProjectionStatus.Ready, projection.Status);
        Assert.Equal(80m, Assert.Single(projection.Lots).CurrentQuantity);
        Assert.Equal(
            PositionEvaluationProjectionRepository.ExactIdsJson([scenario.OpenRevisionId, closeRevisionId]),
            projection.Manifest.TradeExecutionRevisionIdsJson);
        Assert.Equal(
            PositionEvaluationProjectionRepository.ExactIdsJson([allocationId]),
            projection.Manifest.LotAllocationRevisionIdsJson);
    }

    [Fact]
    public async Task Build_RejectsAnEffectiveCloseWithoutCompleteExplicitLotAllocation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var closeExecutionId = Guid.NewGuid();
        database.Context.Add(new TradeExecutionRow
        {
            Id = closeExecutionId,
            PositionId = scenario.PositionId,
            ExecutionKind = "Close",
            Origin = "UserConfirmed",
            CreatedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        database.Context.Add(new TradeExecutionRevisionRow
        {
            Id = Guid.NewGuid(),
            RevisionNo = 1,
            ContentSha256 = Hex('8'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-1),
            TradeExecutionId = closeExecutionId,
            ExecutedAtUtc = scenario.Cutoff.AddDays(-1),
            Price = 1_080m,
            Quantity = 20,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new PositionEvaluationProjectionRepository(database.Context)
                .BuildAsync(scenario.RunId, scenario.PositionId));
    }

    [Fact]
    public async Task Build_FailsClosedWhenPointInTimeInputsAreUnverified()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context, "Unverified");

        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);

        Assert.Equal(PositionProjectionStatus.PointInTimeUnverified, projection.Status);
        Assert.Contains("PointInTimeUnverified", projection.StatusReasons);
    }

    [Fact]
    public async Task Build_AppliesTheExactCorporateActionAndConvertedRiskPlanToTheLotProjection()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var repository = new PositionEvaluationProjectionRepository(database.Context);
        var beforeAction = await repository.BuildAsync(scenario.RunId, scenario.PositionId);
        var actionId = Guid.NewGuid();
        var actionRevisionId = Guid.NewGuid();
        var adjustmentId = Guid.NewGuid();
        var convertedPlanId = Guid.NewGuid();
        var closeExecutionId = Guid.NewGuid();
        var closeRevisionId = Guid.NewGuid();
        database.Context.Add(new TradeExecutionRow
        {
            Id = closeExecutionId,
            PositionId = scenario.PositionId,
            ExecutionKind = "Close",
            Origin = "UserConfirmed",
            CreatedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        database.Context.Add(new TradeExecutionRevisionRow
        {
            Id = closeRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('8'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-1),
            TradeExecutionId = closeExecutionId,
            ExecutedAtUtc = scenario.Cutoff.AddDays(-1),
            Price = 1_080m,
            Quantity = 20,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        database.Context.Add(new LotAllocationRevisionRow
        {
            Id = Guid.NewGuid(),
            AllocationKey = Guid.NewGuid(),
            RevisionNo = 1,
            ClosingTradeExecutionId = closeExecutionId,
            ClosingTradeExecutionRevisionId = closeRevisionId,
            MarginLotId = scenario.LotId,
            Quantity = 20,
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-1),
            ContentSha256 = Hex('9'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        database.Context.Add(new CorporateActionRow
        {
            Id = actionId,
            InstrumentId = scenario.InstrumentId,
            Provider = "Yahoo",
            DerivedEventKey = "split-applied",
            CreatedAtUtc = scenario.Cutoff.AddDays(-2),
        });
        database.Context.Add(new CorporateActionRevisionRow
        {
            Id = actionRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('8'),
            AvailabilityStatus = "Known",
            AvailableAtUtc = scenario.Cutoff.AddDays(-2),
            FirstObservedAtUtc = scenario.Cutoff.AddDays(-2),
            RecordedAtUtc = scenario.Cutoff.AddDays(-2),
            CorporateActionId = actionId,
            ActionType = "Split",
            Status = "Confirmed",
            EffectiveDate = scenario.BarDate,
            RatioNumerator = 2,
            RatioDenominator = 1,
            PointInTimeStatus = "Verified",
        });
        database.Context.Add(new AnalysisActionApplicationRow
        {
            Id = Guid.NewGuid(),
            AnalysisInputManifestId = scenario.AnalysisManifestId,
            CorporateActionRevisionId = actionRevisionId,
            ApplicationStatus = "Applied",
            PriceFactor = 0.5m,
            VolumeFactor = 2m,
            CumulativePriceFactor = 0.5m,
            CumulativeVolumeFactor = 2m,
            Reason = "2-for-1 split",
            Ordinal = 0,
        });
        database.Context.Add(new PositionAdjustmentRow
        {
            Id = adjustmentId,
            AdjustmentKey = Guid.NewGuid(),
            RevisionNo = 1,
            PositionId = scenario.PositionId,
            MarginLotId = scenario.LotId,
            CorporateActionRevisionId = actionRevisionId,
            Status = "Applied",
            EffectiveDate = scenario.BarDate,
            QuantityFactor = 2m,
            PriceFactor = 0.5m,
            BeforeQuantity = 80m,
            AfterQuantity = 160m,
            BeforeBasisPrice = 1_000m,
            AfterBasisPrice = 500m,
            BeforeFixedAtr = 20m,
            AfterFixedAtr = 10m,
            BeforeStopPrice = 940m,
            AfterStopPrice = 470m,
            BeforeTakeProfitPrice = 1_090m,
            AfterTakeProfitPrice = 545m,
            DetailsJson = "{}",
            ContentSha256 = Hex('9'),
            RecordedAtUtc = scenario.Cutoff.AddMinutes(-30),
        });
        database.Context.Add(new RiskPlanRevisionRow
        {
            Id = convertedPlanId,
            RevisionNo = 2,
            SupersedesId = scenario.RiskPlanId,
            ContentSha256 = Hex('a'),
            RecordedAtUtc = scenario.Cutoff.AddMinutes(-30),
            RiskBasisSnapshotId = scenario.RiskBasisId,
            StopPrice = 470m,
            TakeProfitPrice = 545m,
            TriggerPositionAdjustmentId = adjustmentId,
            PlanReason = "CorporateActionConversion",
            EffectiveAtUtc = scenario.Cutoff.AddMinutes(-30),
            IsCostAdjusted = false,
        });
        await database.Context.SaveChangesAsync();

        var projection = await repository.BuildAsync(scenario.RunId, scenario.PositionId);

        Assert.Equal(PositionProjectionStatus.Ready, projection.Status);
        var lot = Assert.Single(projection.Lots);
        Assert.Equal(160m, lot.CurrentQuantity);
        Assert.Equal(500m, lot.EntryBasisPrice);
        Assert.Equal(10m, lot.FixedAtr);
        Assert.Equal(30m, lot.RiskAmountR);
        Assert.Equal(470m, lot.StopPrice);
        Assert.Equal(545m, lot.TakeProfitPrice);
        Assert.Equal(convertedPlanId, lot.RiskPlanRevisionId);
        Assert.Equal(
            64,
            lot.PriceUnitBasisSha256?.Length);
        Assert.NotEqual(
            PositionEvaluationProjectionRepository.CalculateRiskPriceUnitBasisHash(
                scenario.InstrumentId,
                "JPY",
                EmptyActionGraphHash()),
            lot.PriceUnitBasisSha256);
        Assert.NotEqual(beforeAction.Manifest.ManifestSha256, projection.Manifest.ManifestSha256);
        Assert.NotEqual(Assert.Single(beforeAction.Lots).PriceUnitBasisSha256, lot.PriceUnitBasisSha256);
        Assert.Equal(projection.CurrentPrice.PriceUnitBasisSha256, lot.PriceUnitBasisSha256);
        Assert.Equal(
            PositionEvaluationProjectionRepository.ExactIdsJson([adjustmentId]),
            projection.Manifest.PositionAdjustmentIdsJson);
    }

    [Fact]
    public async Task Build_KeepsFullyClosedHistoricalLotsInTheManifestWithoutRequiringActiveRiskGraph()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var openExecutionId = Guid.NewGuid();
        var openRevisionId = Guid.NewGuid();
        var closedLotId = Guid.NewGuid();
        var closeExecutionId = Guid.NewGuid();
        var closeRevisionId = Guid.NewGuid();
        database.Context.Add(new TradeExecutionRow
        {
            Id = openExecutionId,
            PositionId = scenario.PositionId,
            ExecutionKind = "Open",
            Origin = "UserConfirmed",
            CreatedAtUtc = scenario.Cutoff.AddDays(-4),
        });
        database.Context.Add(new TradeExecutionRevisionRow
        {
            Id = openRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('a'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-4),
            TradeExecutionId = openExecutionId,
            ExecutedAtUtc = scenario.Cutoff.AddDays(-4),
            Price = 900m,
            Quantity = 50,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-4),
        });
        database.Context.Add(new MarginLotRow
        {
            Id = closedLotId,
            PositionId = scenario.PositionId,
            OpeningTradeExecutionId = openExecutionId,
            InitialOpeningTradeExecutionRevisionId = openRevisionId,
            CreatedAtUtc = scenario.Cutoff.AddDays(-4),
        });
        database.Context.Add(new TradeExecutionRow
        {
            Id = closeExecutionId,
            PositionId = scenario.PositionId,
            ExecutionKind = "Close",
            Origin = "UserConfirmed",
            CreatedAtUtc = scenario.Cutoff.AddDays(-3),
        });
        database.Context.Add(new TradeExecutionRevisionRow
        {
            Id = closeRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('b'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-3),
            TradeExecutionId = closeExecutionId,
            ExecutedAtUtc = scenario.Cutoff.AddDays(-3),
            Price = 920m,
            Quantity = 50,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-3),
        });
        database.Context.Add(new LotAllocationRevisionRow
        {
            Id = Guid.NewGuid(),
            AllocationKey = Guid.NewGuid(),
            RevisionNo = 1,
            ClosingTradeExecutionId = closeExecutionId,
            ClosingTradeExecutionRevisionId = closeRevisionId,
            MarginLotId = closedLotId,
            Quantity = 50,
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-3),
            ContentSha256 = Hex('c'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-3),
        });
        await database.Context.SaveChangesAsync();

        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);

        Assert.Equal(PositionProjectionStatus.Ready, projection.Status);
        var closedLot = Assert.Single(projection.Lots, lot => lot.MarginLotId == closedLotId);
        Assert.Equal(0m, closedLot.CurrentQuantity);
        Assert.Null(closedLot.ContractRevisionId);
        Assert.Null(closedLot.RiskBasisSnapshotId);
        Assert.Contains(closedLotId.ToString("D"), projection.Manifest.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Build_RejectsAnAdjustmentThatCrossesAnotherPositionsLotGraph()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var otherPositionId = Guid.NewGuid();
        var otherExecutionId = Guid.NewGuid();
        var otherRevisionId = Guid.NewGuid();
        var otherLotId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        var actionRevisionId = Guid.NewGuid();
        database.Context.Add(new PositionRow
        {
            Id = otherPositionId,
            InstrumentId = scenario.InstrumentId,
            PositionSide = "Long",
            CreatedAtUtc = scenario.Cutoff.AddDays(-4),
        });
        database.Context.Add(new TradeExecutionRow
        {
            Id = otherExecutionId,
            PositionId = otherPositionId,
            ExecutionKind = "Open",
            Origin = "UserConfirmed",
            CreatedAtUtc = scenario.Cutoff.AddDays(-4),
        });
        database.Context.Add(new TradeExecutionRevisionRow
        {
            Id = otherRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('4'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-4),
            TradeExecutionId = otherExecutionId,
            ExecutedAtUtc = scenario.Cutoff.AddDays(-4),
            Price = 500m,
            Quantity = 100,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = scenario.Cutoff.AddDays(-4),
        });
        database.Context.Add(new MarginLotRow
        {
            Id = otherLotId,
            PositionId = otherPositionId,
            OpeningTradeExecutionId = otherExecutionId,
            InitialOpeningTradeExecutionRevisionId = otherRevisionId,
            CreatedAtUtc = scenario.Cutoff.AddDays(-4),
        });
        database.Context.Add(new CorporateActionRow
        {
            Id = actionId,
            InstrumentId = scenario.InstrumentId,
            Provider = "Yahoo",
            DerivedEventKey = "split-cross-graph",
            CreatedAtUtc = scenario.Cutoff.AddDays(-2),
        });
        database.Context.Add(new CorporateActionRevisionRow
        {
            Id = actionRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('5'),
            AvailabilityStatus = "Known",
            AvailableAtUtc = scenario.Cutoff.AddDays(-2),
            FirstObservedAtUtc = scenario.Cutoff.AddDays(-2),
            RecordedAtUtc = scenario.Cutoff.AddDays(-2),
            CorporateActionId = actionId,
            ActionType = "Split",
            Status = "Confirmed",
            EffectiveDate = scenario.BarDate.AddDays(-1),
            RatioNumerator = 2,
            RatioDenominator = 1,
            PointInTimeStatus = "Verified",
        });
        database.Context.Add(new PositionAdjustmentRow
        {
            Id = Guid.NewGuid(),
            AdjustmentKey = Guid.NewGuid(),
            RevisionNo = 1,
            PositionId = scenario.PositionId,
            MarginLotId = otherLotId,
            CorporateActionRevisionId = actionRevisionId,
            Status = "Applied",
            EffectiveDate = scenario.BarDate.AddDays(-1),
            QuantityFactor = 2,
            PriceFactor = 0.5m,
            BeforeQuantity = 100,
            AfterQuantity = 200,
            BeforeBasisPrice = 500,
            AfterBasisPrice = 250,
            DetailsJson = "{}",
            ContentSha256 = Hex('6'),
            RecordedAtUtc = scenario.Cutoff.AddDays(-1),
        });
        await database.Context.SaveChangesAsync();

        var repository = new PositionEvaluationProjectionRepository(database.Context);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => repository.BuildAsync(scenario.RunId, scenario.PositionId));
    }

    [Theory]
    [InlineData("Unsupported", false)]
    [InlineData("ReconciliationRequired", false)]
    [InlineData("ExcludedUnavailable", true)]
    public async Task Build_FailsClosedForUnsupportedOrUnreconciledCorporateAction(
        string applicationStatus,
        bool semanticMismatch)
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var actionId = Guid.NewGuid();
        var actionRevisionId = Guid.NewGuid();
        database.Context.Add(new CorporateActionRow
        {
            Id = actionId,
            InstrumentId = scenario.InstrumentId,
            Provider = "Yahoo",
            DerivedEventKey = $"unsupported-{applicationStatus}",
            CreatedAtUtc = scenario.Cutoff.AddDays(-2),
        });
        database.Context.Add(new CorporateActionRevisionRow
        {
            Id = actionRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('7'),
            AvailabilityStatus = "Known",
            AvailableAtUtc = scenario.Cutoff.AddDays(-2),
            FirstObservedAtUtc = scenario.Cutoff.AddDays(-2),
            RecordedAtUtc = scenario.Cutoff.AddDays(-2),
            CorporateActionId = actionId,
            ActionType = "Unsupported",
            Status = "Confirmed",
            EffectiveDate = scenario.BarDate.AddDays(-1),
            PointInTimeStatus = "Verified",
        });
        database.Context.Add(new AnalysisActionApplicationRow
        {
            Id = Guid.NewGuid(),
            AnalysisInputManifestId = scenario.AnalysisManifestId,
            CorporateActionRevisionId = actionRevisionId,
            ApplicationStatus = applicationStatus,
            Reason = "cannot project safely",
            Ordinal = 0,
        });
        await database.Context.SaveChangesAsync();

        var repository = new PositionEvaluationProjectionRepository(database.Context);
        if (semanticMismatch)
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => repository.BuildAsync(scenario.RunId, scenario.PositionId));
            return;
        }

        var projection = await repository.BuildAsync(scenario.RunId, scenario.PositionId);

        Assert.Equal(PositionProjectionStatus.ReconciliationRequired, projection.Status);
        Assert.Contains("UnsupportedCorporateAction", projection.StatusReasons);
        Assert.NotEmpty(projection.Manifest.ManifestSha256);
    }

    private static async Task<Scenario> SeedReadyScenarioAsync(
        SwingAdviserDbContext context,
        string pointInTimeStatus = "Verified")
    {
        var cutoff = new DateTimeOffset(2026, 8, 28, 7, 0, 0, TimeSpan.Zero);
        var barDate = new DateOnly(2026, 8, 28);
        var instrumentId = Guid.NewGuid();
        var dailyPriceId = Guid.NewGuid();
        var dailyPriceRevisionId = Guid.NewGuid();
        var priceSetId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var calendarId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var analysisManifestId = Guid.NewGuid();
        var entryRunId = Guid.NewGuid();
        var entryAnalysisManifestId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var stateId = Guid.NewGuid();
        var openExecutionId = Guid.NewGuid();
        var openRevisionId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var contractRevisionId = Guid.NewGuid();
        var riskBasisId = Guid.NewGuid();
        var riskPlanId = Guid.NewGuid();
        var costItemId = Guid.NewGuid();
        var estimateCostId = Guid.NewGuid();
        var confirmedCostId = Guid.NewGuid();
        var priceHash = Hex('a');
        var corporateActionSetHash = EmptyActionGraphHash();
        var setHash = PriceRevisionSetRepository.CalculateSetHash(
            instrumentId,
            "Yahoo",
            [(barDate, priceHash)]);

        context.Add(new InstrumentRow { Id = instrumentId, CreatedAtUtc = cutoff.AddDays(-30) });
        context.Add(new DailyPriceRow
        {
            Id = dailyPriceId,
            InstrumentId = instrumentId,
            BarDate = barDate,
            Provider = "Yahoo",
            CreatedAtUtc = cutoff.AddHours(-2),
        });
        context.Add(new DailyPriceRevisionRow
        {
            Id = dailyPriceRevisionId,
            RevisionNo = 1,
            ContentSha256 = priceHash,
            AvailabilityStatus = "Known",
            AvailableAtUtc = cutoff.AddHours(-1),
            FirstObservedAtUtc = cutoff.AddHours(-1),
            RecordedAtUtc = cutoff.AddHours(-1),
            DailyPriceId = dailyPriceId,
            ProviderSymbol = "7203.T",
            Open = 1_090m,
            High = 1_120m,
            Low = 1_080m,
            Close = 1_110m,
            Volume = 1_000_000,
            Currency = "JPY",
            BarStatus = "Confirmed",
        });
        context.Add(new PriceRevisionSetRow
        {
            Id = priceSetId,
            InstrumentId = instrumentId,
            Provider = "Yahoo",
            FirstBarDate = barDate,
            LastBarDate = barDate,
            BarCount = 1,
            SetSha256 = setHash,
            SelectorVersion = "selector-v1",
            SelectedAvailableCutoffAtUtc = cutoff,
            SelectedRecordedCutoffAtUtc = cutoff,
            PointInTimeStatus = pointInTimeStatus,
            CreatedAtUtc = cutoff,
        });
        context.Add(new PriceRevisionSetChangeRow
        {
            Id = Guid.NewGuid(),
            PriceRevisionSetId = priceSetId,
            Operation = "Add",
            DailyPriceRevisionId = dailyPriceRevisionId,
            BarDate = barDate,
            Ordinal = 1,
        });
        context.Add(new StrategyParameterSnapshotRow
        {
            Id = strategyId,
            StrategyKey = "swing-v1",
            StrategyVersion = "1",
            SchemaVersion = "1",
            AlgorithmVersion = "1",
            ParametersJson = "{}",
            ParametersSha256 = Hex('b'),
            CapturedAtUtc = cutoff.AddDays(-10),
        });
        context.Add(new MarketCalendarVersionRow
        {
            Id = calendarId,
            MarketCode = "TSE",
            Provider = "JPX",
            VersionName = "2026-08-28",
            TimeZoneId = "Asia/Tokyo",
            AlgorithmVersion = "1",
            ContentSha256 = Hex('c'),
            RecordedAtUtc = cutoff.AddDays(-1),
        });
        context.Add(new AnalysisRunRow
        {
            Id = runId,
            EvaluationBarDate = barDate,
            AnalyzedAtUtc = cutoff,
            RecordedCutoffAtUtc = cutoff,
            RunMode = "Daily",
            Status = "Running",
            StrategyParameterSnapshotId = strategyId,
            PointInTimeStatus = pointInTimeStatus,
            PriceSelectorVersion = "selector-v1",
            AdjustmentEngineVersion = "adjust-v1",
            IndicatorEngineVersion = "indicator-v1",
            CandidateEngineVersion = "candidate-v1",
            MarketCalendarVersionId = calendarId,
            ApplicationVersion = "test",
            StartedAtUtc = cutoff,
        });
        context.Add(new AnalysisInputManifestRow
        {
            Id = analysisManifestId,
            AnalysisRunId = runId,
            InstrumentId = instrumentId,
            PriceProvider = "Yahoo",
            PriceRevisionSetId = priceSetId,
            FirstBarDate = barDate,
            LastBarDate = barDate,
            BarCount = 1,
            RequiredBarCount = 1,
            HistoryStatus = "Complete",
            PointInTimeStatus = pointInTimeStatus,
            SelectionBasis = "ObservedAt",
            SelectionRuleVersion = "selector-v1",
            SelectedRecordedCutoffAtUtc = cutoff,
            SelectedAvailableCutoffAtUtc = cutoff,
            PriceRevisionSetSha256 = setHash,
            CorporateActionSetSha256 = corporateActionSetHash,
            ManifestSha256 = Hex('e'),
            CreatedAtUtc = cutoff,
        });
        context.Add(new AnalysisRunRow
        {
            Id = entryRunId,
            EvaluationBarDate = barDate.AddDays(-6),
            AnalyzedAtUtc = cutoff.AddDays(-6),
            RecordedCutoffAtUtc = cutoff.AddDays(-6),
            RunMode = "Daily",
            Status = "Succeeded",
            StrategyParameterSnapshotId = strategyId,
            PointInTimeStatus = pointInTimeStatus,
            PriceSelectorVersion = "selector-v1",
            AdjustmentEngineVersion = "adjust-v1",
            IndicatorEngineVersion = "indicator-v1",
            CandidateEngineVersion = "candidate-v1",
            MarketCalendarVersionId = calendarId,
            ApplicationVersion = "test",
            StartedAtUtc = cutoff.AddDays(-6),
            CompletedAtUtc = cutoff.AddDays(-6),
        });
        context.Add(new AnalysisInputManifestRow
        {
            Id = entryAnalysisManifestId,
            AnalysisRunId = entryRunId,
            InstrumentId = instrumentId,
            PriceProvider = "Yahoo",
            PriceRevisionSetId = priceSetId,
            FirstBarDate = barDate.AddDays(-6),
            LastBarDate = barDate.AddDays(-6),
            BarCount = 1,
            RequiredBarCount = 1,
            HistoryStatus = "Complete",
            PointInTimeStatus = pointInTimeStatus,
            SelectionBasis = "ObservedAt",
            SelectionRuleVersion = "selector-v1",
            SelectedRecordedCutoffAtUtc = cutoff.AddDays(-6),
            SelectedAvailableCutoffAtUtc = cutoff.AddDays(-6),
            PriceRevisionSetSha256 = setHash,
            CorporateActionSetSha256 = corporateActionSetHash,
            ManifestSha256 = Hex('9'),
            CreatedAtUtc = cutoff.AddDays(-6),
        });
        context.Add(new PositionRow
        {
            Id = positionId,
            InstrumentId = instrumentId,
            PositionSide = "Long",
            StrategyParameterSnapshotId = strategyId,
            CreatedAtUtc = cutoff.AddDays(-5),
        });
        context.Add(new PositionStateRevisionRow
        {
            Id = stateId,
            RevisionNo = 1,
            ContentSha256 = Hex('f'),
            RecordedAtUtc = cutoff.AddDays(-5),
            PositionId = positionId,
            Status = "Open",
            ReconciliationStatus = "Clear",
            EffectiveAtUtc = cutoff.AddDays(-5),
            Reason = "initial",
        });
        context.Add(new TradeExecutionRow
        {
            Id = openExecutionId,
            PositionId = positionId,
            ExecutionKind = "Open",
            Origin = "UserConfirmed",
            CreatedAtUtc = cutoff.AddDays(-5),
        });
        context.Add(new TradeExecutionRevisionRow
        {
            Id = openRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('1'),
            RecordedAtUtc = cutoff.AddDays(-5),
            TradeExecutionId = openExecutionId,
            ExecutedAtUtc = cutoff.AddDays(-5),
            Price = 1_000m,
            Quantity = 100,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = cutoff.AddDays(-5),
        });
        context.Add(new MarginLotRow
        {
            Id = lotId,
            PositionId = positionId,
            OpeningTradeExecutionId = openExecutionId,
            InitialOpeningTradeExecutionRevisionId = openRevisionId,
            CreatedAtUtc = cutoff.AddDays(-5),
        });
        context.Add(new MarginLotContractRevisionRow
        {
            Id = contractRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('2'),
            RecordedAtUtc = cutoff.AddDays(-5),
            MarginLotId = lotId,
            OpeningTradeExecutionRevisionId = openRevisionId,
            MarginType = "Standardized",
            Broker = "Broker",
            ProductName = "Standard margin",
            EffectiveFromDate = barDate.AddDays(-5),
            TermType = "FixedDate",
            FinalRepaymentAtUtc = cutoff.AddMonths(5),
            ContractCurrency = "JPY",
            SpecialFeePolicyJson = "{}",
            RightsProcessingJson = "{}",
            ConfirmedAtUtc = cutoff.AddDays(-5),
            Evidence = "statement",
            ChangeKind = "Initial",
        });
        context.Add(new RiskBasisSnapshotRow
        {
            Id = riskBasisId,
            MarginLotId = lotId,
            RevisionNo = 1,
            OpeningTradeExecutionRevisionId = openRevisionId,
            StrategyParameterSnapshotId = strategyId,
            AnalysisInputManifestId = entryAnalysisManifestId,
            PriceCurrency = "JPY",
            PriceUnitBasisSha256 = PositionEvaluationProjectionRepository.CalculateRiskPriceUnitBasisHash(
                instrumentId,
                "JPY",
                corporateActionSetHash),
            EntryBasisPrice = 1_000m,
            AtrReferenceBarDate = barDate.AddDays(-6),
            FixedAtr = 20m,
            AtrPeriod = 14,
            AtrAlgorithmId = "atr-wilder-v1",
            StopMultiplier = 3m,
            RiskAmountR = 60m,
            PartialTakeProfitRMultiple = 1.5m,
            PartialTakeProfitFraction = 0.5m,
            InitialStopPrice = 940m,
            InitialTakeProfitPrice = 1_090m,
            ContentSha256 = Hex('4'),
            CreatedAtUtc = cutoff.AddDays(-5),
        });
        context.Add(new RiskPlanRevisionRow
        {
            Id = riskPlanId,
            RevisionNo = 1,
            ContentSha256 = Hex('5'),
            RecordedAtUtc = cutoff.AddDays(-5),
            RiskBasisSnapshotId = riskBasisId,
            StopPrice = 940m,
            TakeProfitPrice = 1_090m,
            PlanReason = "Initial",
            EffectiveAtUtc = cutoff.AddDays(-5),
            IsCostAdjusted = false,
        });
        context.Add(new MarginCostItemRow
        {
            Id = costItemId,
            MarginLotId = lotId,
            CostType = "BuyerInterest",
            OccurrenceKey = "2026-08",
            PeriodStartDate = barDate.AddDays(-5),
            PeriodEndDate = barDate,
            CreatedAtUtc = cutoff.AddDays(-1),
        });
        context.Add(new MarginCostObservationRow
        {
            Id = estimateCostId,
            MarginCostItemId = costItemId,
            RevisionNo = 1,
            ValuationKind = "Estimate",
            Direction = "Charge",
            AmountStatus = "KnownAmount",
            Amount = 120m,
            Currency = "JPY",
            SourceKind = "ApplicationEstimate",
            ObservedAtUtc = cutoff.AddHours(-2),
            ContentSha256 = Hex('6'),
            RecordedAtUtc = cutoff.AddHours(-2),
        });
        context.Add(new MarginCostObservationRow
        {
            Id = confirmedCostId,
            MarginCostItemId = costItemId,
            RevisionNo = 1,
            ReconcilesEstimateId = estimateCostId,
            ValuationKind = "Confirmed",
            Direction = "Charge",
            AmountStatus = "KnownAmount",
            Amount = 100m,
            Currency = "JPY",
            SourceKind = "BrokerStatement",
            ObservedAtUtc = cutoff.AddHours(-1),
            ContentSha256 = Hex('7'),
            RecordedAtUtc = cutoff.AddHours(-1),
        });
        await context.SaveChangesAsync();

        return new Scenario(
            cutoff,
            barDate,
            instrumentId,
            runId,
            analysisManifestId,
            positionId,
            openExecutionId,
            openRevisionId,
            lotId,
            contractRevisionId,
            riskBasisId,
            riskPlanId,
            costItemId,
            estimateCostId,
            confirmedCostId);
    }

    private static string Hex(char value) => new(value, 64);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string EmptyActionGraphHash() =>
        Hash("{\"schemaVersion\":\"position-evaluation-corporate-action-graph-v1\",\"applications\":[]}");

    private sealed record Scenario(
        DateTimeOffset Cutoff,
        DateOnly BarDate,
        Guid InstrumentId,
        Guid RunId,
        Guid AnalysisManifestId,
        Guid PositionId,
        Guid OpenExecutionId,
        Guid OpenRevisionId,
        Guid LotId,
        Guid ContractRevisionId,
        Guid RiskBasisId,
        Guid RiskPlanId,
        Guid CostItemId,
        Guid EstimateCostId,
        Guid ConfirmedCostId);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, SwingAdviserDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public SwingAdviserDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
                .UseSwingAdviserSqlite(connection)
                .Options;
            var context = new SwingAdviserDbContext(options);
            await context.Database.MigrateAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
