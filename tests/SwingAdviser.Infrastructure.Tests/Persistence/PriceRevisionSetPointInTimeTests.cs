using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;
using SwingAdviser.Infrastructure.Persistence.Repositories;

namespace SwingAdviser.Infrastructure.Tests.Persistence;

public sealed class PriceRevisionSetPointInTimeTests
{
    private static readonly DateTimeOffset Cutoff =
        new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RecordedCutoff = Cutoff.AddDays(1);

    [Theory]
    [InlineData("RecordedAt")]
    [InlineData("SourceAvailableAt")]
    [InlineData("ObservedAt")]
    public async Task VerifyManifest_RejectsPriceRevisionThatWasUnavailableAtItsCutoff(string futureDimension)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(connection)
            .Options;
        await using var context = new SwingAdviserDbContext(options);
        await context.Database.MigrateAsync();

        var manifestId = await SeedManifestAsync(context, futureDimension);
        var repository = new PriceRevisionSetRepository(context);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => repository.VerifyManifestAsync(manifestId));
    }

    private static async Task<Guid> SeedManifestAsync(
        SwingAdviserDbContext context,
        string futureDimension)
    {
        var future = Cutoff.AddMinutes(1);
        var barDate = new DateOnly(2026, 8, 21);
        var instrumentId = Guid.NewGuid();
        var dailyPriceId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var setId = Guid.NewGuid();
        var strategyId = Guid.NewGuid();
        var calendarId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var contentHash = new string('a', 64);
        var setHash = PriceRevisionSetRepository.CalculateSetHash(
            instrumentId,
            "Yahoo",
            [(barDate, contentHash)]);
        var selectionBasis = futureDimension == "ObservedAt"
            ? "ObservedAt"
            : "SourceAvailableAt";

        context.Set<InstrumentRow>().Add(new InstrumentRow { Id = instrumentId, CreatedAtUtc = Cutoff });
        context.Set<DailyPriceRow>().Add(new DailyPriceRow
        {
            Id = dailyPriceId,
            InstrumentId = instrumentId,
            BarDate = barDate,
            Provider = "Yahoo",
            CreatedAtUtc = Cutoff,
        });
        context.Set<DailyPriceRevisionRow>().Add(new DailyPriceRevisionRow
        {
            Id = revisionId,
            RevisionNo = 1,
            ContentSha256 = contentHash,
            AvailabilityStatus = futureDimension == "ObservedAt" ? "Unknown" : "Known",
            AvailableAtUtc = futureDimension == "ObservedAt"
                ? null
                : futureDimension == "SourceAvailableAt" ? future : Cutoff,
            FirstObservedAtUtc = futureDimension is "ObservedAt" or "SourceAvailableAt" ? future : Cutoff,
            RecordedAtUtc = futureDimension == "RecordedAt" ? RecordedCutoff.AddMinutes(1) : future,
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
            Id = setId,
            InstrumentId = instrumentId,
            Provider = "Yahoo",
            FirstBarDate = barDate,
            LastBarDate = barDate,
            BarCount = 1,
            SetSha256 = setHash,
            SelectorVersion = "selector-v1",
            SelectedAvailableCutoffAtUtc = Cutoff,
            SelectedRecordedCutoffAtUtc = RecordedCutoff,
            PointInTimeStatus = "Verified",
            CreatedAtUtc = Cutoff,
        });
        context.Set<PriceRevisionSetChangeRow>().Add(new PriceRevisionSetChangeRow
        {
            Id = Guid.NewGuid(),
            PriceRevisionSetId = setId,
            Operation = "Add",
            DailyPriceRevisionId = revisionId,
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
            CapturedAtUtc = Cutoff,
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
            RecordedAtUtc = Cutoff,
        });
        context.Set<AnalysisRunRow>().Add(new AnalysisRunRow
        {
            Id = runId,
            EvaluationBarDate = barDate,
            AnalyzedAtUtc = Cutoff,
            RecordedCutoffAtUtc = RecordedCutoff,
            RunMode = "Backtest",
            Status = "Succeeded",
            StrategyParameterSnapshotId = strategyId,
            PointInTimeStatus = "Verified",
            PriceSelectorVersion = "selector-v1",
            AdjustmentEngineVersion = "adjust-v1",
            IndicatorEngineVersion = "indicator-v1",
            CandidateEngineVersion = "candidate-v1",
            MarketCalendarVersionId = calendarId,
            ApplicationVersion = "test",
            StartedAtUtc = Cutoff,
            CompletedAtUtc = Cutoff,
            TotalCount = 1,
            SuccessCount = 1,
        });
        context.Set<AnalysisInputManifestRow>().Add(new AnalysisInputManifestRow
        {
            Id = manifestId,
            AnalysisRunId = runId,
            InstrumentId = instrumentId,
            PriceProvider = "Yahoo",
            PriceRevisionSetId = setId,
            FirstBarDate = barDate,
            LastBarDate = barDate,
            BarCount = 1,
            RequiredBarCount = 1,
            HistoryStatus = "Complete",
            PointInTimeStatus = "Verified",
            SelectionBasis = selectionBasis,
            SelectionRuleVersion = "selector-v1",
            SelectedRecordedCutoffAtUtc = RecordedCutoff,
            SelectedAvailableCutoffAtUtc = Cutoff,
            PriceRevisionSetSha256 = setHash,
            CorporateActionSetSha256 = new string('d', 64),
            ManifestSha256 = new string('e', 64),
            CreatedAtUtc = Cutoff,
        });
        await context.SaveChangesAsync();
        return manifestId;
    }
}
