using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;
using SwingAdviser.Infrastructure.Persistence.Repositories;

namespace SwingAdviser.Infrastructure.Tests.Persistence;

public sealed class PriceRevisionSetRepositoryTests
{
    [Fact]
    public async Task VerifyManifest_ReconstructsExactPriceRevisionMembersAndHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(connection)
            .Options;
        await using var context = new SwingAdviserDbContext(options);
        await context.Database.MigrateAsync();

        var now = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
        var barDate = new DateOnly(2026, 8, 21);
        var instrumentId = Guid.NewGuid();
        var dailyPriceId = Guid.NewGuid();
        var dailyPriceRevisionId = Guid.NewGuid();
        var priceSetId = Guid.NewGuid();
        var contentHash = new string('a', 64);
        var setHash = PriceRevisionSetRepository.CalculateSetHash(
            instrumentId,
            "Yahoo",
            [(barDate, contentHash)]);
        var strategyId = Guid.NewGuid();
        var calendarId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();

        context.Set<InstrumentRow>().Add(new InstrumentRow { Id = instrumentId, CreatedAtUtc = now });
        context.Set<DailyPriceRow>().Add(new DailyPriceRow
        {
            Id = dailyPriceId,
            InstrumentId = instrumentId,
            BarDate = barDate,
            Provider = "Yahoo",
            CreatedAtUtc = now,
        });
        context.Set<DailyPriceRevisionRow>().Add(new DailyPriceRevisionRow
        {
            Id = dailyPriceRevisionId,
            RevisionNo = 1,
            ContentSha256 = contentHash,
            AvailabilityStatus = "Known",
            AvailableAtUtc = now,
            FirstObservedAtUtc = now,
            RecordedAtUtc = now,
            DailyPriceId = dailyPriceId,
            ProviderSymbol = "7203.T",
            Open = 1_000m,
            High = 1_050m,
            Low = 990m,
            Close = 1_040m,
            Volume = 1_000_000,
            Currency = "JPY",
            BarStatus = "Confirmed",
        });
        context.Set<PriceRevisionSetRow>().Add(new PriceRevisionSetRow
        {
            Id = priceSetId,
            InstrumentId = instrumentId,
            Provider = "Yahoo",
            FirstBarDate = barDate,
            LastBarDate = barDate,
            BarCount = 1,
            SetSha256 = setHash,
            SelectorVersion = "selector-v1",
            SelectedAvailableCutoffAtUtc = now,
            SelectedRecordedCutoffAtUtc = now,
            PointInTimeStatus = "Verified",
            CreatedAtUtc = now,
        });
        context.Set<PriceRevisionSetChangeRow>().Add(new PriceRevisionSetChangeRow
        {
            Id = Guid.NewGuid(),
            PriceRevisionSetId = priceSetId,
            Operation = "Add",
            DailyPriceRevisionId = dailyPriceRevisionId,
            BarDate = barDate,
            Ordinal = 1,
        });
        context.Set<StrategyParameterSnapshotRow>().Add(new StrategyParameterSnapshotRow
        {
            Id = strategyId,
            StrategyKey = "swing-v1",
            StrategyVersion = "1",
            SchemaVersion = "1",
            AlgorithmVersion = "1",
            ParametersJson = "{}",
            ParametersSha256 = new string('b', 64),
            CapturedAtUtc = now,
        });
        context.Set<MarketCalendarVersionRow>().Add(new MarketCalendarVersionRow
        {
            Id = calendarId,
            MarketCode = "TSE",
            Provider = "JPX",
            VersionName = "2026-08-24",
            TimeZoneId = "Asia/Tokyo",
            AlgorithmVersion = "1",
            ContentSha256 = new string('c', 64),
            RecordedAtUtc = now,
        });
        context.Set<AnalysisRunRow>().Add(new AnalysisRunRow
        {
            Id = runId,
            EvaluationBarDate = barDate,
            AnalyzedAtUtc = now,
            RecordedCutoffAtUtc = now,
            RunMode = "Daily",
            Status = "Succeeded",
            StrategyParameterSnapshotId = strategyId,
            PointInTimeStatus = "Verified",
            PriceSelectorVersion = "selector-v1",
            AdjustmentEngineVersion = "adjust-v1",
            IndicatorEngineVersion = "indicator-v1",
            CandidateEngineVersion = "candidate-v1",
            MarketCalendarVersionId = calendarId,
            ApplicationVersion = "test",
            StartedAtUtc = now,
            CompletedAtUtc = now,
            TotalCount = 1,
            SuccessCount = 1,
        });
        context.Set<AnalysisInputManifestRow>().Add(new AnalysisInputManifestRow
        {
            Id = manifestId,
            AnalysisRunId = runId,
            InstrumentId = instrumentId,
            PriceProvider = "Yahoo",
            PriceRevisionSetId = priceSetId,
            FirstBarDate = barDate,
            LastBarDate = barDate,
            BarCount = 1,
            RequiredBarCount = 1,
            HistoryStatus = "Complete",
            PointInTimeStatus = "Verified",
            SelectionBasis = "ObservedAt",
            SelectionRuleVersion = "selector-v1",
            SelectedRecordedCutoffAtUtc = now,
            SelectedAvailableCutoffAtUtc = now,
            PriceRevisionSetSha256 = setHash,
            CorporateActionSetSha256 = new string('d', 64),
            ManifestSha256 = new string('e', 64),
            CreatedAtUtc = now,
        });
        await context.SaveChangesAsync();

        var repository = new PriceRevisionSetRepository(context);
        var reconstructed = await repository.VerifyManifestAsync(manifestId);
        var selectedAtCutoff = await context.Set<DailyPriceRevisionRow>()
            .AsNoTracking()
            .Where(revision => revision.RecordedAtUtc <= now)
            .OrderBy(revision => revision.RecordedAtUtc)
            .SingleAsync();

        Assert.Equal(setHash, reconstructed.Sha256);
        Assert.Equal(dailyPriceRevisionId, reconstructed.RevisionIdsByBarDate[barDate]);
        Assert.Equal(1_040m, selectedAtCutoff.Close);
        Assert.Equal(now, selectedAtCutoff.RecordedAtUtc);
    }
}
