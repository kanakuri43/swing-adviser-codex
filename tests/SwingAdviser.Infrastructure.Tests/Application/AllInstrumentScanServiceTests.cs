using SwingAdviser.Application.Analysis;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.MarketData;

namespace SwingAdviser.Infrastructure.Tests.Application;

public sealed class AllInstrumentScanServiceTests
{
    private static readonly DateOnly EvaluationDate = new(2026, 8, 26);
    private static readonly DateTimeOffset AnalyzedAtUtc = new(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ScanAsync_FiltersUniverseAndEvaluatesEligibleInstrumentOnceForBothSides()
    {
        var context = Context();
        var indicator = new RecordingIndicatorEngine(BullishIndicatorResult);
        var service = new AllInstrumentScanService(
            indicator,
            new CandidateScoringEngine(),
            new FixedTimeProvider(AnalyzedAtUtc));
        var eligibleId = InstrumentId.New();
        var inputs = new[]
        {
            Input(
                InstrumentId.New(),
                "4000",
                listingStatus: ListingStatus.Delisted),
            Input(
                eligibleId,
                "2000",
                indicatorRequest: IndicatorRequest(eligibleId, context.Run.Id)),
            Input(
                InstrumentId.New(),
                "1000",
                securityType: SecurityType.ETF),
            Input(
                InstrumentId.New(),
                "3000",
                scanEligibility: ScanEligibility.Unknown),
        };

        var summary = await service.ScanAsync(new AllInstrumentScanRequest(
            context.Run,
            context.Snapshot,
            context.Parameters,
            inputs));

        Assert.Equal(new[] { "1000", "2000", "3000", "4000" }, summary.Items.Select(item => item.InstrumentCode));
        Assert.Equal(4, summary.TotalInputCount);
        Assert.Equal(1, summary.EligibleInstrumentCount);
        Assert.Equal(1, summary.SucceededInstrumentCount);
        Assert.Equal(3, summary.SkippedInstrumentCount);
        Assert.Equal(0, summary.FailedInstrumentCount);
        Assert.Equal(1, summary.CandidateCount);
        Assert.Equal(AnalysisRunStatus.Succeeded, summary.SuggestedRunStatus);
        Assert.Single(indicator.Requests);
        var eligible = summary.Items.Single(item => item.InstrumentCode == "2000");
        Assert.Equal(2, eligible.Directions.Count);
        Assert.Equal(TechnicalAnalysisOutcome.Candidate, eligible.Directions.Single(x => x.Side == PositionSide.Long).TechnicalResult.Outcome);
        Assert.Equal(TechnicalAnalysisOutcome.NotCandidate, eligible.Directions.Single(x => x.Side == PositionSide.Short).TechnicalResult.Outcome);
    }

    [Fact]
    public async Task ScanAsync_ContinuesAfterUnexpectedInstrumentFailure()
    {
        var context = Context();
        var failingId = InstrumentId.New();
        var succeedingId = InstrumentId.New();
        var indicator = new RecordingIndicatorEngine(request =>
            request.Manifest.InstrumentId == failingId
                ? throw new InvalidOperationException("broken input")
                : BullishIndicatorResult(request));
        var service = new AllInstrumentScanService(
            indicator,
            new CandidateScoringEngine(),
            new FixedTimeProvider(AnalyzedAtUtc));
        var inputs = new[]
        {
            Input(
                succeedingId,
                "2000",
                indicatorRequest: IndicatorRequest(succeedingId, context.Run.Id)),
            Input(
                failingId,
                "1000",
                indicatorRequest: IndicatorRequest(failingId, context.Run.Id)),
        };

        var summary = await service.ScanAsync(new AllInstrumentScanRequest(
            context.Run,
            context.Snapshot,
            context.Parameters,
            inputs));

        Assert.Equal(2, indicator.Requests.Count);
        Assert.Equal(1, summary.FailedInstrumentCount);
        Assert.Equal(1, summary.SucceededInstrumentCount);
        Assert.Equal(AnalysisRunStatus.PartiallySucceeded, summary.SuggestedRunStatus);
        var failed = summary.Items.Single(item => item.InstrumentCode == "1000");
        Assert.Equal(AllInstrumentScanItemStatus.Failed, failed.Status);
        Assert.All(failed.Directions, direction =>
            Assert.Equal(TechnicalAnalysisOutcome.Failed, direction.TechnicalResult.Outcome));
        Assert.Equal(TechnicalAnalysisOutcome.Candidate, summary.Items
            .Single(item => item.InstrumentCode == "2000")
            .Directions.Single(item => item.Side == PositionSide.Long)
            .TechnicalResult.Outcome);
    }

    [Fact]
    public async Task ScanAsync_OuterInstrumentBoundaryContinuesWhenFailureResultConstructionThrows()
    {
        var context = Context();
        var failingId = InstrumentId.New();
        var succeedingId = InstrumentId.New();
        var indicator = new RecordingIndicatorEngine(request =>
            request.Manifest.InstrumentId == failingId
                ? throw new InvalidOperationException("broken input")
                : BullishIndicatorResult(request));
        var service = new AllInstrumentScanService(
            indicator,
            new CandidateScoringEngine(),
            new ThrowOnceTimeProvider(AnalyzedAtUtc));

        var summary = await service.ScanAsync(new AllInstrumentScanRequest(
            context.Run,
            context.Snapshot,
            context.Parameters,
            [
                Input(failingId, "1000", indicatorRequest: IndicatorRequest(failingId, context.Run.Id)),
                Input(succeedingId, "2000", indicatorRequest: IndicatorRequest(succeedingId, context.Run.Id)),
            ]));

        Assert.Equal(2, indicator.Requests.Count);
        Assert.Equal(AllInstrumentScanItemStatus.Failed, summary.Items[0].Status);
        Assert.Contains("SCAN_ITEM_FAILED", summary.Items[0].StatusReason, StringComparison.Ordinal);
        Assert.Equal(AllInstrumentScanItemStatus.Completed, summary.Items[1].Status);
        Assert.Equal(1, summary.CandidateCount);
    }

    [Fact]
    public async Task ScanAsync_ProgressObserverFailureDoesNotInvalidateScan()
    {
        var context = Context();
        var firstId = InstrumentId.New();
        var secondId = InstrumentId.New();
        var indicator = new RecordingIndicatorEngine(BullishIndicatorResult);
        var service = new AllInstrumentScanService(indicator, new CandidateScoringEngine());

        var summary = await service.ScanAsync(
            new AllInstrumentScanRequest(
                context.Run,
                context.Snapshot,
                context.Parameters,
                [
                    Input(firstId, "1000", indicatorRequest: IndicatorRequest(firstId, context.Run.Id)),
                    Input(secondId, "2000", indicatorRequest: IndicatorRequest(secondId, context.Run.Id)),
                ]),
            new ThrowingProgress());

        Assert.Equal(2, indicator.Requests.Count);
        Assert.Equal(2, summary.SucceededInstrumentCount);
        Assert.Equal(2, summary.CandidateCount);
    }

    [Fact]
    public async Task RankedCandidates_UseScoreDescendingThenCodeAscending()
    {
        var context = Context();
        var firstId = InstrumentId.New();
        var secondId = InstrumentId.New();
        var indicator = new RecordingIndicatorEngine(BullishIndicatorResult);
        var service = new AllInstrumentScanService(indicator, new CandidateScoringEngine());
        var inputs = new[]
        {
            Input(secondId, "2000", indicatorRequest: IndicatorRequest(secondId, context.Run.Id)),
            Input(firstId, "1000", indicatorRequest: IndicatorRequest(firstId, context.Run.Id)),
        };

        var summary = await service.ScanAsync(new AllInstrumentScanRequest(
            context.Run,
            context.Snapshot,
            context.Parameters,
            inputs));

        var ranked = summary.GetRankedCandidates(PositionSide.Long);
        Assert.Equal(new[] { "1000", "2000" }, ranked.Select(item => item.InstrumentCode));
        Assert.Equal(ranked[0].Candidate.Score, ranked[1].Candidate.Score);
    }

    [Fact]
    public async Task IdentityMismatch_FailsClosedWithoutCallingIndicatorEngine()
    {
        var context = Context();
        var masterId = InstrumentId.New();
        var otherId = InstrumentId.New();
        var indicator = new RecordingIndicatorEngine(BullishIndicatorResult);
        var service = new AllInstrumentScanService(indicator, new CandidateScoringEngine());
        var input = Input(
            masterId,
            "1000",
            indicatorRequest: IndicatorRequest(otherId, context.Run.Id));

        var summary = await service.ScanAsync(new AllInstrumentScanRequest(
            context.Run,
            context.Snapshot,
            context.Parameters,
            [input]));

        Assert.Empty(indicator.Requests);
        Assert.Equal(AllInstrumentScanItemStatus.Failed, summary.Items.Single().Status);
        Assert.Contains("IDENTITY_MISMATCH", summary.Items.Single().StatusReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndicatorResultIdentityMismatch_FailsClosedWithoutProducingCandidates()
    {
        var context = Context();
        var instrumentId = InstrumentId.New();
        var otherRequest = IndicatorRequest(InstrumentId.New(), context.Run.Id);
        var indicator = new RecordingIndicatorEngine(_ => BullishIndicatorResult(otherRequest));
        var service = new AllInstrumentScanService(indicator, new CandidateScoringEngine());

        var summary = await service.ScanAsync(new AllInstrumentScanRequest(
            context.Run,
            context.Snapshot,
            context.Parameters,
            [Input(
                instrumentId,
                "1000",
                indicatorRequest: IndicatorRequest(instrumentId, context.Run.Id))]));

        var item = Assert.Single(summary.Items);
        Assert.Equal(AllInstrumentScanItemStatus.Failed, item.Status);
        Assert.Contains("INDICATOR_RESULT_IDENTITY_MISMATCH", item.StatusReason, StringComparison.Ordinal);
        Assert.Empty(item.Directions);
        Assert.Equal(0, summary.CandidateCount);
    }

    [Fact]
    public void Input_RejectsIdentifierFromAnotherInstrument()
    {
        var first = Input(InstrumentId.New(), "1000");
        var second = Input(InstrumentId.New(), "2000");

        Assert.Throws<ArgumentException>(() => new AllInstrumentScanInput(
            first.IdentifierRevision,
            second.MasterRevision,
            null));
    }

    [Fact]
    public void DirectionResult_RejectsCandidateFromAnotherTechnicalResult()
    {
        var first = CandidateTechnicalResult();
        var second = CandidateTechnicalResult();
        var component = new CandidateScoreComponent("TEST", true, "{}", 100m, 100m, "matched", 1);
        var candidate = CandidateResult.Create(
            CandidateResultId.New(),
            first,
            100,
            ConfidenceLevel.High,
            "matched",
            [component],
            AnalyzedAtUtc);

        Assert.Throws<ArgumentException>(() => new AllInstrumentScanDirectionResult(
            PositionSide.Long,
            second,
            candidate,
            AnalyzedAtUtc));
    }

    [Fact]
    public async Task ParameterSnapshotMismatch_IsRejectedBeforeAnyInstrumentRuns()
    {
        var context = Context();
        var mismatchedSnapshot = new StrategyParameterSnapshot(
            context.Snapshot.Id,
            context.Snapshot.StrategyKey,
            context.Snapshot.StrategyVersion,
            context.Snapshot.SchemaVersion,
            context.Snapshot.AlgorithmVersion,
            "{}",
            Hash('f'),
            context.Snapshot.CapturedAtUtc);
        var indicator = new RecordingIndicatorEngine(BullishIndicatorResult);
        var service = new AllInstrumentScanService(indicator, new CandidateScoringEngine());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ScanAsync(new AllInstrumentScanRequest(
            context.Run,
            mismatchedSnapshot,
            context.Parameters,
            [])));
        Assert.Empty(indicator.Requests);
    }

    [Fact]
    public async Task AlreadyCancelledScan_DoesNotEvaluateAnInstrument()
    {
        var context = Context();
        var instrumentId = InstrumentId.New();
        var indicator = new RecordingIndicatorEngine(BullishIndicatorResult);
        var service = new AllInstrumentScanService(indicator, new CandidateScoringEngine());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ScanAsync(
            new AllInstrumentScanRequest(
                context.Run,
                context.Snapshot,
                context.Parameters,
                [Input(
                    instrumentId,
                    "1000",
                    indicatorRequest: IndicatorRequest(instrumentId, context.Run.Id))]),
            cancellationToken: cancellation.Token));
        Assert.Empty(indicator.Requests);
    }

    private static ScanContext Context()
    {
        var parameters = CandidateStrategyParameters.Initial;
        var snapshot = parameters.CreateSnapshot(
            Guid.NewGuid(),
            "initial-swing",
            "v1",
            AnalyzedAtUtc.AddMinutes(-1));
        var run = new AnalysisRun(
            AnalysisRunId.New(),
            EvaluationDate,
            AnalyzedAtUtc,
            AnalyzedAtUtc,
            AnalysisRunMode.Daily,
            AnalysisRunStatus.Running,
            snapshot.Id,
            PointInTimeStatus.Verified,
            "test-selector-v1",
            "test-adjustment-v1",
            TechnicalIndicatorEngine.EngineVersion,
            CandidateScoringEngine.EngineVersion);
        return new ScanContext(parameters, snapshot, run);
    }

    private static AllInstrumentScanInput Input(
        InstrumentId instrumentId,
        string code,
        TechnicalIndicatorCalculationRequest? indicatorRequest = null,
        SecurityType securityType = SecurityType.DomesticCommonStock,
        ListingStatus listingStatus = ListingStatus.Listed,
        ScanEligibility scanEligibility = ScanEligibility.Eligible)
    {
        var recordedAt = AnalyzedAtUtc.AddHours(-2);
        var master = new InstrumentMasterRevision(
            instrumentId,
            "JPX",
            new DateRange(EvaluationDate.AddYears(-1), null),
            $"Instrument {code}",
            "TSE",
            "Prime",
            securityType,
            100,
            CurrencyCode.Jpy,
            EvaluationDate.AddYears(-5),
            listingStatus == ListingStatus.Delisted ? EvaluationDate.AddDays(-1) : null,
            listingStatus,
            scanEligibility,
            scanEligibility == ScanEligibility.Excluded ? "test exclusion" : null,
            new SourceRevisionMetadata(
                new RevisionMetadata(Guid.NewGuid(), 1, null, Hash('a'), recordedAt),
                Availability.Known(recordedAt.AddMinutes(-1)),
                recordedAt,
                Guid.NewGuid()));
        var identifier = new InstrumentIdentifierRevision(
            Guid.NewGuid(),
            instrumentId,
            "JPXLocalCode",
            code,
            EvaluationDate.AddYears(-5),
            null,
            RecordDisposition.Effective,
            new SourceRevisionMetadata(
                new RevisionMetadata(Guid.NewGuid(), 1, null, Hash('1'), recordedAt),
                Availability.Known(recordedAt.AddMinutes(-1)),
                recordedAt,
                Guid.NewGuid()));
        return new AllInstrumentScanInput(identifier, master, indicatorRequest);
    }

    private static TechnicalIndicatorCalculationRequest IndicatorRequest(
        InstrumentId instrumentId,
        AnalysisRunId runId)
    {
        var manifest = new AnalysisInputManifest(
            Guid.NewGuid(),
            runId,
            instrumentId,
            "test-provider",
            Guid.NewGuid(),
            null,
            null,
            0,
            201,
            HistoryStatus.InsufficientHistory,
            PointInTimeStatus.Verified,
            AnalysisInputHashing.CalculatePriceRevisionSetHash(instrumentId, "test-provider", []),
            Hash('b'),
            Hash('c'));
        var series = new VerifiedPointInTimeAdjustedPriceSeries(
            manifest,
            AdjustedPriceSeriesStatus.Ready,
            [],
            new Dictionary<DateOnly, Guid>(),
            manifest.CorporateActionSetHash,
            manifest.ManifestHash);
        return new TechnicalIndicatorCalculationRequest(series, EvaluationDate);
    }

    private static TechnicalIndicatorCalculationResult BullishIndicatorResult(
        TechnicalIndicatorCalculationRequest request)
    {
        var periods = TechnicalIndicatorParameters.Initial;
        var snapshot = new TechnicalIndicatorSnapshot(
            EvaluationDate,
            new Dictionary<int, CurrentAndPreviousValue>
            {
                [periods.ShortEmaPeriod] = new CurrentAndPreviousValue(100m, 110m),
                [periods.MediumEmaPeriod] = new CurrentAndPreviousValue(100m, 105m),
                [periods.LongEmaPeriod] = new CurrentAndPreviousValue(100m, 100m),
            },
            new MacdSnapshot(
                new CurrentAndPreviousValue(0m, 3m),
                new CurrentAndPreviousValue(1m, 1m),
                new CurrentAndPreviousValue(-1m, 2m)),
            10m,
            new VolumeSnapshot(1_500m, 1_000m, 1.5m, VolumeRatioStatus.Available));
        var keys = new[]
        {
            "MACD",
            $"EMA{periods.ShortEmaPeriod}",
            $"EMA{periods.MediumEmaPeriod}",
            $"EMA{periods.LongEmaPeriod}",
            $"VolumeAverage{periods.VolumeAveragePeriod}",
            "VolumeRatio",
            $"ATR{periods.AtrPeriod}",
        };
        var indicators = keys.Select((key, index) => new IndicatorResult(
            key,
            "test-algorithm",
            "{}",
            "{}",
            EvaluationDate.AddDays(-200),
            Hash((char)('d' + index)),
            index + 1)).ToArray();
        return TechnicalIndicatorCalculationResult.Succeeded(
            201,
            201,
            EvaluationDate.AddDays(-200),
            snapshot,
            indicators,
            TechnicalIndicatorCalculationIdentity.From(request));
    }

    private static Sha256Hash Hash(char value)
    {
        const string hexadecimal = "0123456789abcdef";
        return new Sha256Hash(new string(hexadecimal[value % hexadecimal.Length], 64));
    }

    private static TechnicalAnalysisResult CandidateTechnicalResult() =>
        new(
            Guid.NewGuid(),
            AnalysisRunId.New(),
            Guid.NewGuid(),
            InstrumentId.New(),
            PositionSide.Long,
            TechnicalAnalysisOutcome.Candidate,
            "matched",
            ["matched"],
            EvaluationDate.AddDays(-200),
            []);

    private sealed record ScanContext(
        CandidateStrategyParameters Parameters,
        StrategyParameterSnapshot Snapshot,
        AnalysisRun Run);

    private sealed class RecordingIndicatorEngine(
        Func<TechnicalIndicatorCalculationRequest, TechnicalIndicatorCalculationResult> calculate)
        : ITechnicalIndicatorEngine
    {
        public string Version => TechnicalIndicatorEngine.EngineVersion;
        public List<TechnicalIndicatorCalculationRequest> Requests { get; } = [];

        public TechnicalIndicatorCalculationResult Calculate(
            TechnicalIndicatorCalculationRequest request,
            TechnicalIndicatorParameters parameters)
        {
            Requests.Add(request);
            return calculate(request);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ThrowOnceTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private bool hasThrown;

        public override DateTimeOffset GetUtcNow()
        {
            if (!hasThrown)
            {
                hasThrown = true;
                throw new InvalidOperationException("test clock failure");
            }

            return now;
        }
    }

    private sealed class ThrowingProgress : IProgress<AllInstrumentScanProgress>
    {
        public void Report(AllInstrumentScanProgress value) =>
            throw new InvalidOperationException("test progress failure");
    }
}
