using System.Text.Json;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class TechnicalIndicatorEngineTests
{
    private static readonly DateOnly FirstBarDate = new(2025, 1, 1);
    private readonly TechnicalIndicatorEngine engine = new();

    [Fact]
    public void InitialParameters_CalculateDocumentedIndicatorsWithoutIntermediateRounding()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount);

        var result = engine.Calculate(Request(bars, parameters), parameters);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.RequiredBarCount);
        Assert.Equal(FirstBarDate, result.CalculationStartBarDate);
        var snapshot = Assert.IsType<TechnicalIndicatorSnapshot>(result.Snapshot);
        Assert.Equal(new CurrentAndPreviousValue(1_189.5m, 1_190.5m), snapshot.Emas[20]);
        Assert.Equal(new CurrentAndPreviousValue(1_174.5m, 1_175.5m), snapshot.Emas[50]);
        Assert.Equal(new CurrentAndPreviousValue(1_099.5m, 1_100.5m), snapshot.Emas[200]);
        Assert.Equal(new CurrentAndPreviousValue(7m, 7m), snapshot.Macd.Line);
        Assert.Equal(new CurrentAndPreviousValue(7m, 7m), snapshot.Macd.Signal);
        Assert.Equal(new CurrentAndPreviousValue(0m, 0m), snapshot.Macd.Histogram);
        Assert.Equal(4m, snapshot.Atr);
        Assert.Equal(2_895m, snapshot.Volume.ReferenceAverageVolume);
        Assert.Equal(3_000m / 2_895m, snapshot.Volume.Ratio);
        Assert.Equal(VolumeRatioStatus.Available, snapshot.Volume.RatioStatus);
    }

    [Fact]
    public void Atr_UsesInitialTrueRangeSmaAndWilderSmoothing()
    {
        var parameters = new TechnicalIndicatorParameters(2, 3, 2, 2, 3, 4, 3, 2);
        var bars = new[]
        {
            Bar(0, 9m, 10m, 8m, 9m, 10m),
            Bar(1, 9m, 12m, 9m, 11m, 20m),
            Bar(2, 8m, 10m, 7m, 8m, 30m),
            Bar(3, 14m, 15m, 13m, 14m, 40m),
            Bar(4, 15m, 16m, 14m, 15m, 50m),
        };

        var result = engine.Calculate(Request(bars, parameters), parameters);

        Assert.True(result.IsSuccess);
        Assert.Equal(3.5555555555555555555555555557m, result.Snapshot!.Atr);
        Assert.Equal(
            new CurrentAndPreviousValue(
                12.222222222222222222222222223m,
                14.074074074074074074074074074m),
            result.Snapshot.Emas[2]);
        Assert.Equal(
            new CurrentAndPreviousValue(
                11.666666666666666666666666666m,
                13.333333333333333333333333333m),
            result.Snapshot.Emas[3]);
        Assert.Equal(new CurrentAndPreviousValue(10.5m, 12.3m), result.Snapshot.Emas[4]);
        Assert.Equal(
            new CurrentAndPreviousValue(
                0.555555555555555555555555557m,
                0.740740740740740740740740741m),
            result.Snapshot.Macd.Line);
        Assert.Equal(
            new CurrentAndPreviousValue(
                -0.0555555555555555555555555545m,
                0.4753086419753086419753086425m),
            result.Snapshot.Macd.Signal);
        Assert.Equal(
            new CurrentAndPreviousValue(
                0.6111111111111111111111111115m,
                0.2654320987654320987654320985m),
            result.Snapshot.Macd.Histogram);
    }

    [Fact]
    public void IndicatorResults_ContainOnlyDecisionScalarsAndStableReproductionMetadata()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount);
        var request = Request(bars, parameters);

        var first = engine.Calculate(request, parameters);
        var second = engine.Calculate(request, parameters);

        Assert.Equal(7, first.IndicatorResults.Count);
        Assert.Equal(
            new[] { "MACD", "EMA20", "EMA50", "EMA200", "VolumeAverage20", "VolumeRatio", "ATR14" },
            first.IndicatorResults.Select(item => item.Key));
        Assert.Equal(
            first.IndicatorResults.Select(item => item.InputHash),
            second.IndicatorResults.Select(item => item.InputHash));
        Assert.All(first.IndicatorResults, item => Assert.Equal(FirstBarDate, item.CalculationStartBarDate));
        Assert.Equal(TechnicalIndicatorEngine.EmaAlgorithmId, first.IndicatorResults.Single(item => item.Key == "EMA200").AlgorithmId);
        Assert.Equal(TechnicalIndicatorEngine.MacdAlgorithmId, first.IndicatorResults.Single(item => item.Key == "MACD").AlgorithmId);
        Assert.Equal(TechnicalIndicatorEngine.AtrAlgorithmId, first.IndicatorResults.Single(item => item.Key == "ATR14").AlgorithmId);
        Assert.Equal(TechnicalIndicatorEngine.EngineVersion, ((ITechnicalIndicatorEngine)engine).Version);
        var identity = Assert.IsType<TechnicalIndicatorCalculationIdentity>(first.Identity);
        Assert.Equal(request.Manifest.AnalysisRunId, identity.AnalysisRunId);
        Assert.Equal(request.Manifest.Id, identity.AnalysisInputManifestId);
        Assert.Equal(request.Manifest.InstrumentId, identity.InstrumentId);
        Assert.Equal(request.EvaluationBarDate, identity.EvaluationBarDate);
        Assert.Equal(request.Manifest.ManifestHash, identity.ManifestHash);

        foreach (var indicator in first.IndicatorResults)
        {
            using var document = JsonDocument.Parse(indicator.NormalizedDecisionValuesJson);
            var value = document.RootElement.GetProperty("value");
            Assert.Equal(
                request.EvaluationBarDate.ToString("yyyy-MM-dd"),
                value.GetProperty("evaluationBarDate").GetString());
            Assert.DoesNotContain("bars", indicator.NormalizedDecisionValuesJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(FirstBarDate.ToString("yyyy-MM-dd"), indicator.NormalizedDecisionValuesJson, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FullListingHistory_IsNotSilentlyTrimmedToTheMinimumWindow()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var fullHistory = Enumerable.Range(0, 250)
            .Select(index =>
            {
                var close = index < 49 ? 100m : 200m;
                return Bar(index, close, close + 2m, close - 2m, close, 1_000m);
            })
            .ToArray();
        var minimumWindow = fullHistory.Skip(49).ToArray();

        var full = engine.Calculate(Request(fullHistory, parameters), parameters);
        var trimmed = engine.Calculate(Request(minimumWindow, parameters), parameters);

        Assert.True(full.IsSuccess);
        Assert.True(trimmed.IsSuccess);
        Assert.Equal(FirstBarDate, full.CalculationStartBarDate);
        Assert.Equal(FirstBarDate.AddDays(49), trimmed.CalculationStartBarDate);
        Assert.NotEqual(full.Snapshot!.Emas[200].Current, trimmed.Snapshot!.Emas[200].Current);
        Assert.Equal(200m, trimmed.Snapshot.Emas[200].Current);
    }

    [Fact]
    public void SourceContentHashes_MustReconstructTheManifestRevisionSet()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount);
        var validRequest = Request(bars, parameters);
        var tampered = bars.ToArray();
        var last = tampered[^1];
        tampered[^1] = new PointInTimeAdjustedDailyBar(
            last.SourceRevisionId,
            Hash('d'),
            last.BarDate,
            last.Open,
            last.High,
            last.Low,
            last.Close,
            last.Volume,
            last.SourceStatus);
        var tamperedSeries = new VerifiedPointInTimeAdjustedPriceSeries(
            validRequest.Manifest,
            validRequest.AdjustedSeriesStatus,
            tampered,
            validRequest.PriceSeries.VerifiedRevisionIdsByBarDate,
            validRequest.Manifest.CorporateActionSetHash,
            validRequest.Manifest.ManifestHash);
        var tamperedRequest = new TechnicalIndicatorCalculationRequest(
            tamperedSeries,
            validRequest.EvaluationBarDate);

        var result = engine.Calculate(tamperedRequest, parameters);

        Assert.Equal(TechnicalIndicatorCalculationStatus.InvalidData, result.Status);
        Assert.Contains("revision set hash", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceRevisionIds_MustMatchTheVerifiedManifestReconstruction()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount);
        var validRequest = Request(bars, parameters);
        var tampered = bars.ToArray();
        var last = tampered[^1];
        tampered[^1] = new PointInTimeAdjustedDailyBar(
            Guid.NewGuid(),
            last.SourceContentHash,
            last.BarDate,
            last.Open,
            last.High,
            last.Low,
            last.Close,
            last.Volume,
            last.SourceStatus);
        var tamperedSeries = new VerifiedPointInTimeAdjustedPriceSeries(
            validRequest.Manifest,
            validRequest.AdjustedSeriesStatus,
            tampered,
            validRequest.PriceSeries.VerifiedRevisionIdsByBarDate,
            validRequest.Manifest.CorporateActionSetHash,
            validRequest.Manifest.ManifestHash);

        var result = engine.Calculate(
            new TechnicalIndicatorCalculationRequest(tamperedSeries, validRequest.EvaluationBarDate),
            parameters);

        Assert.Equal(TechnicalIndicatorCalculationStatus.InvalidData, result.Status);
        Assert.Contains("revision IDs", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecimalScale_DoesNotChangeCanonicalJsonOrInputHashes()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount);
        var scaled = bars.ToArray();
        var last = scaled[^1];
        scaled[^1] = new PointInTimeAdjustedDailyBar(
            last.SourceRevisionId,
            last.SourceContentHash,
            last.BarDate,
            decimal.Parse($"{last.Open}.00", System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse($"{last.High}.00", System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse($"{last.Low}.00", System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse($"{last.Close}.00", System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse($"{last.Volume}.00", System.Globalization.CultureInfo.InvariantCulture),
            last.SourceStatus);

        var canonical = engine.Calculate(Request(bars, parameters), parameters);
        var differentScale = engine.Calculate(Request(scaled, parameters), parameters);

        Assert.Equal(
            canonical.IndicatorResults.Select(item => item.NormalizedDecisionValuesJson),
            differentScale.IndicatorResults.Select(item => item.NormalizedDecisionValuesJson));
        Assert.Equal(
            canonical.IndicatorResults.Select(item => item.InputHash),
            differentScale.IndicatorResults.Select(item => item.InputHash));
    }

    [Fact]
    public void RequestAndResultCollections_AreActuallyReadOnly()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount);
        var request = Request(bars, parameters);
        var result = engine.Calculate(request, parameters);

        Assert.Throws<NotSupportedException>(() =>
            ((IList<PointInTimeAdjustedDailyBar>)request.Bars)[0] = bars[1]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<int, CurrentAndPreviousValue>)result.Snapshot!.Emas)
            .Add(999, new CurrentAndPreviousValue(1m, 1m)));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<IndicatorResult>)result.IndicatorResults).Clear());
    }

    [Fact]
    public void IndicatorSpecificHash_ChangesOnlyForIndicatorsThatConsumeChangedField()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var originalBars = CreateLinearBars(parameters.RequiredBarCount);
        var changedBars = originalBars.ToArray();
        var original = changedBars[^1];
        changedBars[^1] = new PointInTimeAdjustedDailyBar(
            original.SourceRevisionId,
            original.SourceContentHash,
            original.BarDate,
            original.Open,
            original.High,
            original.Low,
            original.Close + 0.5m,
            original.Volume,
            original.SourceStatus);

        var before = engine.Calculate(Request(originalBars, parameters), parameters);
        var after = engine.Calculate(Request(changedBars, parameters), parameters);
        var beforeHashes = before.IndicatorResults.ToDictionary(item => item.Key, item => item.InputHash);
        var afterHashes = after.IndicatorResults.ToDictionary(item => item.Key, item => item.InputHash);

        Assert.NotEqual(beforeHashes["EMA20"], afterHashes["EMA20"]);
        Assert.NotEqual(beforeHashes["MACD"], afterHashes["MACD"]);
        Assert.NotEqual(beforeHashes["ATR14"], afterHashes["ATR14"]);
        Assert.Equal(beforeHashes["VolumeAverage20"], afterHashes["VolumeAverage20"]);
        Assert.Equal(beforeHashes["VolumeRatio"], afterHashes["VolumeRatio"]);
    }

    [Fact]
    public void SplitAdjustedUnits_ScalePriceIndicatorsAndPreserveVolumeRatio()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var originalBars = CreateLinearBars(parameters.RequiredBarCount);
        var adjustedBars = originalBars
            .Select(bar => new PointInTimeAdjustedDailyBar(
                bar.SourceRevisionId,
                bar.SourceContentHash,
                bar.BarDate,
                bar.Open / 2m,
                bar.High / 2m,
                bar.Low / 2m,
                bar.Close / 2m,
                bar.Volume * 2m,
                bar.SourceStatus))
            .ToArray();

        var original = engine.Calculate(
            Request(originalBars, parameters, manifestHashCharacter: 'e'),
            parameters).Snapshot!;
        var adjusted = engine.Calculate(
            Request(adjustedBars, parameters, manifestHashCharacter: 'f'),
            parameters).Snapshot!;

        Assert.Equal(original.Emas[200].Current / 2m, adjusted.Emas[200].Current);
        Assert.Equal(original.Macd.Line.Current / 2m, adjusted.Macd.Line.Current);
        Assert.Equal(original.Atr / 2m, adjusted.Atr);
        Assert.Equal(original.Volume.ReferenceAverageVolume * 2m, adjusted.Volume.ReferenceAverageVolume);
        Assert.Equal(original.Volume.Ratio, adjusted.Volume.Ratio);
    }

    [Fact]
    public void ZeroReferenceVolume_DoesNotInventARatio()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount)
            .Select((bar, index) => new PointInTimeAdjustedDailyBar(
                bar.SourceRevisionId,
                bar.SourceContentHash,
                bar.BarDate,
                bar.Open,
                bar.High,
                bar.Low,
                bar.Close,
                index == parameters.RequiredBarCount - 1 ? 100m : 0m,
                bar.SourceStatus))
            .ToArray();

        var result = engine.Calculate(Request(bars, parameters), parameters);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Snapshot!.Volume.Ratio);
        Assert.Equal(VolumeRatioStatus.ReferenceAverageZero, result.Snapshot.Volume.RatioStatus);
        var stored = result.IndicatorResults.Single(item => item.Key == "VolumeRatio");
        using var document = JsonDocument.Parse(stored.NormalizedDecisionValuesJson);
        Assert.Equal(
            JsonValueKind.Null,
            document.RootElement.GetProperty("value").GetProperty("current").ValueKind);
    }

    [Fact]
    public void TwoHundredBars_DoNotFallBackToShorterTrendIndicators()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(200);

        var result = engine.Calculate(
            Request(bars, parameters, historyStatus: HistoryStatus.InsufficientHistory),
            parameters);

        Assert.Equal(TechnicalIndicatorCalculationStatus.InsufficientHistory, result.Status);
        Assert.Equal(200, result.ActualBarCount);
        Assert.Equal(201, result.RequiredBarCount);
        Assert.Null(result.Snapshot);
        Assert.Empty(result.IndicatorResults);
    }

    [Theory]
    [InlineData(HistoryStatus.HistoryIncomplete, PointInTimeStatus.Verified, AdjustedPriceSeriesStatus.Ready, TechnicalIndicatorCalculationStatus.HistoryIncomplete)]
    [InlineData(HistoryStatus.Complete, PointInTimeStatus.Unverified, AdjustedPriceSeriesStatus.Ready, TechnicalIndicatorCalculationStatus.PointInTimeUnverified)]
    [InlineData(HistoryStatus.Complete, PointInTimeStatus.Verified, AdjustedPriceSeriesStatus.ReconciliationRequired, TechnicalIndicatorCalculationStatus.ReconciliationRequired)]
    [InlineData((HistoryStatus)99, PointInTimeStatus.Verified, AdjustedPriceSeriesStatus.Ready, TechnicalIndicatorCalculationStatus.InvalidData)]
    [InlineData(HistoryStatus.Complete, (PointInTimeStatus)99, AdjustedPriceSeriesStatus.Ready, TechnicalIndicatorCalculationStatus.InvalidData)]
    [InlineData(HistoryStatus.Complete, PointInTimeStatus.Verified, (AdjustedPriceSeriesStatus)99, TechnicalIndicatorCalculationStatus.InvalidData)]
    public void UnusableSeries_AreNotCalculated(
        HistoryStatus historyStatus,
        PointInTimeStatus pointInTimeStatus,
        AdjustedPriceSeriesStatus adjustedSeriesStatus,
        TechnicalIndicatorCalculationStatus expectedStatus)
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount);

        var result = engine.Calculate(
            Request(bars, parameters, historyStatus, pointInTimeStatus, adjustedSeriesStatus),
            parameters);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.Snapshot);
        Assert.Empty(result.IndicatorResults);
    }

    [Fact]
    public void FutureBarAfterEvaluationDate_IsRejectedInsteadOfUsed()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount + 1);
        var request = Request(
            bars,
            parameters,
            evaluationBarDate: bars[^2].BarDate);

        var result = engine.Calculate(request, parameters);

        Assert.Equal(TechnicalIndicatorCalculationStatus.InvalidData, result.Status);
        Assert.Contains("future bar", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void DuplicateTradingDate_IsRejectedInsteadOfNormalized()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount).ToArray();
        var prior = bars[^2];
        var last = bars[^1];
        bars[^1] = new PointInTimeAdjustedDailyBar(
            last.SourceRevisionId,
            last.SourceContentHash,
            prior.BarDate,
            last.Open,
            last.High,
            last.Low,
            last.Close,
            last.Volume,
            last.SourceStatus);

        var result = engine.Calculate(Request(bars, parameters), parameters);

        Assert.Equal(TechnicalIndicatorCalculationStatus.InvalidData, result.Status);
        Assert.Contains("strictly ordered", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProvisionalEvaluationBar_IsRejected()
    {
        var parameters = TechnicalIndicatorParameters.Initial;
        var bars = CreateLinearBars(parameters.RequiredBarCount).ToArray();
        var last = bars[^1];
        bars[^1] = new PointInTimeAdjustedDailyBar(
            last.SourceRevisionId,
            last.SourceContentHash,
            last.BarDate,
            last.Open,
            last.High,
            last.Low,
            last.Close,
            last.Volume,
            BarStatus.Provisional);

        var result = engine.Calculate(Request(bars, parameters), parameters);

        Assert.Equal(TechnicalIndicatorCalculationStatus.InvalidData, result.Status);
        Assert.Contains("confirmed or corrected", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static TechnicalIndicatorCalculationRequest Request(
        IReadOnlyList<PointInTimeAdjustedDailyBar> bars,
        TechnicalIndicatorParameters parameters,
        HistoryStatus historyStatus = HistoryStatus.Complete,
        PointInTimeStatus pointInTimeStatus = PointInTimeStatus.Verified,
        AdjustedPriceSeriesStatus adjustedSeriesStatus = AdjustedPriceSeriesStatus.Ready,
        char manifestHashCharacter = 'e',
        DateOnly? evaluationBarDate = null)
    {
        var firstDate = bars.Count == 0 ? (DateOnly?)null : bars[0].BarDate;
        var lastDate = bars.Count == 0 ? (DateOnly?)null : bars[^1].BarDate;
        var instrumentId = InstrumentId.New();
        var provider = "test-provider";
        var priceRevisionSetHash = AnalysisInputHashing.CalculatePriceRevisionSetHash(
            instrumentId,
            provider,
            bars.Select(bar => (bar.BarDate, bar.SourceContentHash)));
        var manifest = new AnalysisInputManifest(
            Guid.NewGuid(),
            AnalysisRunId.New(),
            instrumentId,
            provider,
            Guid.NewGuid(),
            firstDate,
            lastDate,
            bars.Count,
            parameters.RequiredBarCount,
            historyStatus,
            pointInTimeStatus,
            priceRevisionSetHash,
            Hash('b'),
            Hash(manifestHashCharacter));
        var revisionIds = bars
            .GroupBy(bar => bar.BarDate)
            .ToDictionary(group => group.Key, group => group.First().SourceRevisionId);
        var verifiedSeries = new VerifiedPointInTimeAdjustedPriceSeries(
            manifest,
            adjustedSeriesStatus,
            bars,
            revisionIds,
            manifest.CorporateActionSetHash,
            manifest.ManifestHash);
        return new TechnicalIndicatorCalculationRequest(
            verifiedSeries,
            evaluationBarDate ?? lastDate ?? FirstBarDate);
    }

    private static PointInTimeAdjustedDailyBar[] CreateLinearBars(int count) =>
        Enumerable.Range(0, count)
            .Select(index =>
            {
                var close = 1_000m + index;
                return Bar(
                    index,
                    close,
                    close + 2m,
                    close - 2m,
                    close,
                    1_000m + index * 10m);
            })
            .ToArray();

    private static PointInTimeAdjustedDailyBar Bar(
        int dayOffset,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume) =>
        new(
            DeterministicGuid(dayOffset),
            Hash('c'),
            FirstBarDate.AddDays(dayOffset),
            open,
            high,
            low,
            close,
            volume,
            BarStatus.Confirmed);

    private static Guid DeterministicGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value + 1);
        return new Guid(bytes);
    }

    private static Sha256Hash Hash(char value) => new(new string(value, 64));
}
