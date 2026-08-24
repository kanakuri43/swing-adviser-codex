using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Analysis;

public sealed record IndicatorResult
{
    public IndicatorResult(
        string key,
        string algorithmId,
        string normalizedParametersJson,
        string normalizedDecisionValuesJson,
        DateOnly calculationStartBarDate,
        Sha256Hash inputHash,
        int ordinal)
    {
        Key = DomainGuard.Required(key, nameof(key));
        AlgorithmId = DomainGuard.Required(algorithmId, nameof(algorithmId));
        NormalizedParametersJson = DomainGuard.Required(normalizedParametersJson, nameof(normalizedParametersJson));
        NormalizedDecisionValuesJson = DomainGuard.Required(normalizedDecisionValuesJson, nameof(normalizedDecisionValuesJson));
        CalculationStartBarDate = calculationStartBarDate;
        InputHash = inputHash;
        Ordinal = DomainGuard.Positive(ordinal, nameof(ordinal));
    }

    public string Key { get; }
    public string AlgorithmId { get; }
    public string NormalizedParametersJson { get; }
    public string NormalizedDecisionValuesJson { get; }
    public DateOnly CalculationStartBarDate { get; }
    public Sha256Hash InputHash { get; }
    public int Ordinal { get; }
}

public sealed record TechnicalAnalysisResult
{
    public TechnicalAnalysisResult(
        Guid id,
        AnalysisRunId analysisRunId,
        Guid analysisInputManifestId,
        InstrumentId instrumentId,
        PositionSide positionSide,
        TechnicalAnalysisOutcome outcome,
        string reasonSummary,
        IReadOnlyList<string> reasons,
        DateOnly? calculationStartBarDate,
        IReadOnlyList<IndicatorResult> indicators)
    {
        if (id == Guid.Empty || analysisInputManifestId == Guid.Empty)
        {
            throw new ArgumentException("Analysis result and manifest IDs cannot be empty.");
        }

        if (indicators.GroupBy(x => x.Key, StringComparer.Ordinal).Any(x => x.Count() > 1))
        {
            throw new ArgumentException("Indicator keys must be unique.", nameof(indicators));
        }

        Id = id;
        AnalysisRunId = analysisRunId;
        AnalysisInputManifestId = analysisInputManifestId;
        InstrumentId = instrumentId;
        PositionSide = positionSide;
        SignalPurpose = SignalPurpose.Entry;
        Outcome = outcome;
        ReasonSummary = DomainGuard.Required(reasonSummary, nameof(reasonSummary));
        Reasons = reasons.ToArray();
        CalculationStartBarDate = calculationStartBarDate;
        Indicators = indicators.OrderBy(x => x.Ordinal).ToArray();
    }

    public Guid Id { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public Guid AnalysisInputManifestId { get; }
    public InstrumentId InstrumentId { get; }
    public PositionSide PositionSide { get; }
    public SignalPurpose SignalPurpose { get; }
    public TechnicalAnalysisOutcome Outcome { get; }
    public string ReasonSummary { get; }
    public IReadOnlyList<string> Reasons { get; }
    public DateOnly? CalculationStartBarDate { get; }
    public IReadOnlyList<IndicatorResult> Indicators { get; }
}
