using System.Text.Json;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class CandidateScoringEngineTests
{
    private static readonly DateOnly EvaluationDate = new(2026, 8, 26);
    private readonly CandidateScoringEngine engine = new();
    private readonly TechnicalIndicatorParameters indicatorParameters = TechnicalIndicatorParameters.Initial;

    [Fact]
    public void LongCandidate_UsesDirectionalStrengthAndExcludesVolumeFromScore()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m);

        var decision = engine.Evaluate(
            PositionSide.Long,
            result,
            indicatorParameters,
            CandidateScoringParameters.Initial);

        Assert.True(decision.IsCandidate);
        Assert.Equal(63, decision.Score);
        Assert.Equal(ConfidenceLevel.Medium, decision.Confidence);
        Assert.Equal(new[] { "MACD_DIRECTION", "EMA_ALIGNMENT", "VOLUME_FILTER" }, decision.Components.Select(x => x.Key));
        Assert.Equal(100m, decision.Components.Sum(x => x.Weight));
        var volume = decision.Components.Single(x => x.Key == "VOLUME_FILTER");
        Assert.Equal(0m, volume.Weight);
        Assert.Equal(0m, volume.AwardedScore);
        Assert.Contains("relative ranking aid", decision.PrimaryReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortCandidate_RequiresAndScoresVolumeConfirmation()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(4m, 1m),
            macdSignal: new CurrentAndPreviousValue(3m, 3m),
            shortEma: new CurrentAndPreviousValue(100m, 90m),
            mediumEma: new CurrentAndPreviousValue(100m, 95m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m);

        var decision = engine.Evaluate(
            PositionSide.Short,
            result,
            indicatorParameters,
            CandidateScoringParameters.Initial);

        Assert.True(decision.IsCandidate);
        Assert.Equal(60, decision.Score);
        Assert.Equal(ConfidenceLevel.Medium, decision.Confidence);
        var volume = decision.Components.Single(x => x.Key == "VOLUME_FILTER");
        Assert.Equal(20m, volume.Weight);
        Assert.Equal(10m, volume.AwardedScore);
    }

    [Fact]
    public void MacdEquality_IsNotCandidate()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(1m, 2m),
            macdSignal: new CurrentAndPreviousValue(1m, 2m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m);

        var decision = EvaluateLong(result);

        Assert.Equal(TechnicalAnalysisOutcome.NotCandidate, decision.Outcome);
        Assert.Null(decision.Score);
        Assert.Empty(decision.Components);
        Assert.StartsWith("MACD_NOT_MATCHED", decision.Reasons[0], StringComparison.Ordinal);
    }

    [Fact]
    public void EmaEquality_IsNotCandidate()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 105m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m);

        var decision = EvaluateLong(result);

        Assert.Equal(TechnicalAnalysisOutcome.NotCandidate, decision.Outcome);
        Assert.StartsWith("EMA_NOT_MATCHED", decision.Reasons[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.499999", false)]
    [InlineData("1.5", true)]
    public void VolumeThreshold_IsInclusive(string ratioText, bool expectedCandidate)
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: decimal.Parse(ratioText, System.Globalization.CultureInfo.InvariantCulture));

        var decision = EvaluateLong(result);

        Assert.Equal(expectedCandidate, decision.IsCandidate);
    }

    [Fact]
    public void ReferenceAverageZero_IsInvalidDataInsteadOfInferred()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: null,
            volumeStatus: VolumeRatioStatus.ReferenceAverageZero);

        var decision = EvaluateLong(result);

        Assert.Equal(TechnicalAnalysisOutcome.InvalidData, decision.Outcome);
        Assert.Null(decision.Score);
        Assert.Contains("cannot be inferred", decision.ReasonSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(TechnicalIndicatorCalculationStatus.InsufficientHistory, TechnicalAnalysisOutcome.InsufficientHistory)]
    [InlineData(TechnicalIndicatorCalculationStatus.HistoryIncomplete, TechnicalAnalysisOutcome.HistoryIncomplete)]
    [InlineData(TechnicalIndicatorCalculationStatus.InvalidData, TechnicalAnalysisOutcome.InvalidData)]
    [InlineData(TechnicalIndicatorCalculationStatus.PointInTimeUnverified, TechnicalAnalysisOutcome.PointInTimeUnverified)]
    [InlineData(TechnicalIndicatorCalculationStatus.ReconciliationRequired, TechnicalAnalysisOutcome.ReconciliationRequired)]
    public void IndicatorFailures_ArePropagatedWithoutScoring(
        TechnicalIndicatorCalculationStatus status,
        TechnicalAnalysisOutcome expectedOutcome)
    {
        var result = TechnicalIndicatorCalculationResult.Failed(
            status,
            "test failure",
            actualBarCount: 10,
            requiredBarCount: 201,
            calculationStartBarDate: null,
            identity: TestIdentity());

        var decision = EvaluateLong(result);

        Assert.Equal(expectedOutcome, decision.Outcome);
        Assert.Null(decision.Score);
        Assert.Null(decision.Confidence);
        Assert.Empty(decision.Components);
    }

    [Fact]
    public void PriceAndAtrRescaling_DoesNotChangeGateScoreOrConfidence()
    {
        var original = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m);
        var splitAdjusted = Success(
            macdLine: new CurrentAndPreviousValue(0m, 0.3m),
            macdSignal: new CurrentAndPreviousValue(0.1m, 0.1m),
            shortEma: new CurrentAndPreviousValue(10m, 11m),
            mediumEma: new CurrentAndPreviousValue(10m, 10.5m),
            longEma: new CurrentAndPreviousValue(10m, 10m),
            atr: 1m,
            volumeRatio: 1.5m);

        var left = EvaluateLong(original);
        var right = EvaluateLong(splitAdjusted);

        Assert.Equal(left.Outcome, right.Outcome);
        Assert.Equal(left.Score, right.Score);
        Assert.Equal(left.Confidence, right.Confidence);
        Assert.Equal(
            left.Components.Select(x => x.AwardedScore),
            right.Components.Select(x => x.AwardedScore));
    }

    [Fact]
    public void SameInput_ProducesStableReasonsComponentOrderAndJson()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m);

        var left = EvaluateLong(result);
        var right = EvaluateLong(result);

        Assert.Equal(left.Reasons, right.Reasons);
        Assert.Equal(left.Components.Select(x => x.Key), right.Components.Select(x => x.Key));
        Assert.Equal(left.Components.Select(x => x.RawValueJson), right.Components.Select(x => x.RawValueJson));
        foreach (var component in left.Components)
        {
            using var document = JsonDocument.Parse(component.RawValueJson);
            Assert.Equal("candidate-score-component-v1", document.RootElement.GetProperty("schemaVersion").GetString());
        }
    }

    [Fact]
    public void ZeroAtrWithAlignedSignals_ProducesCappedScore()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 0m,
            volumeRatio: 1.5m);

        var decision = EvaluateLong(result);

        Assert.Equal(100, decision.Score);
        Assert.Equal(ConfidenceLevel.High, decision.Confidence);
    }

    [Fact]
    public void MissingRequiredIndicatorEvidence_IsInvalidData()
    {
        var result = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m,
            omitIndicatorKey: "VolumeRatio");

        var decision = EvaluateLong(result);

        Assert.Equal(TechnicalAnalysisOutcome.InvalidData, decision.Outcome);
        Assert.Contains("VolumeRatio", decision.ReasonSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfidenceThresholds_AreInclusive()
    {
        var parameters = CandidateScoringParameters.Initial;

        Assert.Equal(ConfidenceLevel.Low, parameters.Classify(59));
        Assert.Equal(ConfidenceLevel.Medium, parameters.Classify(60));
        Assert.Equal(ConfidenceLevel.Medium, parameters.Classify(79));
        Assert.Equal(ConfidenceLevel.High, parameters.Classify(80));
    }

    [Fact]
    public void StrategyParameters_HaveStableJsonAndHashAndIncludeUniverse()
    {
        var parameters = CandidateStrategyParameters.Initial;

        var firstJson = parameters.ToNormalizedJson();
        var secondJson = parameters.ToNormalizedJson();

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(
            parameters.CalculateSnapshotHash("initial-swing", "v1"),
            parameters.CalculateSnapshotHash("initial-swing", "v1"));
        using var document = JsonDocument.Parse(firstJson);
        Assert.Equal("TSE", document.RootElement
            .GetProperty("universe")
            .GetProperty("exchangeCodes")[0]
            .GetString());
        Assert.Equal("candidate-strategy-parameters-v1", document.RootElement.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void StrategySnapshotHash_IncludesStrategyAndAlgorithmIdentity()
    {
        var parameters = CandidateStrategyParameters.Initial;
        var first = parameters.CreateSnapshot(
            Guid.NewGuid(),
            "initial-swing",
            "v1",
            new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero));
        var second = parameters.CreateSnapshot(
            Guid.NewGuid(),
            "initial-swing",
            "v2",
            new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero));
        var nextAlgorithm = parameters.CreateSnapshot(
            Guid.NewGuid(),
            "initial-swing",
            "v1",
            new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero),
            "candidate-scoring-engine-v2");

        Assert.NotEqual(first.ParametersHash, second.ParametersHash);
        Assert.NotEqual(first.ParametersHash, nextAlgorithm.ParametersHash);
        using var document = JsonDocument.Parse(first.NormalizedParametersJson);
        Assert.Equal("initial-swing", document.RootElement.GetProperty("strategyKey").GetString());
        Assert.Equal("v1", document.RootElement.GetProperty("strategyVersion").GetString());
        Assert.Equal(
            CandidateScoringEngine.EngineVersion,
            document.RootElement.GetProperty("algorithmVersion").GetString());
        Assert.Equal(
            CandidateStrategyParameters.SchemaVersion,
            document.RootElement.GetProperty("parameters").GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void CandidateComponents_CannotBeMutatedAfterValidation()
    {
        var indicatorResult = Success(
            macdLine: new CurrentAndPreviousValue(0m, 3m),
            macdSignal: new CurrentAndPreviousValue(1m, 1m),
            shortEma: new CurrentAndPreviousValue(100m, 110m),
            mediumEma: new CurrentAndPreviousValue(100m, 105m),
            longEma: new CurrentAndPreviousValue(100m, 100m),
            atr: 10m,
            volumeRatio: 1.5m);
        var decision = EvaluateLong(indicatorResult);
        var technical = new TechnicalAnalysisResult(
            Guid.NewGuid(),
            AnalysisRunId.New(),
            Guid.NewGuid(),
            InstrumentId.New(),
            PositionSide.Long,
            TechnicalAnalysisOutcome.Candidate,
            decision.ReasonSummary,
            decision.Reasons,
            indicatorResult.CalculationStartBarDate,
            indicatorResult.IndicatorResults);
        var candidate = CandidateResult.Create(
            CandidateResultId.New(),
            technical,
            decision.Score!.Value,
            decision.Confidence!.Value,
            decision.PrimaryReason!,
            decision.Components,
            new DateTimeOffset(2026, 8, 26, 14, 0, 0, TimeSpan.Zero));

        var components = Assert.IsAssignableFrom<IList<CandidateScoreComponent>>(candidate.Components);
        Assert.Throws<NotSupportedException>(() => components[0] = components[1]);
    }

    [Fact]
    public void InvalidWeightTotal_IsRejectedBeforeScanning()
    {
        Assert.Throws<ArgumentException>(() => new CandidateScoreWeights(40m, 40m, 10m));
    }

    private CandidateSelectionDecision EvaluateLong(TechnicalIndicatorCalculationResult result) =>
        engine.Evaluate(
            PositionSide.Long,
            result,
            indicatorParameters,
            CandidateScoringParameters.Initial);

    private TechnicalIndicatorCalculationResult Success(
        CurrentAndPreviousValue macdLine,
        CurrentAndPreviousValue macdSignal,
        CurrentAndPreviousValue shortEma,
        CurrentAndPreviousValue mediumEma,
        CurrentAndPreviousValue longEma,
        decimal atr,
        decimal? volumeRatio,
        VolumeRatioStatus volumeStatus = VolumeRatioStatus.Available,
        string? omitIndicatorKey = null)
    {
        var snapshot = new TechnicalIndicatorSnapshot(
            EvaluationDate,
            new Dictionary<int, CurrentAndPreviousValue>
            {
                [indicatorParameters.ShortEmaPeriod] = shortEma,
                [indicatorParameters.MediumEmaPeriod] = mediumEma,
                [indicatorParameters.LongEmaPeriod] = longEma,
            },
            new MacdSnapshot(
                macdLine,
                macdSignal,
                new CurrentAndPreviousValue(
                    macdLine.Previous - macdSignal.Previous,
                    macdLine.Current - macdSignal.Current)),
            atr,
            new VolumeSnapshot(
                1_500m,
                volumeStatus == VolumeRatioStatus.Available ? 1_000m : 0m,
                volumeRatio,
                volumeStatus));
        var keys = new[]
        {
            "MACD",
            $"EMA{indicatorParameters.ShortEmaPeriod}",
            $"EMA{indicatorParameters.MediumEmaPeriod}",
            $"EMA{indicatorParameters.LongEmaPeriod}",
            $"VolumeAverage{indicatorParameters.VolumeAveragePeriod}",
            "VolumeRatio",
            $"ATR{indicatorParameters.AtrPeriod}",
        };
        var indicators = keys
            .Where(key => key != omitIndicatorKey)
            .Select((key, index) => new IndicatorResult(
                key,
                "test-algorithm",
                "{}",
                "{}",
                EvaluationDate.AddDays(-200),
                Hash((char)('a' + index)),
                index + 1))
            .ToArray();
        return TechnicalIndicatorCalculationResult.Succeeded(
            201,
            201,
            EvaluationDate.AddDays(-200),
            snapshot,
            indicators,
            TestIdentity());
    }

    private static TechnicalIndicatorCalculationIdentity TestIdentity() =>
        new(
            AnalysisRunId.New(),
            Guid.NewGuid(),
            InstrumentId.New(),
            EvaluationDate,
            Hash('f'));

    private static Sha256Hash Hash(char value)
    {
        const string hexadecimal = "0123456789abcdef";
        return new Sha256Hash(new string(hexadecimal[value % hexadecimal.Length], 64));
    }
}
