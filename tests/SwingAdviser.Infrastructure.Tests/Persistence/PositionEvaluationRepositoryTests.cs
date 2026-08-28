using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.Positions;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;
using SwingAdviser.Infrastructure.Persistence.Repositories;

namespace SwingAdviser.Infrastructure.Tests.Persistence;

public sealed partial class PositionEvaluationProjectionRepositoryTests
{
    [Fact]
    public async Task Save_AppendsManifestAndEvaluationAtomically_AndReadsBackVerifiedEvidence()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var evaluation = CreateEvaluatedResult(projection);
        var repository = new PositionEvaluationRepository(database.Context);
        var executionCountBefore = await database.Context.Set<TradeExecutionRow>().CountAsync();

        var saved = await repository.SaveAsync(
            projection,
            evaluation,
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(projection));

        Assert.Equal(evaluation.InputManifestId, saved.ManifestId);
        Assert.Equal(projection.Manifest.ManifestSha256, saved.Projection.Manifest.ManifestSha256);
        Assert.Equal(PositionEvaluationOutcome.Evaluated, saved.Evaluation.Outcome);
        Assert.Equal(ExitDecision.TakeProfit, saved.Evaluation.Decision);
        Assert.Equal(["AllLotsEvaluated"], saved.ReasonCodes);
        var lot = Assert.Single(saved.LotEvaluations);
        Assert.Equal(scenario.LotId, lot.MarginLotId);
        Assert.Equal(scenario.RiskBasisId, lot.RiskBasisSnapshotId);
        Assert.Equal(scenario.RiskPlanId, lot.RiskPlanRevisionId);

        await using var freshContext = CreateContext(database.Connection);
        var read = await new PositionEvaluationRepository(freshContext)
            .ReadAsync(scenario.RunId, scenario.PositionId);
        Assert.Equal(saved.ManifestId, read.ManifestId);
        Assert.Equal(saved.Projection.Manifest, read.Projection.Manifest);
        Assert.Equal(saved.Evaluation, read.Evaluation);
        Assert.Equal(saved.ReasonCodes, read.ReasonCodes);
        Assert.Equal(saved.LotEvaluations, read.LotEvaluations);

        var manifestRow = Assert.Single(await freshContext.Set<PositionEvaluationInputManifestRow>().ToListAsync());
        var evaluationRow = Assert.Single(await freshContext.Set<PositionEvaluationRow>().ToListAsync());
        Assert.Equal(projection.Manifest.ManifestSha256, manifestRow.ManifestSha256);
        Assert.Equal(PositionEvaluationRepository.ReasonsSchemaVersion,
            JsonSchemaVersion(evaluationRow.ReasonsJson));
        Assert.Equal(PositionEvaluationRepository.LotEvaluationsSchemaVersion,
            JsonSchemaVersion(evaluationRow.LotEvaluationsJson));
        Assert.Equal(PositionEvaluationOutcome.Evaluated.ToString(), evaluationRow.EvaluationOutcome);
        Assert.Equal(executionCountBefore, await freshContext.Set<TradeExecutionRow>().CountAsync());
    }

    [Fact]
    public async Task Save_RejectsTamperedManifestHashWithoutAppendingEitherRow()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var tampered = projection with
        {
            Manifest = projection.Manifest with { ManifestSha256 = Hex('0') },
        };
        var repository = new PositionEvaluationRepository(database.Context);

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(
            tampered,
            CreateEvaluatedResult(tampered),
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(tampered)));

        Assert.Empty(await database.Context.Set<PositionEvaluationInputManifestRow>().ToListAsync());
        Assert.Empty(await database.Context.Set<PositionEvaluationRow>().ToListAsync());
    }

    [Fact]
    public async Task Save_RejectsTamperedDerivedLotProjectionEvenWhenTheManifestIsUnchanged()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var originalLot = Assert.Single(projection.Lots);
        var tampered = projection with
        {
            Lots = [originalLot with { CurrentQuantity = originalLot.CurrentQuantity + 1m }],
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => new PositionEvaluationRepository(database.Context)
            .SaveAsync(
                tampered,
                CreateEvaluatedResult(tampered),
                ["AllLotsEvaluated"],
                CreateEvaluatedLotEvidence(tampered)));

        Assert.Empty(await database.Context.Set<PositionEvaluationInputManifestRow>().ToListAsync());
        Assert.Empty(await database.Context.Set<PositionEvaluationRow>().ToListAsync());
    }

    [Fact]
    public async Task Save_RejectsDecisionAndPriceProfitThatContradictTheExactProjection()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var correct = CreateEvaluatedResult(projection);
        var lot = Assert.Single(CreateEvaluatedLotEvidence(projection));
        var repository = new PositionEvaluationRepository(database.Context);

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(
            projection,
            new PositionEvaluation(
                Guid.NewGuid(), correct.AnalysisRunId, correct.PositionId, Guid.NewGuid(),
                correct.EvaluationBarDate, PositionEvaluationOutcome.Evaluated, ExitDecision.Hold,
                "incorrect hold", correct.CurrentQuantity, correct.PriceProfitAndLoss,
                null, null, null, null, PartialExitStatus.NotApplicable, correct.CreatedAtUtc),
            ["AllLotsEvaluated"],
            [lot with { Decision = ExitDecision.Hold, PartialExitStatus = PartialExitStatus.NotApplicable }]));

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(
            projection,
            new PositionEvaluation(
                Guid.NewGuid(), correct.AnalysisRunId, correct.PositionId, Guid.NewGuid(),
                correct.EvaluationBarDate, correct.Outcome, correct.Decision,
                correct.ReasonSummary, correct.CurrentQuantity, 0m,
                null, null, null, null, correct.PartialExitStatus, correct.CreatedAtUtc),
            ["AllLotsEvaluated"],
            [lot]));

        Assert.Empty(await database.Context.Set<PositionEvaluationInputManifestRow>().ToListAsync());
        Assert.Empty(await database.Context.Set<PositionEvaluationRow>().ToListAsync());
    }

    [Fact]
    public async Task Save_RejectsDuplicateRunAndPositionWithoutChangingTheFirstResult()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var repository = new PositionEvaluationRepository(database.Context);
        var first = CreateEvaluatedResult(projection);
        await repository.SaveAsync(
            projection,
            first,
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(projection));

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(
            projection,
            CreateEvaluatedResult(projection),
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(projection)));

        var manifest = Assert.Single(await database.Context.Set<PositionEvaluationInputManifestRow>().ToListAsync());
        var evaluation = Assert.Single(await database.Context.Set<PositionEvaluationRow>().ToListAsync());
        Assert.Equal(first.InputManifestId, manifest.Id);
        Assert.Equal(first.Id, evaluation.Id);
    }

    [Fact]
    public async Task EvaluationInsertFailure_RollsBackTheManifestInsert()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        await database.Context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER test_position_evaluation_insert_failure
            BEFORE INSERT ON position_evaluations
            BEGIN
                SELECT RAISE(ABORT, 'forced evaluation failure');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => new PositionEvaluationRepository(database.Context)
            .SaveAsync(
                projection,
                CreateEvaluatedResult(projection),
                ["AllLotsEvaluated"],
                CreateEvaluatedLotEvidence(projection)));

        await using var freshContext = CreateContext(database.Connection);
        Assert.Empty(await freshContext.Set<PositionEvaluationInputManifestRow>().ToListAsync());
        Assert.Empty(await freshContext.Set<PositionEvaluationRow>().ToListAsync());
    }

    [Fact]
    public async Task Save_PersistsPointInTimeFailureWithoutInventingADecisionOrProfitAndLoss()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context, "Unverified");
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var manifestId = Guid.NewGuid();
        var evaluation = new PositionEvaluation(
            Guid.NewGuid(),
            new AnalysisRunId(scenario.RunId),
            new PositionId(scenario.PositionId),
            manifestId,
            scenario.BarDate,
            PositionEvaluationOutcome.PointInTimeUnverified,
            null,
            "Point-in-time検証が完了していません。",
            projection.Lots.Sum(lot => lot.CurrentQuantity),
            null,
            null,
            null,
            null,
            null,
            PartialExitStatus.NotApplicable,
            scenario.Cutoff.AddMinutes(1));
        var lotEvidence = projection.Lots.Where(lot => lot.CurrentQuantity is > 0m).Select(lot =>
            new PositionEvaluationLotEvidence(
                lot.MarginLotId,
                lot.RiskBasisSnapshotId,
                lot.RiskPlanRevisionId,
                PositionEvaluationOutcome.PointInTimeUnverified,
                null,
                PartialExitStatus.NotApplicable,
                null,
                "{\"blockingReason\":\"PointInTimeUnverified\"}"))
            .ToArray();

        var saved = await new PositionEvaluationRepository(database.Context).SaveAsync(
            projection,
            evaluation,
            ["PointInTimeUnverified"],
            lotEvidence);

        Assert.Equal(PositionEvaluationOutcome.PointInTimeUnverified, saved.Evaluation.Outcome);
        Assert.Null(saved.Evaluation.Decision);
        Assert.Null(saved.Evaluation.PriceProfitAndLoss);
        Assert.Equal(PartialExitStatus.NotApplicable, saved.Evaluation.PartialExitStatus);
    }

    [Fact]
    public async Task Save_PropagatesAnObservationlessCostItemAsMissingInsteadOfZero()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        database.Context.Add(new MarginCostItemRow
        {
            Id = Guid.NewGuid(),
            MarginLotId = scenario.LotId,
            CostType = "StockLendingFee",
            OccurrenceKey = "2026-08-missing",
            PeriodStartDate = scenario.BarDate.AddDays(-5),
            PeriodEndDate = scenario.BarDate,
            CreatedAtUtc = scenario.Cutoff.AddHours(-3),
        });
        await database.Context.SaveChangesAsync();
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var initiallyComplete = CreateEvaluatedResult(projection);

        await Assert.ThrowsAsync<InvalidDataException>(() => new PositionEvaluationRepository(database.Context)
            .SaveAsync(
                projection,
                initiallyComplete,
                ["AllLotsEvaluated"],
                CreateEvaluatedLotEvidence(projection)));

        var missingCosts = new PositionEvaluation(
            Guid.NewGuid(), initiallyComplete.AnalysisRunId, initiallyComplete.PositionId, Guid.NewGuid(),
            initiallyComplete.EvaluationBarDate, initiallyComplete.Outcome, initiallyComplete.Decision,
            initiallyComplete.ReasonSummary, initiallyComplete.CurrentQuantity,
            initiallyComplete.PriceProfitAndLoss, null, null, null,
            initiallyComplete.PartialExitQuantity, initiallyComplete.PartialExitStatus,
            initiallyComplete.CreatedAtUtc);
        var saved = await new PositionEvaluationRepository(database.Context).SaveAsync(
            projection,
            missingCosts,
            ["AllLotsEvaluated", "MarginCostIncomplete"],
            CreateEvaluatedLotEvidence(projection));

        Assert.Null(saved.Evaluation.ConfirmedCostProfitAndLoss);
        Assert.Null(saved.Evaluation.EstimatedNetProfitAndLoss);
        Assert.Null(saved.Evaluation.CostToRRatio);
        Assert.Equal(2, Assert.Single(saved.Projection.Lots).MarginCostItemIds.Count);
    }

    [Fact]
    public async Task Save_VerifiesPartialExitQuantityAgainstPointInTimeTradingUnit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        database.Context.Add(new InstrumentMasterRevisionRow
        {
            Id = Guid.NewGuid(),
            RevisionNo = 1,
            ContentSha256 = Hex('d'),
            AvailabilityStatus = "Known",
            AvailableAtUtc = scenario.Cutoff.AddDays(-2),
            FirstObservedAtUtc = scenario.Cutoff.AddDays(-2),
            RecordedAtUtc = scenario.Cutoff.AddDays(-2),
            InstrumentId = scenario.InstrumentId,
            Provider = "JPX",
            EffectiveFromDate = scenario.BarDate.AddYears(-1),
            Name = "Test Instrument",
            ExchangeCode = "TSE",
            MarketSegment = "Prime",
            SecurityType = "DomesticCommonStock",
            TradingUnit = 10,
            Currency = "JPY",
            ListingStatus = "Listed",
            ScanEligibility = "Eligible",
            ChangeKind = "EffectiveSnapshot",
        });
        await database.Context.SaveChangesAsync();
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var basis = CreateEvaluatedResult(projection);
        var lot = Assert.Single(CreateEvaluatedLotEvidence(projection));
        var candidate = new PositionEvaluation(
            Guid.NewGuid(), basis.AnalysisRunId, basis.PositionId, Guid.NewGuid(),
            basis.EvaluationBarDate, basis.Outcome, basis.Decision, basis.ReasonSummary,
            basis.CurrentQuantity, basis.PriceProfitAndLoss, basis.ConfirmedCostProfitAndLoss,
            basis.EstimatedNetProfitAndLoss, basis.CostToRRatio, 50,
            PartialExitStatus.Candidate, basis.CreatedAtUtc);
        var repository = new PositionEvaluationRepository(database.Context);
        var wrongCandidate = new PositionEvaluation(
            Guid.NewGuid(), basis.AnalysisRunId, basis.PositionId, Guid.NewGuid(),
            basis.EvaluationBarDate, basis.Outcome, basis.Decision, basis.ReasonSummary,
            basis.CurrentQuantity, basis.PriceProfitAndLoss, basis.ConfirmedCostProfitAndLoss,
            basis.EstimatedNetProfitAndLoss, basis.CostToRRatio, 40,
            PartialExitStatus.Candidate, basis.CreatedAtUtc);

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(
            projection,
            wrongCandidate,
            ["AllLotsEvaluated"],
            [lot with { PartialExitStatus = PartialExitStatus.Candidate, PartialExitQuantity = 40 }]));

        var saved = await repository.SaveAsync(
            projection,
            candidate,
            ["AllLotsEvaluated"],
            [lot with { PartialExitStatus = PartialExitStatus.Candidate, PartialExitQuantity = 50 }]);
        Assert.Equal(50, saved.Evaluation.PartialExitQuantity);
        Assert.Equal(PartialExitStatus.Candidate, Assert.Single(saved.LotEvaluations).PartialExitStatus);
    }

    [Fact]
    public async Task Save_AllowsTwoPositionsOfTheSameInstrumentToRemainIndependent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var first = await SeedReadyScenarioAsync(database.Context);
        var secondPositionId = await AddSecondPositionAsync(database.Context, first);
        var projectionRepository = new PositionEvaluationProjectionRepository(database.Context);
        var firstProjection = await projectionRepository.BuildAsync(first.RunId, first.PositionId);
        var secondProjection = await projectionRepository.BuildAsync(first.RunId, secondPositionId);
        var repository = new PositionEvaluationRepository(database.Context);

        await repository.SaveAsync(
            firstProjection,
            CreateEvaluatedResult(firstProjection),
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(firstProjection));
        await repository.SaveAsync(
            secondProjection,
            CreateEvaluatedResult(secondProjection),
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(secondProjection));

        Assert.Equal(2, await database.Context.Set<PositionEvaluationInputManifestRow>().CountAsync());
        Assert.Equal(2, await database.Context.Set<PositionEvaluationRow>().CountAsync());
        var firstRead = await repository.ReadAsync(first.RunId, first.PositionId);
        var secondRead = await repository.ReadAsync(first.RunId, secondPositionId);
        Assert.NotEqual(firstRead.ManifestId, secondRead.ManifestId);
        Assert.NotEqual(firstRead.Projection.Manifest.ManifestSha256, secondRead.Projection.Manifest.ManifestSha256);
        Assert.NotEqual(
            Assert.Single(firstRead.LotEvaluations).MarginLotId,
            Assert.Single(secondRead.LotEvaluations).MarginLotId);
    }

    [Fact]
    public async Task MigratedEvaluationTable_RemainsAppendOnly()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        await new PositionEvaluationRepository(database.Context).SaveAsync(
            projection,
            CreateEvaluatedResult(projection),
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(projection));

        var update = await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE position_evaluations SET reason_summary = 'tampered'"));
        var delete = await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlRawAsync(
            "DELETE FROM position_evaluations"));
        Assert.Contains("append-only", update.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be deleted", delete.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OutcomeMigration_DowngradesEvaluatedRowsAndReappliesWithoutLosingImmutability()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context);
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        await new PositionEvaluationRepository(database.Context).SaveAsync(
            projection,
            CreateEvaluatedResult(projection),
            ["AllLotsEvaluated"],
            CreateEvaluatedLotEvidence(projection));

        await database.Context.Database.MigrateAsync("20260827013734_RestoreRiskBasisSnapshotTriggers");
        Assert.False(await HasColumnAsync(database.Connection, "position_evaluations", "evaluation_outcome"));
        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE position_evaluations SET reason_summary = 'tampered'"));

        await database.Context.Database.MigrateAsync();
        Assert.True(await HasColumnAsync(database.Connection, "position_evaluations", "evaluation_outcome"));
        var restored = Assert.Single(await database.Context.Set<PositionEvaluationRow>().AsNoTracking().ToListAsync());
        Assert.Equal(PositionEvaluationOutcome.Evaluated.ToString(), restored.EvaluationOutcome);
    }

    [Fact]
    public async Task OutcomeMigration_RefusesLossyFailClosedDowngradeAndKeepsTriggers()
    {
        await using var database = await TestDatabase.CreateAsync();
        var scenario = await SeedReadyScenarioAsync(database.Context, "Unverified");
        var projection = await new PositionEvaluationProjectionRepository(database.Context)
            .BuildAsync(scenario.RunId, scenario.PositionId);
        var evaluation = new PositionEvaluation(
            Guid.NewGuid(), new AnalysisRunId(scenario.RunId), new PositionId(scenario.PositionId),
            Guid.NewGuid(), scenario.BarDate, PositionEvaluationOutcome.PointInTimeUnverified, null,
            "Point-in-time検証が完了していません。", projection.Lots.Sum(lot => lot.CurrentQuantity),
            null, null, null, null, null, PartialExitStatus.NotApplicable,
            scenario.Cutoff.AddMinutes(1));
        var lotEvidence = projection.Lots.Where(lot => lot.CurrentQuantity != 0m).Select(lot =>
            new PositionEvaluationLotEvidence(
                lot.MarginLotId, lot.RiskBasisSnapshotId, lot.RiskPlanRevisionId,
                PositionEvaluationOutcome.PointInTimeUnverified, null,
                PartialExitStatus.NotApplicable, null,
                "{\"blockingReason\":\"PointInTimeUnverified\"}"))
            .ToArray();
        await new PositionEvaluationRepository(database.Context).SaveAsync(
            projection, evaluation, ["PointInTimeUnverified"], lotEvidence);

        await Assert.ThrowsAnyAsync<Exception>(() => database.Context.Database.MigrateAsync(
            "20260827013734_RestoreRiskBasisSnapshotTriggers"));

        Assert.True(await HasColumnAsync(database.Connection, "position_evaluations", "evaluation_outcome"));
        await Assert.ThrowsAsync<SqliteException>(() => database.Context.Database.ExecuteSqlRawAsync(
            "UPDATE position_evaluations SET reason_summary = 'tampered'"));
    }

    private static PositionEvaluation CreateEvaluatedResult(PositionEvaluationProjection projection)
    {
        var lots = CreateEvaluatedLotEvidence(projection);
        var decision = lots.Select(lot => lot.Decision!.Value).OrderByDescending(decision => decision switch
        {
            ExitDecision.StopLoss => 4,
            ExitDecision.Exit => 3,
            ExitDecision.TakeProfit => 2,
            _ => 1,
        }).First();
        var partialStatus = decision == ExitDecision.TakeProfit
            ? PartialExitStatus.NotFeasible
            : PartialExitStatus.NotApplicable;
        var pricePnl = projection.Lots.Where(lot => lot.CurrentQuantity is > 0m).Sum(lot =>
            (projection.PositionSide == "Long"
                ? projection.CurrentPrice.Close - lot.EntryBasisPrice!.Value
                : lot.EntryBasisPrice!.Value - projection.CurrentPrice.Close) * lot.CurrentQuantity!.Value);
        var hasKnownFixtureCosts = projection.Lots.SelectMany(lot => lot.MarginCostObservationIds).Any();
        var knownFixtureCost = hasKnownFixtureCosts ? 100m : (decimal?)null;
        var totalRisk = projection.Lots.Where(lot => lot.CurrentQuantity is > 0m)
            .Sum(lot => lot.RiskAmountR!.Value * lot.CurrentQuantity!.Value);
        return new PositionEvaluation(
            Guid.NewGuid(),
            new AnalysisRunId(projection.Manifest.AnalysisRunId),
            new PositionId(projection.PositionId),
            Guid.NewGuid(),
            projection.CurrentPrice.BarDate,
            PositionEvaluationOutcome.Evaluated,
            decision,
            decision == ExitDecision.TakeProfit ? "1.5R到達を確認しました。" : "全lotに上位条件はありません。",
            projection.Lots.Sum(lot => lot.CurrentQuantity),
            pricePnl,
            knownFixtureCost.HasValue ? pricePnl - knownFixtureCost.Value : null,
            knownFixtureCost.HasValue ? pricePnl - knownFixtureCost.Value : null,
            knownFixtureCost.HasValue ? knownFixtureCost.Value / totalRisk : null,
            null,
            partialStatus,
            projection.Manifest.RecordedCutoffAtUtc.AddMinutes(1));
    }

    private static PositionEvaluationLotEvidence[] CreateEvaluatedLotEvidence(
        PositionEvaluationProjection projection) => projection.Lots
        .Where(lot => lot.CurrentQuantity is > 0m)
        .OrderBy(lot => lot.MarginLotId)
        .Select(lot =>
        {
            var stopReached = projection.PositionSide == "Long"
                ? projection.CurrentPrice.Low <= lot.StopPrice
                : projection.CurrentPrice.High >= lot.StopPrice;
            var targetReached = projection.PositionSide == "Long"
                ? projection.CurrentPrice.High >= lot.TakeProfitPrice
                : projection.CurrentPrice.Low <= lot.TakeProfitPrice;
            var decision = stopReached
                ? ExitDecision.StopLoss
                : targetReached
                    ? ExitDecision.TakeProfit
                    : ExitDecision.Hold;
            return new PositionEvaluationLotEvidence(
                lot.MarginLotId,
                lot.RiskBasisSnapshotId,
                lot.RiskPlanRevisionId,
                PositionEvaluationOutcome.Evaluated,
                decision,
                decision == ExitDecision.TakeProfit
                    ? PartialExitStatus.NotFeasible
                    : PartialExitStatus.NotApplicable,
                null,
                $"{{\"algorithmVersion\":\"holding-risk-evaluation-v1\",\"stopReached\":{stopReached.ToString().ToLowerInvariant()},\"targetReached\":{targetReached.ToString().ToLowerInvariant()}}}");
        })
        .ToArray();

    private static SwingAdviserDbContext CreateContext(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(connection)
            .Options);

    private static string JsonSchemaVersion(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.GetProperty("schemaVersion").GetString()!;
    }

    private static async Task<bool> HasColumnAsync(
        SqliteConnection connection,
        string table,
        string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(reader.GetOrdinal("name")) == column)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<Guid> AddSecondPositionAsync(
        SwingAdviserDbContext context,
        Scenario source)
    {
        var sourcePosition = await context.Set<PositionRow>().SingleAsync(row => row.Id == source.PositionId);
        var sourceBasis = await context.Set<RiskBasisSnapshotRow>().SingleAsync(row => row.Id == source.RiskBasisId);
        var positionId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var executionRevisionId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var basisId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var createdAt = source.Cutoff.AddDays(-4);

        context.Add(new PositionRow
        {
            Id = positionId,
            InstrumentId = source.InstrumentId,
            PositionSide = sourcePosition.PositionSide,
            StrategyParameterSnapshotId = sourcePosition.StrategyParameterSnapshotId,
            CreatedAtUtc = createdAt,
        });
        context.Add(new PositionStateRevisionRow
        {
            Id = Guid.NewGuid(),
            RevisionNo = 1,
            ContentSha256 = Hex('8'),
            RecordedAtUtc = createdAt,
            PositionId = positionId,
            Status = "Open",
            ReconciliationStatus = "Clear",
            EffectiveAtUtc = createdAt,
            Reason = "second position",
        });
        context.Add(new TradeExecutionRow
        {
            Id = executionId,
            PositionId = positionId,
            ExecutionKind = "Open",
            Origin = "UserConfirmed",
            CreatedAtUtc = createdAt,
        });
        context.Add(new TradeExecutionRevisionRow
        {
            Id = executionRevisionId,
            RevisionNo = 1,
            ContentSha256 = Hex('9'),
            RecordedAtUtc = createdAt,
            TradeExecutionId = executionId,
            ExecutedAtUtc = createdAt,
            Price = 1_050m,
            Quantity = 200,
            Currency = "JPY",
            RecordDisposition = "Effective",
            ChangeKind = "Initial",
            UserConfirmedAtUtc = createdAt,
        });
        context.Add(new MarginLotRow
        {
            Id = lotId,
            PositionId = positionId,
            OpeningTradeExecutionId = executionId,
            InitialOpeningTradeExecutionRevisionId = executionRevisionId,
            CreatedAtUtc = createdAt,
        });
        context.Add(new MarginLotContractRevisionRow
        {
            Id = Guid.NewGuid(),
            RevisionNo = 1,
            ContentSha256 = Hex('a'),
            RecordedAtUtc = createdAt,
            MarginLotId = lotId,
            OpeningTradeExecutionRevisionId = executionRevisionId,
            MarginType = "Standardized",
            Broker = "Broker",
            ProductName = "Standard margin",
            EffectiveFromDate = source.BarDate.AddDays(-4),
            TermType = "FixedDate",
            FinalRepaymentAtUtc = source.Cutoff.AddMonths(5),
            ContractCurrency = "JPY",
            SpecialFeePolicyJson = "{}",
            RightsProcessingJson = "{}",
            ConfirmedAtUtc = createdAt,
            Evidence = "statement",
            ChangeKind = "Initial",
        });
        context.Add(new RiskBasisSnapshotRow
        {
            Id = basisId,
            MarginLotId = lotId,
            RevisionNo = 1,
            OpeningTradeExecutionRevisionId = executionRevisionId,
            StrategyParameterSnapshotId = sourcePosition.StrategyParameterSnapshotId,
            AnalysisInputManifestId = sourceBasis.AnalysisInputManifestId,
            PriceCurrency = "JPY",
            PriceUnitBasisSha256 = sourceBasis.PriceUnitBasisSha256,
            EntryBasisPrice = 1_050m,
            AtrReferenceBarDate = source.BarDate.AddDays(-6),
            FixedAtr = 20m,
            AtrPeriod = 14,
            AtrAlgorithmId = "atr-wilder-v1",
            StopMultiplier = 3m,
            RiskAmountR = 60m,
            PartialTakeProfitRMultiple = 1.5m,
            PartialTakeProfitFraction = 0.5m,
            InitialStopPrice = 990m,
            InitialTakeProfitPrice = 1_140m,
            ContentSha256 = Hex('b'),
            CreatedAtUtc = createdAt,
        });
        context.Add(new RiskPlanRevisionRow
        {
            Id = planId,
            RevisionNo = 1,
            ContentSha256 = Hex('c'),
            RecordedAtUtc = createdAt,
            RiskBasisSnapshotId = basisId,
            StopPrice = 990m,
            TakeProfitPrice = 1_140m,
            PlanReason = "Initial",
            EffectiveAtUtc = createdAt,
            IsCostAdjusted = false,
        });
        await context.SaveChangesAsync();
        return positionId;
    }
}
