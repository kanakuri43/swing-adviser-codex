using System.Buffers;
using System.Text;
using System.Text.Json;
using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.MarketData;

namespace SwingAdviser.Domain.Analysis;

public sealed record CandidateUniverseParameters
{
    public CandidateUniverseParameters(
        IEnumerable<string> exchangeCodes,
        IEnumerable<SecurityType> securityTypes,
        bool requireListed,
        bool requireExplicitScanEligibility)
    {
        ArgumentNullException.ThrowIfNull(exchangeCodes);
        ArgumentNullException.ThrowIfNull(securityTypes);
        ExchangeCodes = Array.AsReadOnly(exchangeCodes
            .Select(code => DomainGuard.Required(code, nameof(exchangeCodes)).ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray());
        SecurityTypes = Array.AsReadOnly(securityTypes
            .Distinct()
            .OrderBy(type => type)
            .ToArray());
        if (ExchangeCodes.Count == 0 || SecurityTypes.Count == 0)
        {
            throw new ArgumentException("The candidate universe must include an exchange and a security type.");
        }

        if (SecurityTypes.Any(type => !Enum.IsDefined(type)))
        {
            throw new ArgumentOutOfRangeException(nameof(securityTypes));
        }

        RequireListed = requireListed;
        RequireExplicitScanEligibility = requireExplicitScanEligibility;
    }

    public static CandidateUniverseParameters Initial { get; } = new(
        ["TSE"],
        [SecurityType.DomesticCommonStock],
        requireListed: true,
        requireExplicitScanEligibility: true);

    public IReadOnlyList<string> ExchangeCodes { get; }
    public IReadOnlyList<SecurityType> SecurityTypes { get; }
    public bool RequireListed { get; }
    public bool RequireExplicitScanEligibility { get; }

    public bool Includes(InstrumentMasterRevision master, out string? exclusionReason)
    {
        ArgumentNullException.ThrowIfNull(master);
        if (!ExchangeCodes.Contains(master.ExchangeCode.ToUpperInvariant(), StringComparer.Ordinal))
        {
            exclusionReason = $"UNIVERSE_EXCHANGE_EXCLUDED: Exchange {master.ExchangeCode} is outside the configured universe.";
            return false;
        }

        if (!SecurityTypes.Contains(master.SecurityType))
        {
            exclusionReason = $"UNIVERSE_SECURITY_TYPE_EXCLUDED: Security type {master.SecurityType} is outside the configured universe.";
            return false;
        }

        if (RequireListed && master.ListingStatus != ListingStatus.Listed)
        {
            exclusionReason = $"UNIVERSE_LISTING_STATUS_EXCLUDED: Listing status is {master.ListingStatus}.";
            return false;
        }

        if (RequireExplicitScanEligibility && master.ScanEligibility != ScanEligibility.Eligible)
        {
            exclusionReason = master.ScanEligibility == ScanEligibility.Excluded
                ? $"UNIVERSE_SCAN_EXCLUDED: {master.ExclusionReason}"
                : "UNIVERSE_SCAN_UNKNOWN: Scan eligibility is unknown and is not inferred as eligible.";
            return false;
        }

        exclusionReason = null;
        return true;
    }
}

public sealed record CandidateScoreWeights
{
    public CandidateScoreWeights(decimal macd, decimal ema, decimal volume)
    {
        if (macd < 0m || ema < 0m || volume < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(macd), "Candidate score weights cannot be negative.");
        }

        if (macd + ema + volume != 100m)
        {
            throw new ArgumentException("Candidate score weights must total exactly 100.");
        }

        Macd = macd;
        Ema = ema;
        Volume = volume;
    }

    public decimal Macd { get; }
    public decimal Ema { get; }
    public decimal Volume { get; }
}

public sealed record CandidateDirectionParameters
{
    public CandidateDirectionParameters(
        bool requireVolume,
        decimal minimumVolumeRatio,
        CandidateScoreWeights weights)
    {
        if (minimumVolumeRatio <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumVolumeRatio),
                "The minimum volume ratio must be positive.");
        }

        Weights = weights ?? throw new ArgumentNullException(nameof(weights));
        if (!requireVolume && weights.Volume > 0m)
        {
            throw new ArgumentException(
                "Volume cannot receive score weight when it is not a required condition.",
                nameof(weights));
        }

        RequireVolume = requireVolume;
        MinimumVolumeRatio = minimumVolumeRatio;
    }

    public bool RequireVolume { get; }
    public decimal MinimumVolumeRatio { get; }
    public CandidateScoreWeights Weights { get; }
}

public sealed record CandidateScoringParameters
{
    public CandidateScoringParameters(
        CandidateDirectionParameters longEntry,
        CandidateDirectionParameters shortEntry,
        decimal atrNormalizationScale,
        decimal matchedBaseFraction,
        decimal volumeFullStrengthRatio,
        int highConfidenceMinimumScore,
        int mediumConfidenceMinimumScore)
    {
        LongEntry = longEntry ?? throw new ArgumentNullException(nameof(longEntry));
        ShortEntry = shortEntry ?? throw new ArgumentNullException(nameof(shortEntry));
        if (atrNormalizationScale <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atrNormalizationScale),
                "The ATR normalization scale must be positive.");
        }

        if (matchedBaseFraction is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(matchedBaseFraction),
                "The matched base fraction must be between zero and one.");
        }

        var highestScoredVolumeThreshold = new[] { longEntry, shortEntry }
            .Where(item => item.Weights.Volume > 0m)
            .Select(item => item.MinimumVolumeRatio)
            .DefaultIfEmpty(0m)
            .Max();
        if (volumeFullStrengthRatio <= highestScoredVolumeThreshold)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeFullStrengthRatio),
                "The full-strength volume ratio must exceed every scored volume threshold.");
        }

        if (mediumConfidenceMinimumScore is < 0 or > 100 ||
            highConfidenceMinimumScore is < 0 or > 100 ||
            mediumConfidenceMinimumScore >= highConfidenceMinimumScore)
        {
            throw new ArgumentException(
                "Confidence thresholds must satisfy 0 <= medium < high <= 100.");
        }

        AtrNormalizationScale = atrNormalizationScale;
        MatchedBaseFraction = matchedBaseFraction;
        VolumeFullStrengthRatio = volumeFullStrengthRatio;
        HighConfidenceMinimumScore = highConfidenceMinimumScore;
        MediumConfidenceMinimumScore = mediumConfidenceMinimumScore;
    }

    public static CandidateScoringParameters Initial { get; } = new(
        new CandidateDirectionParameters(
            requireVolume: true,
            minimumVolumeRatio: 1.5m,
            new CandidateScoreWeights(macd: 50m, ema: 50m, volume: 0m)),
        new CandidateDirectionParameters(
            requireVolume: true,
            minimumVolumeRatio: 1.5m,
            new CandidateScoreWeights(macd: 40m, ema: 40m, volume: 20m)),
        atrNormalizationScale: 1m,
        matchedBaseFraction: 0.5m,
        volumeFullStrengthRatio: 2m,
        highConfidenceMinimumScore: 80,
        mediumConfidenceMinimumScore: 60);

    public CandidateDirectionParameters LongEntry { get; }
    public CandidateDirectionParameters ShortEntry { get; }
    public decimal AtrNormalizationScale { get; }
    public decimal MatchedBaseFraction { get; }
    public decimal VolumeFullStrengthRatio { get; }
    public int HighConfidenceMinimumScore { get; }
    public int MediumConfidenceMinimumScore { get; }

    public CandidateDirectionParameters For(PositionSide side) => side switch
    {
        PositionSide.Long => LongEntry,
        PositionSide.Short => ShortEntry,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    public ConfidenceLevel Classify(int score)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        return score >= HighConfidenceMinimumScore
            ? ConfidenceLevel.High
            : score >= MediumConfidenceMinimumScore
                ? ConfidenceLevel.Medium
                : ConfidenceLevel.Low;
    }
}

public sealed record CandidateStrategyParameters
{
    public const string SchemaVersion = "candidate-strategy-parameters-v1";

    public CandidateStrategyParameters(
        TechnicalIndicatorParameters indicators,
        CandidateScoringParameters candidates)
        : this(CandidateUniverseParameters.Initial, indicators, candidates)
    {
    }

    public CandidateStrategyParameters(
        CandidateUniverseParameters universe,
        TechnicalIndicatorParameters indicators,
        CandidateScoringParameters candidates)
    {
        Universe = universe ?? throw new ArgumentNullException(nameof(universe));
        Indicators = indicators ?? throw new ArgumentNullException(nameof(indicators));
        Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
    }

    public static CandidateStrategyParameters Initial { get; } = new(
        CandidateUniverseParameters.Initial,
        TechnicalIndicatorParameters.Initial,
        CandidateScoringParameters.Initial);

    public CandidateUniverseParameters Universe { get; }
    public TechnicalIndicatorParameters Indicators { get; }
    public CandidateScoringParameters Candidates { get; }

    public string ToNormalizedJson()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WriteStartObject("universe");
            writer.WriteStartArray("exchangeCodes");
            foreach (var exchangeCode in Universe.ExchangeCodes)
            {
                writer.WriteStringValue(exchangeCode);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("securityTypes");
            foreach (var securityType in Universe.SecurityTypes)
            {
                writer.WriteStringValue(securityType.ToString());
            }

            writer.WriteEndArray();
            writer.WriteBoolean("requireListed", Universe.RequireListed);
            writer.WriteBoolean("requireExplicitScanEligibility", Universe.RequireExplicitScanEligibility);
            writer.WriteEndObject();
            writer.WriteStartObject("indicators");
            writer.WriteNumber("macdFastPeriod", Indicators.MacdFastPeriod);
            writer.WriteNumber("macdSlowPeriod", Indicators.MacdSlowPeriod);
            writer.WriteNumber("macdSignalPeriod", Indicators.MacdSignalPeriod);
            writer.WriteNumber("shortEmaPeriod", Indicators.ShortEmaPeriod);
            writer.WriteNumber("mediumEmaPeriod", Indicators.MediumEmaPeriod);
            writer.WriteNumber("longEmaPeriod", Indicators.LongEmaPeriod);
            writer.WriteNumber("atrPeriod", Indicators.AtrPeriod);
            writer.WriteNumber("volumeAveragePeriod", Indicators.VolumeAveragePeriod);
            writer.WriteEndObject();
            writer.WriteStartObject("candidateScoring");
            AnalysisCanonicalJson.WriteDecimal(
                writer,
                "atrNormalizationScale",
                Candidates.AtrNormalizationScale);
            AnalysisCanonicalJson.WriteDecimal(
                writer,
                "matchedBaseFraction",
                Candidates.MatchedBaseFraction);
            AnalysisCanonicalJson.WriteDecimal(
                writer,
                "volumeFullStrengthRatio",
                Candidates.VolumeFullStrengthRatio);
            writer.WriteNumber("highConfidenceMinimumScore", Candidates.HighConfidenceMinimumScore);
            writer.WriteNumber("mediumConfidenceMinimumScore", Candidates.MediumConfidenceMinimumScore);
            writer.WriteString("rounding", "AwayFromZero");
            WriteDirection(writer, "long", Candidates.LongEntry);
            WriteDirection(writer, "short", Candidates.ShortEntry);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public string ToSnapshotNormalizedJson(
        string strategyKey,
        string strategyVersion,
        string algorithmVersion = CandidateScoringEngine.EngineVersion)
    {
        strategyKey = DomainGuard.Required(strategyKey, nameof(strategyKey));
        strategyVersion = DomainGuard.Required(strategyVersion, nameof(strategyVersion));
        algorithmVersion = DomainGuard.Required(algorithmVersion, nameof(algorithmVersion));
        using var parameters = JsonDocument.Parse(ToNormalizedJson());
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WriteString("strategyKey", strategyKey);
            writer.WriteString("strategyVersion", strategyVersion);
            writer.WriteString("algorithmVersion", algorithmVersion);
            writer.WritePropertyName("parameters");
            parameters.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public Sha256Hash CalculateSnapshotHash(
        string strategyKey,
        string strategyVersion,
        string algorithmVersion = CandidateScoringEngine.EngineVersion) =>
        AnalysisInputHashing.Hash(Encoding.UTF8.GetBytes(ToSnapshotNormalizedJson(
            strategyKey,
            strategyVersion,
            algorithmVersion)));

    public StrategyParameterSnapshot CreateSnapshot(
        Guid id,
        string strategyKey,
        string strategyVersion,
        DateTimeOffset capturedAtUtc,
        string algorithmVersion = CandidateScoringEngine.EngineVersion)
    {
        var normalizedJson = ToSnapshotNormalizedJson(
            strategyKey,
            strategyVersion,
            algorithmVersion);
        return new StrategyParameterSnapshot(
            id,
            strategyKey,
            strategyVersion,
            SchemaVersion,
            algorithmVersion,
            normalizedJson,
            AnalysisInputHashing.Hash(Encoding.UTF8.GetBytes(normalizedJson)),
            capturedAtUtc);
    }

    private static void WriteDirection(
        Utf8JsonWriter writer,
        string propertyName,
        CandidateDirectionParameters direction)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteBoolean("requireVolume", direction.RequireVolume);
        AnalysisCanonicalJson.WriteDecimal(
            writer,
            "minimumVolumeRatio",
            direction.MinimumVolumeRatio);
        writer.WriteStartObject("weights");
        AnalysisCanonicalJson.WriteDecimal(writer, "macd", direction.Weights.Macd);
        AnalysisCanonicalJson.WriteDecimal(writer, "ema", direction.Weights.Ema);
        AnalysisCanonicalJson.WriteDecimal(writer, "volume", direction.Weights.Volume);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

public sealed record CandidateSelectionDecision
{
    private CandidateSelectionDecision(
        TechnicalAnalysisOutcome outcome,
        string reasonSummary,
        IReadOnlyList<string> reasons,
        int? score,
        ConfidenceLevel? confidence,
        string? primaryReason,
        IReadOnlyList<CandidateScoreComponent> components)
    {
        Outcome = outcome;
        ReasonSummary = reasonSummary;
        Reasons = Array.AsReadOnly(reasons.ToArray());
        Score = score;
        Confidence = confidence;
        PrimaryReason = primaryReason;
        Components = Array.AsReadOnly(components.OrderBy(item => item.Ordinal).ToArray());
    }

    public TechnicalAnalysisOutcome Outcome { get; }
    public string ReasonSummary { get; }
    public IReadOnlyList<string> Reasons { get; }
    public int? Score { get; }
    public ConfidenceLevel? Confidence { get; }
    public string? PrimaryReason { get; }
    public IReadOnlyList<CandidateScoreComponent> Components { get; }
    public bool IsCandidate => Outcome == TechnicalAnalysisOutcome.Candidate;

    internal static CandidateSelectionDecision NotScored(
        TechnicalAnalysisOutcome outcome,
        string reasonSummary,
        IReadOnlyList<string> reasons) =>
        new(outcome, reasonSummary, reasons, null, null, null, []);

    internal static CandidateSelectionDecision Candidate(
        string reasonSummary,
        IReadOnlyList<string> reasons,
        int score,
        ConfidenceLevel confidence,
        string primaryReason,
        IReadOnlyList<CandidateScoreComponent> components) =>
        new(
            TechnicalAnalysisOutcome.Candidate,
            reasonSummary,
            reasons,
            score,
            confidence,
            primaryReason,
            components);
}

public interface ICandidateScoringEngine
{
    string Version { get; }

    CandidateSelectionDecision Evaluate(
        PositionSide side,
        TechnicalIndicatorCalculationResult indicators,
        TechnicalIndicatorParameters indicatorParameters,
        CandidateScoringParameters parameters);
}

public sealed class CandidateScoringEngine : ICandidateScoringEngine
{
    public const string EngineVersion = "candidate-scoring-engine-v1";
    private const string ComponentSchemaVersion = "candidate-score-component-v1";

    public string Version => EngineVersion;

    public CandidateSelectionDecision Evaluate(
        PositionSide side,
        TechnicalIndicatorCalculationResult indicators,
        TechnicalIndicatorParameters indicatorParameters,
        CandidateScoringParameters parameters)
    {
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side));
        }

        ArgumentNullException.ThrowIfNull(indicators);
        ArgumentNullException.ThrowIfNull(indicatorParameters);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!indicators.IsSuccess)
        {
            var outcome = MapFailure(indicators.Status);
            return CandidateSelectionDecision.NotScored(
                outcome,
                indicators.Reason,
                [$"INDICATOR_{indicators.Status.ToString().ToUpperInvariant()}: {indicators.Reason}"]);
        }

        var snapshot = indicators.Snapshot;
        if (snapshot is null)
        {
            return Invalid("A successful indicator result did not contain a snapshot.");
        }

        if (snapshot.Atr < 0m)
        {
            return Invalid("ATR cannot be negative.");
        }

        if (!TryGetRequiredInputs(
                indicators,
                snapshot,
                indicatorParameters,
                out var hashes,
                out var shortEma,
                out var mediumEma,
                out var longEma,
                out var invalidReason))
        {
            return Invalid(invalidReason!);
        }

        var direction = parameters.For(side);
        var volume = snapshot.Volume;
        if (!Enum.IsDefined(volume.RatioStatus))
        {
            return Invalid("The volume ratio status is undefined.");
        }

        if (direction.RequireVolume &&
            (volume.RatioStatus != VolumeRatioStatus.Available || volume.Ratio is null))
        {
            return Invalid("The required volume ratio is unavailable and cannot be inferred.");
        }

        if (volume.Ratio is < 0m)
        {
            return Invalid("The volume ratio cannot be negative.");
        }

        var macdGap = side == PositionSide.Long
            ? snapshot.Macd.Line.Current - snapshot.Macd.Signal.Current
            : snapshot.Macd.Signal.Current - snapshot.Macd.Line.Current;
        var previousMacdGap = side == PositionSide.Long
            ? snapshot.Macd.Line.Previous - snapshot.Macd.Signal.Previous
            : snapshot.Macd.Signal.Previous - snapshot.Macd.Line.Previous;
        var directionalHistogram = side == PositionSide.Long
            ? snapshot.Macd.Histogram.Current
            : -snapshot.Macd.Histogram.Current;
        var macdMatched = macdGap > 0m && directionalHistogram > 0m;

        var firstEmaGap = side == PositionSide.Long
            ? shortEma.Current - mediumEma.Current
            : mediumEma.Current - shortEma.Current;
        var secondEmaGap = side == PositionSide.Long
            ? mediumEma.Current - longEma.Current
            : longEma.Current - mediumEma.Current;
        var emaGap = Math.Min(firstEmaGap, secondEmaGap);
        var emaMatched = emaGap > 0m;
        var previousEmaMatched = side == PositionSide.Long
            ? shortEma.Previous > mediumEma.Previous && mediumEma.Previous > longEma.Previous
            : shortEma.Previous < mediumEma.Previous && mediumEma.Previous < longEma.Previous;

        var volumeRatio = volume.Ratio;
        var volumeMatched = !direction.RequireVolume ||
                            volumeRatio is not null && volumeRatio.Value >= direction.MinimumVolumeRatio;
        var reasons = new[]
        {
            macdMatched
                ? previousMacdGap <= 0m
                    ? "MACD_MATCHED_FRESH: The directional MACD state crossed into alignment on the evaluation bar."
                    : "MACD_MATCHED_CONTINUATION: The directional MACD state remains aligned."
                : "MACD_NOT_MATCHED: The current MACD line and signal are not aligned for this side.",
            emaMatched
                ? previousEmaMatched
                    ? "EMA_MATCHED_CONTINUATION: The EMA stack remains directionally aligned."
                    : "EMA_MATCHED_FRESH: The EMA stack became directionally aligned on the evaluation bar."
                : "EMA_NOT_MATCHED: EMA short, medium, and long periods are not strictly ordered for this side.",
            !direction.RequireVolume
                ? "VOLUME_NOT_REQUIRED: Volume is not a gate for this configured side."
                : volumeMatched
                    ? "VOLUME_MATCHED: The volume ratio meets the configured minimum."
                    : "VOLUME_NOT_MATCHED: The volume ratio is below the configured minimum.",
        };

        if (!macdMatched || !emaMatched || !volumeMatched)
        {
            return CandidateSelectionDecision.NotScored(
                TechnicalAnalysisOutcome.NotCandidate,
                $"The {side} Entry conditions did not all match.",
                reasons);
        }

        var macdStrength = NormalizeGap(macdGap, snapshot.Atr, parameters.AtrNormalizationScale);
        var emaStrength = NormalizeGap(emaGap, snapshot.Atr, parameters.AtrNormalizationScale);
        var volumeStrength = direction.Weights.Volume == 0m
            ? 0m
            : NormalizeVolume(
                volumeRatio!.Value,
                direction.MinimumVolumeRatio,
                parameters.VolumeFullStrengthRatio);
        var macdFactor = MatchedFactor(macdStrength, parameters.MatchedBaseFraction);
        var emaFactor = MatchedFactor(emaStrength, parameters.MatchedBaseFraction);
        var volumeFactor = direction.Weights.Volume == 0m
            ? 0m
            : MatchedFactor(volumeStrength, parameters.MatchedBaseFraction);
        var components = new[]
        {
            new CandidateScoreComponent(
                "MACD_DIRECTION",
                true,
                MacdRawJson(
                    side,
                    snapshot,
                    macdGap,
                    snapshot.Atr,
                    macdStrength,
                    macdFactor,
                    hashes["MACD"],
                    hashes[$"ATR{indicatorParameters.AtrPeriod}"]),
                direction.Weights.Macd,
                direction.Weights.Macd * macdFactor,
                reasons[0],
                1),
            new CandidateScoreComponent(
                "EMA_ALIGNMENT",
                true,
                EmaRawJson(
                    side,
                    indicatorParameters,
                    shortEma,
                    mediumEma,
                    longEma,
                    emaGap,
                    snapshot.Atr,
                    emaStrength,
                    emaFactor,
                    hashes),
                direction.Weights.Ema,
                direction.Weights.Ema * emaFactor,
                reasons[1],
                2),
            new CandidateScoreComponent(
                "VOLUME_FILTER",
                true,
                VolumeRawJson(
                    side,
                    volume,
                    direction,
                    parameters.VolumeFullStrengthRatio,
                    volumeStrength,
                    volumeFactor,
                    hashes[$"VolumeAverage{indicatorParameters.VolumeAveragePeriod}"],
                    hashes["VolumeRatio"]),
                direction.Weights.Volume,
                direction.Weights.Volume * volumeFactor,
                reasons[2],
                3),
        };
        var total = components.Sum(item => item.AwardedScore);
        var score = decimal.ToInt32(decimal.Clamp(
            decimal.Round(total, 0, MidpointRounding.AwayFromZero),
            0m,
            100m));
        var confidence = parameters.Classify(score);
        var primaryReason = $"{side} Entry conditions matched; score {score}/100 is a relative ranking aid, not a probability.";

        return CandidateSelectionDecision.Candidate(
            $"The {side} Entry conditions matched.",
            reasons,
            score,
            confidence,
            primaryReason,
            components);
    }

    private static CandidateSelectionDecision Invalid(string reason) =>
        CandidateSelectionDecision.NotScored(
            TechnicalAnalysisOutcome.InvalidData,
            reason,
            [$"CANDIDATE_INPUT_INVALID: {reason}"]);

    private static TechnicalAnalysisOutcome MapFailure(TechnicalIndicatorCalculationStatus status) => status switch
    {
        TechnicalIndicatorCalculationStatus.InsufficientHistory => TechnicalAnalysisOutcome.InsufficientHistory,
        TechnicalIndicatorCalculationStatus.HistoryIncomplete => TechnicalAnalysisOutcome.HistoryIncomplete,
        TechnicalIndicatorCalculationStatus.InvalidData => TechnicalAnalysisOutcome.InvalidData,
        TechnicalIndicatorCalculationStatus.PointInTimeUnverified => TechnicalAnalysisOutcome.PointInTimeUnverified,
        TechnicalIndicatorCalculationStatus.ReconciliationRequired => TechnicalAnalysisOutcome.ReconciliationRequired,
        _ => TechnicalAnalysisOutcome.Failed,
    };

    private static bool TryGetRequiredInputs(
        TechnicalIndicatorCalculationResult indicators,
        TechnicalIndicatorSnapshot snapshot,
        TechnicalIndicatorParameters parameters,
        out IReadOnlyDictionary<string, Sha256Hash> hashes,
        out CurrentAndPreviousValue shortEma,
        out CurrentAndPreviousValue mediumEma,
        out CurrentAndPreviousValue longEma,
        out string? reason)
    {
        var duplicateKeys = indicators.IndicatorResults
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (duplicateKeys.Length > 0)
        {
            hashes = new Dictionary<string, Sha256Hash>();
            shortEma = mediumEma = longEma = new CurrentAndPreviousValue(0m, 0m);
            reason = $"The indicator result contains duplicate evidence keys: {string.Join(", ", duplicateKeys)}.";
            return false;
        }

        var byKey = indicators.IndicatorResults.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var requiredKeys = new[]
        {
            "MACD",
            $"EMA{parameters.ShortEmaPeriod}",
            $"EMA{parameters.MediumEmaPeriod}",
            $"EMA{parameters.LongEmaPeriod}",
            $"VolumeAverage{parameters.VolumeAveragePeriod}",
            "VolumeRatio",
            $"ATR{parameters.AtrPeriod}",
        };
        var missing = requiredKeys.Where(key => !byKey.ContainsKey(key)).ToArray();
        if (missing.Length > 0)
        {
            hashes = new Dictionary<string, Sha256Hash>();
            shortEma = mediumEma = longEma = new CurrentAndPreviousValue(0m, 0m);
            reason = $"The indicator result is missing required evidence: {string.Join(", ", missing)}.";
            return false;
        }

        if (!snapshot.Emas.TryGetValue(parameters.ShortEmaPeriod, out shortEma!) ||
            !snapshot.Emas.TryGetValue(parameters.MediumEmaPeriod, out mediumEma!) ||
            !snapshot.Emas.TryGetValue(parameters.LongEmaPeriod, out longEma!))
        {
            hashes = new Dictionary<string, Sha256Hash>();
            shortEma = mediumEma = longEma = new CurrentAndPreviousValue(0m, 0m);
            reason = "The indicator snapshot is missing a required EMA period.";
            return false;
        }

        hashes = byKey.ToDictionary(item => item.Key, item => item.Value.InputHash, StringComparer.Ordinal);
        reason = null;
        return true;
    }

    private static decimal NormalizeGap(decimal gap, decimal atr, decimal atrScale)
    {
        if (gap <= 0m)
        {
            return 0m;
        }

        var scaledAtr = atr * atrScale;
        return scaledAtr <= 0m ? 1m : gap / (gap + scaledAtr);
    }

    private static decimal NormalizeVolume(decimal ratio, decimal minimum, decimal fullStrength) =>
        decimal.Clamp((ratio - minimum) / (fullStrength - minimum), 0m, 1m);

    private static decimal MatchedFactor(decimal strength, decimal baseFraction) =>
        baseFraction + ((1m - baseFraction) * decimal.Clamp(strength, 0m, 1m));

    private static string MacdRawJson(
        PositionSide side,
        TechnicalIndicatorSnapshot snapshot,
        decimal gap,
        decimal atr,
        decimal strength,
        decimal factor,
        Sha256Hash macdHash,
        Sha256Hash atrHash) =>
        JsonObject(writer =>
        {
            writer.WriteString("side", side.ToString());
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
            WriteDecimal(writer, "directionalGap", gap);
            WriteDecimal(writer, "atr", atr);
            WriteDecimal(writer, "normalizedStrength", strength);
            WriteDecimal(writer, "matchedFactor", factor);
            WriteHashes(writer, macdHash, atrHash);
        });

    private static string EmaRawJson(
        PositionSide side,
        TechnicalIndicatorParameters periods,
        CurrentAndPreviousValue shortEma,
        CurrentAndPreviousValue mediumEma,
        CurrentAndPreviousValue longEma,
        decimal gap,
        decimal atr,
        decimal strength,
        decimal factor,
        IReadOnlyDictionary<string, Sha256Hash> hashes) =>
        JsonObject(writer =>
        {
            writer.WriteString("side", side.ToString());
            writer.WriteStartObject("periods");
            writer.WriteNumber("short", periods.ShortEmaPeriod);
            writer.WriteNumber("medium", periods.MediumEmaPeriod);
            writer.WriteNumber("long", periods.LongEmaPeriod);
            writer.WriteEndObject();
            writer.WriteStartObject("previous");
            WriteDecimal(writer, "short", shortEma.Previous);
            WriteDecimal(writer, "medium", mediumEma.Previous);
            WriteDecimal(writer, "long", longEma.Previous);
            writer.WriteEndObject();
            writer.WriteStartObject("current");
            WriteDecimal(writer, "short", shortEma.Current);
            WriteDecimal(writer, "medium", mediumEma.Current);
            WriteDecimal(writer, "long", longEma.Current);
            writer.WriteEndObject();
            WriteDecimal(writer, "directionalMinimumGap", gap);
            WriteDecimal(writer, "atr", atr);
            WriteDecimal(writer, "normalizedStrength", strength);
            WriteDecimal(writer, "matchedFactor", factor);
            WriteHashes(
                writer,
                hashes[$"EMA{periods.ShortEmaPeriod}"],
                hashes[$"EMA{periods.MediumEmaPeriod}"],
                hashes[$"EMA{periods.LongEmaPeriod}"],
                hashes[$"ATR{periods.AtrPeriod}"]);
        });

    private static string VolumeRawJson(
        PositionSide side,
        VolumeSnapshot volume,
        CandidateDirectionParameters direction,
        decimal fullStrengthRatio,
        decimal strength,
        decimal factor,
        Sha256Hash averageHash,
        Sha256Hash ratioHash) =>
        JsonObject(writer =>
        {
            writer.WriteString("side", side.ToString());
            writer.WriteBoolean("required", direction.RequireVolume);
            writer.WriteString("status", volume.RatioStatus.ToString());
            WriteDecimal(writer, "currentVolume", volume.CurrentVolume);
            WriteDecimal(writer, "referenceAverage", volume.ReferenceAverageVolume);
            if (volume.Ratio is { } ratio)
            {
                WriteDecimal(writer, "ratio", ratio);
            }
            else
            {
                writer.WriteNull("ratio");
            }

            WriteDecimal(writer, "minimumRatio", direction.MinimumVolumeRatio);
            WriteDecimal(writer, "fullStrengthRatio", fullStrengthRatio);
            WriteDecimal(writer, "normalizedStrength", strength);
            WriteDecimal(writer, "matchedFactor", factor);
            WriteHashes(writer, averageHash, ratioHash);
        });

    private static string JsonObject(Action<Utf8JsonWriter> writeValue)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", ComponentSchemaVersion);
            writer.WriteStartObject("value");
            writeValue(writer);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteHashes(Utf8JsonWriter writer, params Sha256Hash[] hashes)
    {
        writer.WriteStartArray("inputSha256");
        foreach (var hash in hashes)
        {
            writer.WriteStringValue(hash.Value);
        }

        writer.WriteEndArray();
    }

    private static void WriteDecimal(Utf8JsonWriter writer, string propertyName, decimal value) =>
        AnalysisCanonicalJson.WriteDecimal(writer, propertyName, value);
}
