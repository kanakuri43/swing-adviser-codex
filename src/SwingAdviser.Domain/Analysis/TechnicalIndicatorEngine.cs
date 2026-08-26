using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Analysis;

public enum AdjustedPriceSeriesStatus
{
    Ready,
    ReconciliationRequired,
}

public enum TechnicalIndicatorCalculationStatus
{
    Succeeded,
    InsufficientHistory,
    HistoryIncomplete,
    InvalidData,
    PointInTimeUnverified,
    ReconciliationRequired,
}

public enum VolumeRatioStatus
{
    Available,
    ReferenceAverageZero,
}

/// <summary>
/// A confirmed price revision after point-in-time corporate-action adjustment.
/// Provider adjusted-close values are deliberately not part of this contract.
/// </summary>
public sealed record PointInTimeAdjustedDailyBar
{
    internal PointInTimeAdjustedDailyBar(
        Guid sourceRevisionId,
        Sha256Hash sourceContentHash,
        DateOnly barDate,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume,
        BarStatus sourceStatus)
    {
        if (sourceRevisionId == Guid.Empty)
        {
            throw new ArgumentException("Source revision ID cannot be empty.", nameof(sourceRevisionId));
        }

        if (open <= 0m || high <= 0m || low <= 0m || close <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(open), "Adjusted OHLC values must be positive.");
        }

        if (volume < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Adjusted volume cannot be negative.");
        }

        if (high < open || high < low || high < close)
        {
            throw new DomainException("Adjusted high must be greater than or equal to open, low, and close.");
        }

        if (low > open || low > high || low > close)
        {
            throw new DomainException("Adjusted low must be less than or equal to open, high, and close.");
        }

        SourceRevisionId = sourceRevisionId;
        SourceContentHash = sourceContentHash;
        BarDate = barDate;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
        SourceStatus = sourceStatus;
    }

    public Guid SourceRevisionId { get; }
    public Sha256Hash SourceContentHash { get; }
    public DateOnly BarDate { get; }
    public decimal Open { get; }
    public decimal High { get; }
    public decimal Low { get; }
    public decimal Close { get; }
    public decimal Volume { get; }
    public BarStatus SourceStatus { get; }
}

public sealed record TechnicalIndicatorParameters
{
    public TechnicalIndicatorParameters(
        int macdFastPeriod,
        int macdSlowPeriod,
        int macdSignalPeriod,
        int shortEmaPeriod,
        int mediumEmaPeriod,
        int longEmaPeriod,
        int atrPeriod,
        int volumeAveragePeriod)
    {
        if (macdFastPeriod <= 0 || macdSlowPeriod <= 0 || macdSignalPeriod <= 0 ||
            shortEmaPeriod <= 0 || mediumEmaPeriod <= 0 || longEmaPeriod <= 0 ||
            atrPeriod <= 0 || volumeAveragePeriod <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(macdFastPeriod), "Indicator periods must be positive.");
        }

        if (macdFastPeriod >= macdSlowPeriod)
        {
            throw new ArgumentException("MACD fast period must be shorter than its slow period.", nameof(macdFastPeriod));
        }

        if (shortEmaPeriod >= mediumEmaPeriod || mediumEmaPeriod >= longEmaPeriod)
        {
            throw new ArgumentException("EMA periods must be strictly ordered from short to long.", nameof(shortEmaPeriod));
        }

        MacdFastPeriod = macdFastPeriod;
        MacdSlowPeriod = macdSlowPeriod;
        MacdSignalPeriod = macdSignalPeriod;
        ShortEmaPeriod = shortEmaPeriod;
        MediumEmaPeriod = mediumEmaPeriod;
        LongEmaPeriod = longEmaPeriod;
        AtrPeriod = atrPeriod;
        VolumeAveragePeriod = volumeAveragePeriod;
    }

    public static TechnicalIndicatorParameters Initial { get; } = new(
        macdFastPeriod: 12,
        macdSlowPeriod: 26,
        macdSignalPeriod: 9,
        shortEmaPeriod: 20,
        mediumEmaPeriod: 50,
        longEmaPeriod: 200,
        atrPeriod: 14,
        volumeAveragePeriod: 20);

    public int MacdFastPeriod { get; }
    public int MacdSlowPeriod { get; }
    public int MacdSignalPeriod { get; }
    public int ShortEmaPeriod { get; }
    public int MediumEmaPeriod { get; }
    public int LongEmaPeriod { get; }
    public int AtrPeriod { get; }
    public int VolumeAveragePeriod { get; }

    // EMA trend/crossover decisions require the current and prior long EMA.
    public int RequiredBarCount =>
        new[]
        {
            LongEmaPeriod + 1,
            MacdSlowPeriod + MacdSignalPeriod,
            AtrPeriod,
            VolumeAveragePeriod + 1,
        }.Max();
}

public sealed record TechnicalIndicatorCalculationRequest
{
    public TechnicalIndicatorCalculationRequest(
        VerifiedPointInTimeAdjustedPriceSeries priceSeries,
        DateOnly evaluationBarDate)
    {
        PriceSeries = priceSeries ?? throw new ArgumentNullException(nameof(priceSeries));
        EvaluationBarDate = evaluationBarDate;
    }

    public VerifiedPointInTimeAdjustedPriceSeries PriceSeries { get; }
    public DateOnly EvaluationBarDate { get; }
    public AnalysisInputManifest Manifest => PriceSeries.Manifest;
    public AdjustedPriceSeriesStatus AdjustedSeriesStatus => PriceSeries.Status;
    public IReadOnlyList<PointInTimeAdjustedDailyBar> Bars => PriceSeries.Bars;
}

/// <summary>
/// Immutable output of the Infrastructure point-in-time selection and corporate-action adjustment boundary.
/// Application code cannot construct or replace its adjusted bars directly.
/// </summary>
public sealed record VerifiedPointInTimeAdjustedPriceSeries
{
    internal VerifiedPointInTimeAdjustedPriceSeries(
        AnalysisInputManifest manifest,
        AdjustedPriceSeriesStatus status,
        IReadOnlyList<PointInTimeAdjustedDailyBar> bars,
        IReadOnlyDictionary<DateOnly, Guid> verifiedRevisionIdsByBarDate,
        Sha256Hash verifiedCorporateActionSetHash,
        Sha256Hash verifiedManifestHash)
    {
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Status = status;
        Bars = Array.AsReadOnly((bars ?? throw new ArgumentNullException(nameof(bars))).ToArray());
        VerifiedRevisionIdsByBarDate = new ReadOnlyDictionary<DateOnly, Guid>(
            new Dictionary<DateOnly, Guid>(
                verifiedRevisionIdsByBarDate ?? throw new ArgumentNullException(nameof(verifiedRevisionIdsByBarDate))));
        VerifiedCorporateActionSetHash = verifiedCorporateActionSetHash;
        VerifiedManifestHash = verifiedManifestHash;

        if (VerifiedCorporateActionSetHash != Manifest.CorporateActionSetHash ||
            VerifiedManifestHash != Manifest.ManifestHash)
        {
            throw new ArgumentException("Verified series proof hashes must match the analysis manifest.");
        }
    }

    public AnalysisInputManifest Manifest { get; }
    public AdjustedPriceSeriesStatus Status { get; }
    public IReadOnlyList<PointInTimeAdjustedDailyBar> Bars { get; }
    public IReadOnlyDictionary<DateOnly, Guid> VerifiedRevisionIdsByBarDate { get; }
    public Sha256Hash VerifiedCorporateActionSetHash { get; }
    public Sha256Hash VerifiedManifestHash { get; }
}

public sealed record CurrentAndPreviousValue(decimal Previous, decimal Current);

public sealed record MacdSnapshot(
    CurrentAndPreviousValue Line,
    CurrentAndPreviousValue Signal,
    CurrentAndPreviousValue Histogram);

public sealed record VolumeSnapshot(
    decimal CurrentVolume,
    decimal ReferenceAverageVolume,
    decimal? Ratio,
    VolumeRatioStatus RatioStatus);

public sealed record TechnicalIndicatorSnapshot
{
    public TechnicalIndicatorSnapshot(
        DateOnly evaluationBarDate,
        IReadOnlyDictionary<int, CurrentAndPreviousValue> emas,
        MacdSnapshot macd,
        decimal atr,
        VolumeSnapshot volume)
    {
        EvaluationBarDate = evaluationBarDate;
        Emas = new ReadOnlyDictionary<int, CurrentAndPreviousValue>(
            new Dictionary<int, CurrentAndPreviousValue>(
                emas ?? throw new ArgumentNullException(nameof(emas))));
        Macd = macd ?? throw new ArgumentNullException(nameof(macd));
        Atr = atr;
        Volume = volume ?? throw new ArgumentNullException(nameof(volume));
    }

    public DateOnly EvaluationBarDate { get; }
    public IReadOnlyDictionary<int, CurrentAndPreviousValue> Emas { get; }
    public MacdSnapshot Macd { get; }
    public decimal Atr { get; }
    public VolumeSnapshot Volume { get; }
}

public sealed record TechnicalIndicatorCalculationIdentity
{
    public TechnicalIndicatorCalculationIdentity(
        AnalysisRunId analysisRunId,
        Guid analysisInputManifestId,
        InstrumentId instrumentId,
        DateOnly evaluationBarDate,
        Sha256Hash manifestHash)
    {
        if (analysisRunId.Value == Guid.Empty)
        {
            throw new ArgumentException("Analysis run ID cannot be empty.", nameof(analysisRunId));
        }

        if (analysisInputManifestId == Guid.Empty)
        {
            throw new ArgumentException("Analysis input manifest ID cannot be empty.", nameof(analysisInputManifestId));
        }

        if (instrumentId.Value == Guid.Empty)
        {
            throw new ArgumentException("Instrument ID cannot be empty.", nameof(instrumentId));
        }

        AnalysisRunId = analysisRunId;
        AnalysisInputManifestId = analysisInputManifestId;
        InstrumentId = instrumentId;
        EvaluationBarDate = evaluationBarDate;
        ManifestHash = manifestHash;
    }

    public AnalysisRunId AnalysisRunId { get; }
    public Guid AnalysisInputManifestId { get; }
    public InstrumentId InstrumentId { get; }
    public DateOnly EvaluationBarDate { get; }
    public Sha256Hash ManifestHash { get; }

    internal static TechnicalIndicatorCalculationIdentity From(
        TechnicalIndicatorCalculationRequest request) =>
        new(
            request.Manifest.AnalysisRunId,
            request.Manifest.Id,
            request.Manifest.InstrumentId,
            request.EvaluationBarDate,
            request.Manifest.ManifestHash);
}

public sealed record TechnicalIndicatorCalculationResult
{
    private TechnicalIndicatorCalculationResult(
        TechnicalIndicatorCalculationStatus status,
        string reason,
        int actualBarCount,
        int requiredBarCount,
        DateOnly? calculationStartBarDate,
        TechnicalIndicatorSnapshot? snapshot,
        IReadOnlyList<IndicatorResult> indicatorResults,
        TechnicalIndicatorCalculationIdentity identity)
    {
        Status = status;
        Reason = reason;
        ActualBarCount = actualBarCount;
        RequiredBarCount = requiredBarCount;
        CalculationStartBarDate = calculationStartBarDate;
        Snapshot = snapshot;
        IndicatorResults = Array.AsReadOnly(indicatorResults.ToArray());
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    public TechnicalIndicatorCalculationStatus Status { get; }
    public string Reason { get; }
    public int ActualBarCount { get; }
    public int RequiredBarCount { get; }
    public DateOnly? CalculationStartBarDate { get; }
    public TechnicalIndicatorSnapshot? Snapshot { get; }
    public IReadOnlyList<IndicatorResult> IndicatorResults { get; }
    public TechnicalIndicatorCalculationIdentity Identity { get; }
    public bool IsSuccess => Status == TechnicalIndicatorCalculationStatus.Succeeded;

    internal static TechnicalIndicatorCalculationResult Failed(
        TechnicalIndicatorCalculationStatus status,
        string reason,
        int actualBarCount,
        int requiredBarCount,
        DateOnly? calculationStartBarDate,
        TechnicalIndicatorCalculationIdentity identity) =>
        new(status, reason, actualBarCount, requiredBarCount, calculationStartBarDate, null, [], identity);

    internal static TechnicalIndicatorCalculationResult Succeeded(
        int actualBarCount,
        int requiredBarCount,
        DateOnly calculationStartBarDate,
        TechnicalIndicatorSnapshot snapshot,
        IReadOnlyList<IndicatorResult> indicatorResults,
        TechnicalIndicatorCalculationIdentity identity) =>
        new(
            TechnicalIndicatorCalculationStatus.Succeeded,
            "All required technical indicators were calculated.",
            actualBarCount,
            requiredBarCount,
            calculationStartBarDate,
            snapshot,
            indicatorResults,
            identity);
}

public interface ITechnicalIndicatorEngine
{
    string Version { get; }

    TechnicalIndicatorCalculationResult Calculate(
        TechnicalIndicatorCalculationRequest request,
        TechnicalIndicatorParameters parameters);
}

public static class AnalysisInputHashing
{
    public const string PriceRevisionSetHashAlgorithmId = "price-revision-set-sha256-v1";

    public static Sha256Hash CalculatePriceRevisionSetHash(
        InstrumentId instrumentId,
        string provider,
        IEnumerable<(DateOnly BarDate, Sha256Hash ContentHash)> members)
    {
        provider = DomainGuard.Required(provider, nameof(provider));
        ArgumentNullException.ThrowIfNull(members);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("algorithm", PriceRevisionSetHashAlgorithmId);
            writer.WriteString("instrumentId", instrumentId.Value.ToString("D").ToLowerInvariant());
            writer.WriteString("provider", provider);
            writer.WriteStartArray("members");
            foreach (var member in members.OrderBy(item => item.BarDate))
            {
                writer.WriteStartObject();
                writer.WriteString("barDate", FormatDate(member.BarDate));
                writer.WriteString("contentSha256", member.ContentHash.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Hash(buffer.WrittenSpan);
    }

    internal static Sha256Hash Hash(ReadOnlySpan<byte> input) =>
        new(Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant());

    internal static string FormatDate(DateOnly value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public sealed class TechnicalIndicatorEngine : ITechnicalIndicatorEngine
{
    public const string EngineVersion = "technical-indicator-engine-v1";
    public const string EmaAlgorithmId = "ema-sma-seed-v1";
    public const string MacdAlgorithmId = "macd-ema-sma-seed-v1";
    public const string AtrAlgorithmId = "atr-wilder-v1";
    public const string VolumeAverageAlgorithmId = "volume-trailing-prior-bars-sma-v1";
    public const string VolumeRatioAlgorithmId = "volume-current-to-prior-average-v1";

    public string Version => EngineVersion;

    public TechnicalIndicatorCalculationResult Calculate(
        TechnicalIndicatorCalculationRequest request,
        TechnicalIndicatorParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(parameters);

        var identity = TechnicalIndicatorCalculationIdentity.From(request);
        var bars = request.Bars;
        var requiredBarCount = parameters.RequiredBarCount;
        var startDate = bars.Count == 0 ? (DateOnly?)null : bars[0].BarDate;
        var invalidReason = ValidateInputIdentity(request, parameters);
        if (invalidReason is not null)
        {
            return TechnicalIndicatorCalculationResult.Failed(
                TechnicalIndicatorCalculationStatus.InvalidData,
                invalidReason,
                bars.Count,
                requiredBarCount,
                startDate,
                identity);
        }

        if (request.AdjustedSeriesStatus == AdjustedPriceSeriesStatus.ReconciliationRequired)
        {
            return TechnicalIndicatorCalculationResult.Failed(
                TechnicalIndicatorCalculationStatus.ReconciliationRequired,
                "The adjusted price series contains a corporate action that requires reconciliation.",
                bars.Count,
                requiredBarCount,
                startDate,
                identity);
        }

        if (request.Manifest.PointInTimeStatus != PointInTimeStatus.Verified)
        {
            return TechnicalIndicatorCalculationResult.Failed(
                TechnicalIndicatorCalculationStatus.PointInTimeUnverified,
                "The input manifest is not point-in-time verified.",
                bars.Count,
                requiredBarCount,
                startDate,
                identity);
        }

        if (request.Manifest.HistoryStatus == HistoryStatus.HistoryIncomplete)
        {
            return TechnicalIndicatorCalculationResult.Failed(
                TechnicalIndicatorCalculationStatus.HistoryIncomplete,
                "The listing-to-evaluation price history is not known to be complete.",
                bars.Count,
                requiredBarCount,
                startDate,
                identity);
        }

        if (request.Manifest.HistoryStatus == HistoryStatus.Invalid)
        {
            return TechnicalIndicatorCalculationResult.Failed(
                TechnicalIndicatorCalculationStatus.InvalidData,
                "The input manifest marks the price history as invalid.",
                bars.Count,
                requiredBarCount,
                startDate,
                identity);
        }

        if (request.Manifest.HistoryStatus == HistoryStatus.InsufficientHistory || bars.Count < requiredBarCount)
        {
            return TechnicalIndicatorCalculationResult.Failed(
                TechnicalIndicatorCalculationStatus.InsufficientHistory,
                $"Insufficient history: {bars.Count} available bars; {requiredBarCount} required.",
                bars.Count,
                requiredBarCount,
                startDate,
                identity);
        }

        if (bars.Any(bar => bar.SourceStatus is not (BarStatus.Confirmed or BarStatus.Corrected)))
        {
            return TechnicalIndicatorCalculationResult.Failed(
                TechnicalIndicatorCalculationStatus.InvalidData,
                "Only confirmed or corrected daily bars may be used for technical indicators.",
                bars.Count,
                requiredBarCount,
                startDate,
                identity);
        }

        var closes = bars.Select(bar => bar.Close).ToArray();
        var shortEma = CalculateEma(closes, parameters.ShortEmaPeriod);
        var mediumEma = CalculateEma(closes, parameters.MediumEmaPeriod);
        var longEma = CalculateEma(closes, parameters.LongEmaPeriod);
        var macdFastEma = CalculateEma(closes, parameters.MacdFastPeriod);
        var macdSlowEma = CalculateEma(closes, parameters.MacdSlowPeriod);
        var macdLine = Subtract(macdFastEma, macdSlowEma);
        var macdSignal = CalculateEma(macdLine, parameters.MacdSignalPeriod);
        var macdHistogram = Subtract(macdLine, macdSignal);
        var atr = CalculateAtr(bars, parameters.AtrPeriod);

        var lastIndex = bars.Count - 1;
        var previousIndex = lastIndex - 1;
        var emaSnapshots = new Dictionary<int, CurrentAndPreviousValue>
        {
            [parameters.ShortEmaPeriod] = Pair(shortEma, previousIndex, lastIndex),
            [parameters.MediumEmaPeriod] = Pair(mediumEma, previousIndex, lastIndex),
            [parameters.LongEmaPeriod] = Pair(longEma, previousIndex, lastIndex),
        };
        var macd = new MacdSnapshot(
            Pair(macdLine, previousIndex, lastIndex),
            Pair(macdSignal, previousIndex, lastIndex),
            Pair(macdHistogram, previousIndex, lastIndex));
        var volume = CalculateVolumeSnapshot(bars, parameters.VolumeAveragePeriod);
        var snapshot = new TechnicalIndicatorSnapshot(
            request.EvaluationBarDate,
            emaSnapshots,
            macd,
            Required(atr[lastIndex]),
            volume);
        var results = CreateIndicatorResults(request, parameters, snapshot);

        return TechnicalIndicatorCalculationResult.Succeeded(
            bars.Count,
            requiredBarCount,
            bars[0].BarDate,
            snapshot,
            results,
            identity);
    }

    private static string? ValidateInputIdentity(
        TechnicalIndicatorCalculationRequest request,
        TechnicalIndicatorParameters parameters)
    {
        var manifest = request.Manifest;
        var bars = request.Bars;
        if (!Enum.IsDefined(manifest.HistoryStatus) ||
            !Enum.IsDefined(manifest.PointInTimeStatus) ||
            !Enum.IsDefined(request.AdjustedSeriesStatus))
        {
            return "The adjusted series contains an undefined readiness status.";
        }

        if (manifest.BarCount != bars.Count)
        {
            return "The adjusted bar count does not match the analysis input manifest.";
        }

        if (manifest.RequiredBarCount < parameters.RequiredBarCount)
        {
            return "The analysis input manifest was not created with the engine's required history count.";
        }

        if (bars.Count == 0)
        {
            if (manifest.FirstBarDate is not null || manifest.LastBarDate is not null)
            {
                return "An empty adjusted series must have an empty manifest date range.";
            }

            var emptySetHash = AnalysisInputHashing.CalculatePriceRevisionSetHash(
                manifest.InstrumentId,
                manifest.PriceProvider,
                []);
            return emptySetHash == manifest.PriceRevisionSetHash
                ? null
                : "The empty adjusted series does not match the manifest price revision set hash.";
        }

        if (manifest.FirstBarDate != bars[0].BarDate || manifest.LastBarDate != bars[^1].BarDate)
        {
            return "The adjusted series date range does not match the analysis input manifest.";
        }

        if (bars[^1].BarDate != request.EvaluationBarDate)
        {
            return bars[^1].BarDate > request.EvaluationBarDate
                ? "The adjusted series contains a future bar after the evaluation date."
                : "The evaluation date is not the latest confirmed bar in the adjusted series.";
        }

        for (var index = 1; index < bars.Count; index++)
        {
            if (bars[index].BarDate <= bars[index - 1].BarDate)
            {
                return "Adjusted bars must be unique and strictly ordered by trading date.";
            }
        }

        if (bars.Select(bar => bar.SourceRevisionId).Distinct().Count() != bars.Count)
        {
            return "An adjusted series cannot reuse a source price revision across multiple bars.";
        }

        if (request.PriceSeries.VerifiedRevisionIdsByBarDate.Count != bars.Count ||
            bars.Any(bar =>
                !request.PriceSeries.VerifiedRevisionIdsByBarDate.TryGetValue(bar.BarDate, out var revisionId) ||
                revisionId != bar.SourceRevisionId))
        {
            return "The adjusted series source revision IDs do not match the verified manifest reconstruction.";
        }

        var reconstructedSetHash = AnalysisInputHashing.CalculatePriceRevisionSetHash(
            manifest.InstrumentId,
            manifest.PriceProvider,
            bars.Select(bar => (bar.BarDate, bar.SourceContentHash)));
        if (reconstructedSetHash != manifest.PriceRevisionSetHash)
        {
            return "The adjusted series source revisions do not match the manifest price revision set hash.";
        }

        return null;
    }

    private static decimal?[] CalculateEma(IReadOnlyList<decimal> source, int period)
    {
        var nullableSource = source.Select(value => (decimal?)value).ToArray();
        return CalculateEma(nullableSource, period);
    }

    private static decimal?[] CalculateEma(IReadOnlyList<decimal?> source, int period)
    {
        var result = new decimal?[source.Count];
        var first = -1;
        for (var index = 0; index < source.Count; index++)
        {
            if (source[index].HasValue)
            {
                first = index;
                break;
            }
        }

        if (first < 0 || source.Count - first < period)
        {
            return result;
        }

        var seed = 0m;
        for (var index = first; index < first + period; index++)
        {
            if (source[index] is not { } value)
            {
                return result;
            }

            seed += value;
        }

        seed /= period;
        var seedIndex = first + period - 1;
        result[seedIndex] = seed;
        var alpha = 2m / (period + 1m);
        for (var index = seedIndex + 1; index < source.Count; index++)
        {
            if (source[index] is not { } value || result[index - 1] is not { } previous)
            {
                continue;
            }

            result[index] = alpha * value + (1m - alpha) * previous;
        }

        return result;
    }

    private static decimal?[] Subtract(
        IReadOnlyList<decimal?> left,
        IReadOnlyList<decimal?> right)
    {
        var result = new decimal?[left.Count];
        for (var index = 0; index < result.Length; index++)
        {
            if (left[index] is { } leftValue && right[index] is { } rightValue)
            {
                result[index] = leftValue - rightValue;
            }
        }

        return result;
    }

    private static decimal?[] CalculateAtr(
        IReadOnlyList<PointInTimeAdjustedDailyBar> bars,
        int period)
    {
        var result = new decimal?[bars.Count];
        if (bars.Count < period)
        {
            return result;
        }

        var trueRanges = new decimal[bars.Count];
        trueRanges[0] = bars[0].High - bars[0].Low;
        for (var index = 1; index < bars.Count; index++)
        {
            var highLow = bars[index].High - bars[index].Low;
            var highPreviousClose = Math.Abs(bars[index].High - bars[index - 1].Close);
            var lowPreviousClose = Math.Abs(bars[index].Low - bars[index - 1].Close);
            trueRanges[index] = Math.Max(highLow, Math.Max(highPreviousClose, lowPreviousClose));
        }

        var initial = 0m;
        for (var index = 0; index < period; index++)
        {
            initial += trueRanges[index];
        }

        initial /= period;
        result[period - 1] = initial;
        for (var index = period; index < bars.Count; index++)
        {
            result[index] = (Required(result[index - 1]) * (period - 1m) + trueRanges[index]) / period;
        }

        return result;
    }

    private static VolumeSnapshot CalculateVolumeSnapshot(
        IReadOnlyList<PointInTimeAdjustedDailyBar> bars,
        int period)
    {
        var lastIndex = bars.Count - 1;
        var average = 0m;
        for (var index = lastIndex - period; index < lastIndex; index++)
        {
            average += bars[index].Volume;
        }

        average /= period;
        if (average == 0m)
        {
            return new VolumeSnapshot(
                bars[lastIndex].Volume,
                average,
                null,
                VolumeRatioStatus.ReferenceAverageZero);
        }

        return new VolumeSnapshot(
            bars[lastIndex].Volume,
            average,
            bars[lastIndex].Volume / average,
            VolumeRatioStatus.Available);
    }

    private static CurrentAndPreviousValue Pair(
        IReadOnlyList<decimal?> values,
        int previousIndex,
        int currentIndex) =>
        new(Required(values[previousIndex]), Required(values[currentIndex]));

    private static decimal Required(decimal? value) =>
        value ?? throw new InvalidOperationException("Required indicator warm-up unexpectedly produced no value.");

    private static IReadOnlyList<IndicatorResult> CreateIndicatorResults(
        TechnicalIndicatorCalculationRequest request,
        TechnicalIndicatorParameters parameters,
        TechnicalIndicatorSnapshot snapshot)
    {
        var results = new List<IndicatorResult>();
        var ordinal = 1;
        var macdParameters = JsonObject(writer =>
        {
            writer.WriteNumber("fastPeriod", parameters.MacdFastPeriod);
            writer.WriteNumber("slowPeriod", parameters.MacdSlowPeriod);
            writer.WriteNumber("signalPeriod", parameters.MacdSignalPeriod);
        });
        results.Add(Result(
            "MACD",
            MacdAlgorithmId,
            macdParameters,
            MacdValuesJson(snapshot),
            request,
            ordinal++,
            IndicatorInputKind.Close));

        foreach (var period in new[]
                 {
                     parameters.ShortEmaPeriod,
                     parameters.MediumEmaPeriod,
                     parameters.LongEmaPeriod,
                 })
        {
            var emaParameters = JsonObject(writer => writer.WriteNumber("period", period));
            results.Add(Result(
                $"EMA{period}",
                EmaAlgorithmId,
                emaParameters,
                PairValuesJson(snapshot.EvaluationBarDate, snapshot.Emas[period]),
                request,
                ordinal++,
                IndicatorInputKind.Close));
        }

        var volumeParameters = JsonObject(writer =>
        {
            writer.WriteNumber("period", parameters.VolumeAveragePeriod);
            writer.WriteString("window", "prior-bars-excluding-evaluation-bar");
        });
        results.Add(Result(
            $"VolumeAverage{parameters.VolumeAveragePeriod}",
            VolumeAverageAlgorithmId,
            volumeParameters,
            ScalarValuesJson(snapshot.EvaluationBarDate, snapshot.Volume.ReferenceAverageVolume),
            request,
            ordinal++,
            IndicatorInputKind.Volume));
        results.Add(Result(
            "VolumeRatio",
            VolumeRatioAlgorithmId,
            volumeParameters,
            VolumeRatioValuesJson(snapshot),
            request,
            ordinal++,
            IndicatorInputKind.Volume));

        var atrParameters = JsonObject(writer => writer.WriteNumber("period", parameters.AtrPeriod));
        results.Add(Result(
            $"ATR{parameters.AtrPeriod}",
            AtrAlgorithmId,
            atrParameters,
            ScalarValuesJson(snapshot.EvaluationBarDate, snapshot.Atr),
            request,
            ordinal,
            IndicatorInputKind.HighLowClose));
        return results;
    }

    private static IndicatorResult Result(
        string key,
        string algorithmId,
        string parametersJson,
        string valuesJson,
        TechnicalIndicatorCalculationRequest request,
        int ordinal,
        IndicatorInputKind inputKind) =>
        new(
            key,
            algorithmId,
            parametersJson,
            valuesJson,
            request.Bars[0].BarDate,
            CalculateInputHash(key, algorithmId, parametersJson, request, inputKind),
            ordinal);

    private static string PairValuesJson(DateOnly evaluationBarDate, CurrentAndPreviousValue pair) =>
        JsonObject(writer =>
        {
            WriteDecisionHeader(writer, evaluationBarDate);
            WriteDecimal(writer, "previous", pair.Previous);
            WriteDecimal(writer, "current", pair.Current);
        });

    private static string ScalarValuesJson(DateOnly evaluationBarDate, decimal current) =>
        JsonObject(writer =>
        {
            WriteDecisionHeader(writer, evaluationBarDate);
            WriteDecimal(writer, "current", current);
        });

    private static string MacdValuesJson(TechnicalIndicatorSnapshot snapshot) =>
        JsonObject(writer =>
        {
            WriteDecisionHeader(writer, snapshot.EvaluationBarDate);
            writer.WriteStartObject("previous");
            WriteDecimal(writer, "line", snapshot.Macd.Line.Previous);
            WriteDecimal(writer, "signal", snapshot.Macd.Signal.Previous);
            WriteDecimal(writer, "histogram", snapshot.Macd.Histogram.Previous);
            writer.WriteEndObject();
            writer.WriteStartObject("current");
            WriteDecimal(writer, "line", snapshot.Macd.Line.Current);
            WriteDecimal(writer, "signal", snapshot.Macd.Signal.Current);
            WriteDecimal(writer, "histogram", snapshot.Macd.Histogram.Current);
            writer.WriteEndObject();
        });

    private static string VolumeRatioValuesJson(TechnicalIndicatorSnapshot snapshot) =>
        JsonObject(writer =>
        {
            WriteDecisionHeader(writer, snapshot.EvaluationBarDate);
            WriteDecimal(writer, "currentVolume", snapshot.Volume.CurrentVolume);
            WriteDecimal(writer, "referenceAverage", snapshot.Volume.ReferenceAverageVolume);
            if (snapshot.Volume.Ratio is { } ratio)
            {
                WriteDecimal(writer, "current", ratio);
            }
            else
            {
                writer.WriteNull("current");
            }

            writer.WriteString("status", snapshot.Volume.RatioStatus.ToString());
        });

    private static void WriteDecisionHeader(Utf8JsonWriter writer, DateOnly evaluationBarDate)
    {
        writer.WriteString("evaluationBarDate", FormatDate(evaluationBarDate));
    }

    private static string JsonObject(Action<Utf8JsonWriter> writeProperties)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "1");
            writer.WriteStartObject("value");
            writeProperties(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static Sha256Hash CalculateInputHash(
        string key,
        string algorithmId,
        string parametersJson,
        TechnicalIndicatorCalculationRequest request,
        IndicatorInputKind inputKind)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("hashAlgorithm", "indicator-input-sha256-v1");
            writer.WriteString("indicatorKey", key);
            writer.WriteString("algorithmId", algorithmId);
            writer.WriteString("parametersJson", parametersJson);
            writer.WriteString("evaluationBarDate", FormatDate(request.EvaluationBarDate));
            writer.WriteString("manifestSha256", request.Manifest.ManifestHash.Value);
            writer.WriteStartArray("bars");
            foreach (var bar in request.Bars)
            {
                writer.WriteStartObject();
                writer.WriteString("sourceRevisionId", bar.SourceRevisionId.ToString("D").ToLowerInvariant());
                writer.WriteString("sourceContentSha256", bar.SourceContentHash.Value);
                writer.WriteString("barDate", FormatDate(bar.BarDate));
                switch (inputKind)
                {
                    case IndicatorInputKind.Close:
                        WriteDecimal(writer, "close", bar.Close);
                        break;
                    case IndicatorInputKind.HighLowClose:
                        WriteDecimal(writer, "high", bar.High);
                        WriteDecimal(writer, "low", bar.Low);
                        WriteDecimal(writer, "close", bar.Close);
                        break;
                    case IndicatorInputKind.Volume:
                        WriteDecimal(writer, "volume", bar.Volume);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(inputKind));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return AnalysisInputHashing.Hash(buffer.WrittenSpan);
    }

    private static void WriteDecimal(Utf8JsonWriter writer, string propertyName, decimal value) =>
        AnalysisCanonicalJson.WriteDecimal(writer, propertyName, value);

    private static string CanonicalDecimal(decimal value) =>
        AnalysisCanonicalJson.FormatDecimal(value);

    private static string FormatDate(DateOnly value) =>
        AnalysisInputHashing.FormatDate(value);

    private enum IndicatorInputKind
    {
        Close,
        HighLowClose,
        Volume,
    }
}
