using Microsoft.EntityFrameworkCore;
using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.Persistence.Entities;

namespace SwingAdviser.Infrastructure.TradingWorkspace;

public static class DevelopmentDataSeeder
{
    private const string Provider = "DevelopmentSeed";
    private static readonly DateTimeOffset SeedInstant = new(2026, 8, 26, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly EvaluationDate = new(2026, 8, 25);

    public static async Task SeedAsync(
        DbContextOptions<SwingAdviserDbContext> options,
        CancellationToken cancellationToken = default)
    {
        await using var context = new SwingAdviserDbContext(options);
        if (await context.Set<InstrumentMasterRevisionRow>()
            .AnyAsync(x => x.Provider == Provider, cancellationToken))
        {
            return;
        }

        var strategyId = Guid.NewGuid();
        var calendarId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        context.Add(new StrategyParameterSnapshotRow
        {
            Id = strategyId,
            StrategyKey = "SwingTrend",
            StrategyVersion = "dev-1",
            SchemaVersion = "1",
            AlgorithmVersion = "development-seed",
            ParametersJson = "{\"purpose\":\"local integration testing only\"}",
            ParametersSha256 = Hash('1'),
            CapturedAtUtc = SeedInstant,
            SourceDescription = "再現可能なローカル結合テストデータ",
        });
        context.Add(new MarketCalendarVersionRow
        {
            Id = calendarId,
            MarketCode = "TSE",
            Provider = Provider,
            VersionName = "dev-20260826",
            TimeZoneId = "Tokyo Standard Time",
            AlgorithmVersion = "development-seed",
            ContentSha256 = Hash('2'),
            RecordedAtUtc = SeedInstant,
        });
        context.Add(new AnalysisRunRow
        {
            Id = runId,
            EvaluationBarDate = EvaluationDate,
            AnalyzedAtUtc = SeedInstant,
            RecordedCutoffAtUtc = SeedInstant,
            RunMode = AnalysisRunMode.Manual.ToString(),
            Status = AnalysisRunStatus.Succeeded.ToString(),
            StrategyParameterSnapshotId = strategyId,
            PointInTimeStatus = PointInTimeStatus.Verified.ToString(),
            PriceSelectorVersion = "development-seed",
            AdjustmentEngineVersion = "development-seed",
            IndicatorEngineVersion = "development-seed",
            CandidateEngineVersion = "development-seed",
            MarketCalendarVersionId = calendarId,
            ApplicationVersion = "development-seed",
            StartedAtUtc = SeedInstant.AddMinutes(-1),
            CompletedAtUtc = SeedInstant,
            TotalCount = 2,
            SuccessCount = 2,
            FailureCount = 0,
            Summary = "ローカル結合テスト専用。実分析結果ではありません。",
        });

        var longSeed = AddCandidateGraph(
            context, runId, strategyId, "7203", "トヨタ自動車", PositionSide.Long, 2860m, 82, ConfidenceLevel.High, '3');
        AddCandidateGraph(
            context, runId, strategyId, "9101", "日本郵船", PositionSide.Short, 4890m, 71, ConfidenceLevel.Medium, '6');
        await context.SaveChangesAsync(cancellationToken);

        var service = new TradingWorkspaceService(new SqliteTradingWorkspaceRepository(options));
        var opening = await service.RegisterManualExecutionAsync(new RegisterManualExecutionRequest(
            longSeed.InstrumentId,
            null,
            longSeed.CandidateId,
            PositionSide.Long,
            ExecutionKind.Open,
            new DateTimeOffset(2026, 8, 26, 8, 15, 0, TimeSpan.Zero),
            2780m,
            200,
            "JPY",
            SeedInstant,
            true,
            [],
            Broker: "開発用証券",
            ExternalReference: "DEV-OPEN-001",
            UserNote: "ローカル結合テスト用の利用者確認済みサンプル"), cancellationToken);

        await AddPositionEvaluationAsync(
            options,
            runId,
            strategyId,
            longSeed,
            opening,
            cancellationToken);
    }

    private static CandidateSeedIds AddCandidateGraph(
        SwingAdviserDbContext context,
        Guid runId,
        Guid strategyId,
        string code,
        string name,
        PositionSide side,
        decimal close,
        long score,
        ConfidenceLevel confidence,
        char hashSeed)
    {
        var instrumentId = Guid.NewGuid();
        var identifierId = Guid.NewGuid();
        var dailyPriceId = Guid.NewGuid();
        var priceRevisionId = Guid.NewGuid();
        var priceSetId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var technicalId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var setHash = Hash(hashSeed);

        context.Add(new InstrumentRow { Id = instrumentId, CreatedAtUtc = SeedInstant });
        context.Add(new InstrumentIdentifierRow
        {
            Id = identifierId,
            InstrumentId = instrumentId,
            Scheme = "JPX",
            CreatedAtUtc = SeedInstant,
        });
        context.Add(new InstrumentIdentifierRevisionRow
        {
            Id = Guid.NewGuid(), RevisionNo = 1, ContentSha256 = Hash((char)(hashSeed + 1)),
            AvailableAtUtc = SeedInstant, AvailabilityStatus = "Known", FirstObservedAtUtc = SeedInstant,
            RecordedAtUtc = SeedInstant, InstrumentIdentifierId = identifierId, Value = code,
            RecordDisposition = RecordDisposition.Effective.ToString(), ChangeKind = "Initial",
        });
        context.Add(new InstrumentMasterRevisionRow
        {
            Id = Guid.NewGuid(), RevisionNo = 1, ContentSha256 = Hash((char)(hashSeed + 2)),
            AvailableAtUtc = SeedInstant, AvailabilityStatus = "Known", FirstObservedAtUtc = SeedInstant,
            RecordedAtUtc = SeedInstant, InstrumentId = instrumentId, Provider = Provider,
            EffectiveFromDate = new DateOnly(2026, 1, 1), Name = name, ExchangeCode = "TSE",
            MarketSegment = "Prime", SecurityType = SecurityType.DomesticCommonStock.ToString(), TradingUnit = 100,
            Currency = "JPY", ListingStatus = ListingStatus.Listed.ToString(),
            ScanEligibility = ScanEligibility.Eligible.ToString(), ChangeKind = "EffectiveSnapshot",
        });
        context.Add(new DailyPriceRow
        {
            Id = dailyPriceId, InstrumentId = instrumentId, BarDate = EvaluationDate,
            Provider = Provider, CreatedAtUtc = SeedInstant,
        });
        context.Add(new DailyPriceRevisionRow
        {
            Id = priceRevisionId, RevisionNo = 1, ContentSha256 = Hash((char)(hashSeed + 3)),
            AvailableAtUtc = SeedInstant, AvailabilityStatus = "Known", FirstObservedAtUtc = SeedInstant,
            RecordedAtUtc = SeedInstant, DailyPriceId = dailyPriceId, ProviderSymbol = $"{code}.T",
            Open = close - 20, High = close + 35, Low = close - 45, Close = close, Volume = 1_000_000,
            Currency = "JPY", BarStatus = BarStatus.Confirmed.ToString(),
        });
        context.Add(new PriceRevisionSetRow
        {
            Id = priceSetId, InstrumentId = instrumentId, Provider = Provider,
            FirstBarDate = EvaluationDate, LastBarDate = EvaluationDate, BarCount = 1, SetSha256 = setHash,
            SelectorVersion = "development-seed", SelectedAvailableCutoffAtUtc = SeedInstant,
            SelectedRecordedCutoffAtUtc = SeedInstant, PointInTimeStatus = PointInTimeStatus.Verified.ToString(),
            CreatedAtUtc = SeedInstant,
        });
        context.Add(new PriceRevisionSetChangeRow
        {
            Id = Guid.NewGuid(), PriceRevisionSetId = priceSetId, Operation = "Add",
            DailyPriceRevisionId = priceRevisionId, BarDate = EvaluationDate, Ordinal = 0,
        });
        context.Add(new AnalysisInputManifestRow
        {
            Id = manifestId, AnalysisRunId = runId, InstrumentId = instrumentId, PriceProvider = Provider,
            PriceRevisionSetId = priceSetId, FirstBarDate = EvaluationDate, LastBarDate = EvaluationDate,
            BarCount = 1, RequiredBarCount = 1, HistoryStatus = HistoryStatus.Complete.ToString(),
            PointInTimeStatus = PointInTimeStatus.Verified.ToString(), SelectionBasis = "ObservedAt",
            SelectionRuleVersion = "development-seed", SelectedRecordedCutoffAtUtc = SeedInstant,
            SelectedAvailableCutoffAtUtc = SeedInstant, PriceRevisionSetSha256 = setHash,
            CorporateActionSetSha256 = Hash((char)(hashSeed + 4)), ManifestSha256 = Hash((char)(hashSeed + 5)),
            CreatedAtUtc = SeedInstant,
        });
        context.Add(new TechnicalAnalysisResultRow
        {
            Id = technicalId, AnalysisRunId = runId, AnalysisInputManifestId = manifestId,
            InstrumentId = instrumentId, PositionSide = side.ToString(), SignalPurpose = SignalPurpose.Entry.ToString(),
            Outcome = TechnicalAnalysisOutcome.Candidate.ToString(),
            ReasonSummary = side == PositionSide.Long ? "上昇トレンドと出来高を確認" : "下降トレンドと戻り売り条件を確認",
            ReasonsJson = "[\"development seed\"]", CalculationStartBarDate = EvaluationDate, CreatedAtUtc = SeedInstant,
        });
        context.Add(new IndicatorResultRow
        {
            Id = Guid.NewGuid(), TechnicalAnalysisResultId = technicalId, IndicatorKey = "ATR14",
            AlgorithmId = TechnicalIndicatorEngine.AtrAlgorithmId, ParametersJson = "{\"period\":14}",
            ValuesJson = $"{{\"schemaVersion\":\"1\",\"value\":{{\"evaluationBarDate\":\"{EvaluationDate:yyyy-MM-dd}\",\"current\":40}}}}",
            CalculationStartBarDate = EvaluationDate, InputSha256 = Hash((char)(hashSeed + 6)), Ordinal = 0,
        });
        context.Add(new CandidateResultRow
        {
            Id = candidateId, TechnicalAnalysisResultId = technicalId, Score = score,
            Confidence = confidence.ToString(), PrimaryReason = side == PositionSide.Long
                ? "EMA配列・MACD・出来高条件が整合"
                : "Short Entry条件がすべて整合（売建可否は別確認）",
            CreatedAtUtc = SeedInstant,
        });
        return new CandidateSeedIds(instrumentId, candidateId, manifestId, priceRevisionId, close);
    }

    private static async Task AddPositionEvaluationAsync(
        DbContextOptions<SwingAdviserDbContext> options,
        Guid runId,
        Guid strategyId,
        CandidateSeedIds seed,
        ManualExecutionResult opening,
        CancellationToken cancellationToken)
    {
        await using var context = new SwingAdviserDbContext(options);
        var lot = await context.Set<MarginLotRow>().SingleAsync(x => x.PositionId == opening.PositionId, cancellationToken);
        var riskBasis = await context.Set<RiskBasisSnapshotRow>()
            .SingleAsync(x => x.MarginLotId == lot.Id, cancellationToken);
        var riskPlan = await context.Set<RiskPlanRevisionRow>()
            .SingleAsync(x => x.RiskBasisSnapshotId == riskBasis.Id, cancellationToken);
        var evaluationManifestId = Guid.NewGuid();
        context.Add(new PositionEvaluationInputManifestRow
        {
            Id = evaluationManifestId, AnalysisRunId = runId, PositionId = opening.PositionId,
            AnalysisInputManifestId = seed.ManifestId, CurrentPriceRevisionId = seed.PriceRevisionId,
            TradeExecutionRevisionIdsJson = $"[\"{opening.RevisionId:D}\"]", LotAllocationRevisionIdsJson = "[]",
            PositionAdjustmentIdsJson = "[]", ContractRevisionIdsJson = "[]",
            RiskBasisSnapshotIdsJson = $"[\"{riskBasis.Id:D}\"]", RiskPlanRevisionIdsJson = $"[\"{riskPlan.Id:D}\"]",
            MarginCostObservationIdsJson = "[]", ProjectionVersion = "development-seed",
            RecordedCutoffAtUtc = SeedInstant, ManifestSha256 = Hash('0'), CreatedAtUtc = SeedInstant,
        });
        context.Add(new PositionEvaluationRow
        {
            Id = Guid.NewGuid(), AnalysisRunId = runId, PositionId = opening.PositionId,
            PositionEvaluationInputManifestId = evaluationManifestId, EvaluationBarDate = EvaluationDate,
            EvaluationOutcome = PositionEvaluationOutcome.Evaluated.ToString(),
            ExitDecision = ExitDecision.TakeProfit.ToString(),
            ReasonSummary = "1.5R到達を想定したローカル結合テスト用の利確候補",
            ReasonsJson = "[\"development seed only\"]", LotEvaluationsJson = "[]", CurrentQuantity = 200m,
            PricePnl = (seed.Close - 2780m) * 200m, ConfirmedCostPnl = null, EstimatedNetPnl = null,
            PartialExitQuantity = 100, PartialExitStatus = PartialExitStatus.Candidate.ToString(), CreatedAtUtc = SeedInstant,
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(char value) => new("0123456789abcdef"[value % 16], 64);

    private sealed record CandidateSeedIds(
        Guid InstrumentId,
        Guid CandidateId,
        Guid ManifestId,
        Guid PriceRevisionId,
        decimal Close);
}
