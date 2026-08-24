using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Analysis;

public sealed record AnalysisInputManifest
{
    public AnalysisInputManifest(
        Guid id,
        AnalysisRunId analysisRunId,
        InstrumentId instrumentId,
        string priceProvider,
        Guid priceRevisionSetId,
        DateOnly? firstBarDate,
        DateOnly? lastBarDate,
        int barCount,
        int requiredBarCount,
        HistoryStatus historyStatus,
        PointInTimeStatus pointInTimeStatus,
        Sha256Hash priceRevisionSetHash,
        Sha256Hash corporateActionSetHash,
        Sha256Hash manifestHash)
    {
        if (id == Guid.Empty || priceRevisionSetId == Guid.Empty)
        {
            throw new ArgumentException("Manifest and price revision set IDs cannot be empty.");
        }

        if (barCount < 0 || requiredBarCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(barCount), "Bar counts must be non-negative and required count must be positive.");
        }

        if (firstBarDate is not null && lastBarDate is not null && lastBarDate < firstBarDate)
        {
            throw new ArgumentException("Last bar date cannot precede first bar date.", nameof(lastBarDate));
        }

        if (historyStatus == HistoryStatus.Complete && barCount < requiredBarCount)
        {
            throw new DomainException("Complete history must contain at least the required number of bars.");
        }

        Id = id;
        AnalysisRunId = analysisRunId;
        InstrumentId = instrumentId;
        PriceProvider = DomainGuard.Required(priceProvider, nameof(priceProvider));
        PriceRevisionSetId = priceRevisionSetId;
        FirstBarDate = firstBarDate;
        LastBarDate = lastBarDate;
        BarCount = barCount;
        RequiredBarCount = requiredBarCount;
        HistoryStatus = historyStatus;
        PointInTimeStatus = pointInTimeStatus;
        PriceRevisionSetHash = priceRevisionSetHash;
        CorporateActionSetHash = corporateActionSetHash;
        ManifestHash = manifestHash;
    }

    public Guid Id { get; }
    public AnalysisRunId AnalysisRunId { get; }
    public InstrumentId InstrumentId { get; }
    public string PriceProvider { get; }
    public Guid PriceRevisionSetId { get; }
    public DateOnly? FirstBarDate { get; }
    public DateOnly? LastBarDate { get; }
    public int BarCount { get; }
    public int RequiredBarCount { get; }
    public HistoryStatus HistoryStatus { get; }
    public PointInTimeStatus PointInTimeStatus { get; }
    public Sha256Hash PriceRevisionSetHash { get; }
    public Sha256Hash CorporateActionSetHash { get; }
    public Sha256Hash ManifestHash { get; }
}
