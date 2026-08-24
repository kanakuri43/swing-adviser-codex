using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.MarginCosts;

public sealed record MarginCostObservation
{
    public MarginCostObservation(
        MarginCostObservationId id,
        MarginCostItemId marginCostItemId,
        CostValuationKind valuationKind,
        CostDirection direction,
        CostAmount amount,
        decimal? quantity,
        decimal? rate,
        string? rateUnit,
        int? includedDays,
        string? dayCountConvention,
        CostSourceKind sourceKind,
        MarginCostObservationId? reconcilesEstimateId,
        RevisionMetadata audit,
        DateTimeOffset observedAtUtc,
        DateTimeOffset? bookedAtUtc)
    {
        if (id.Value == Guid.Empty || marginCostItemId.Value == Guid.Empty)
        {
            throw new ArgumentException("Cost observation and item IDs cannot be empty.");
        }

        if (id.Value != audit.Id)
        {
            throw new ArgumentException("The typed observation ID must match its audit metadata ID.", nameof(audit));
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A supplied quantity must be positive.");
        }

        if (includedDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(includedDays), "Included days cannot be negative.");
        }

        if (reconcilesEstimateId is not null && valuationKind != CostValuationKind.Confirmed)
        {
            throw new ArgumentException("Only a confirmed observation can reconcile an estimate.", nameof(reconcilesEstimateId));
        }

        Id = id;
        MarginCostItemId = marginCostItemId;
        ValuationKind = valuationKind;
        Direction = direction;
        Amount = amount;
        Quantity = quantity;
        Rate = rate;
        RateUnit = rateUnit?.Trim();
        IncludedDays = includedDays;
        DayCountConvention = dayCountConvention?.Trim();
        SourceKind = sourceKind;
        ReconcilesEstimateId = reconcilesEstimateId;
        Audit = audit;
        ObservedAtUtc = DomainGuard.Utc(observedAtUtc, nameof(observedAtUtc));
        BookedAtUtc = bookedAtUtc is null ? null : DomainGuard.Utc(bookedAtUtc.Value, nameof(bookedAtUtc));
    }

    public MarginCostObservationId Id { get; }
    public MarginCostItemId MarginCostItemId { get; }
    public CostValuationKind ValuationKind { get; }
    public CostDirection Direction { get; }
    public CostAmount Amount { get; }
    public decimal? Quantity { get; }
    public decimal? Rate { get; }
    public string? RateUnit { get; }
    public int? IncludedDays { get; }
    public string? DayCountConvention { get; }
    public CostSourceKind SourceKind { get; }
    public MarginCostObservationId? ReconcilesEstimateId { get; }
    public RevisionMetadata Audit { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public DateTimeOffset? BookedAtUtc { get; }
}

public sealed record MarginCostResolution(
    MarginCostObservation? SelectedForReference,
    MarginCostObservation? ConfirmedState,
    MarginCostObservation? EstimateState)
{
    public bool HasKnownReferenceAmount => SelectedForReference?.Amount.Status is AmountStatus.KnownAmount or AmountStatus.KnownZero;
}

public sealed class MarginCostItem
{
    private readonly List<MarginCostObservation> _observations = [];

    public MarginCostItem(
        MarginCostItemId id,
        MarginLotId marginLotId,
        MarginCostType costType,
        string occurrenceKey,
        DateRange period,
        string? brokerStatementLineId,
        DateTimeOffset createdAtUtc)
    {
        if (id.Value == Guid.Empty || marginLotId.Value == Guid.Empty)
        {
            throw new ArgumentException("Cost item and margin lot IDs cannot be empty.");
        }

        Id = id;
        MarginLotId = marginLotId;
        CostType = costType;
        OccurrenceKey = DomainGuard.Required(occurrenceKey, nameof(occurrenceKey));
        Period = period;
        BrokerStatementLineId = brokerStatementLineId?.Trim();
        CreatedAtUtc = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public MarginCostItemId Id { get; }
    public MarginLotId MarginLotId { get; }
    public MarginCostType CostType { get; }
    public string OccurrenceKey { get; }
    public DateRange Period { get; }
    public string? BrokerStatementLineId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public IReadOnlyList<MarginCostObservation> Observations => _observations.AsReadOnly();

    public void AppendObservation(MarginCostObservation observation)
    {
        if (observation.MarginCostItemId != Id)
        {
            throw new DomainException("A cost observation must belong to this cost item.");
        }

        if (_observations.Any(x => x.Id == observation.Id))
        {
            throw new DomainException("The cost observation has already been appended.");
        }

        var current = Current(observation.ValuationKind);
        if (current is null)
        {
            if (observation.Audit.RevisionNumber != 1 || observation.Audit.SupersedesId is not null)
            {
                throw new DomainException("The first observation for a valuation kind must be revision 1.");
            }
        }
        else if (observation.Audit.RevisionNumber != current.Audit.RevisionNumber + 1 ||
                 observation.Audit.SupersedesId != current.Audit.Id)
        {
            throw new DomainException("A cost correction must directly supersede the current leaf of the same valuation kind.");
        }

        if (observation.ReconcilesEstimateId is { } estimateId &&
            Current(CostValuationKind.Estimate)?.Id != estimateId)
        {
            throw new DomainException("The reconciled estimate must be the current unsuperseded estimate on the same cost item.");
        }

        _observations.Add(observation);
    }

    public MarginCostResolution ResolveForReferenceTotal()
    {
        var confirmed = Current(CostValuationKind.Confirmed);
        var estimate = Current(CostValuationKind.Estimate);

        // A resolved confirmed leaf owns the whole logical item. The estimate is audit evidence,
        // not an additional amount. Unresolved confirmed states remain visible but do not erase an estimate.
        var selected = confirmed?.Amount.IsResolved == true ? confirmed : estimate;
        return new MarginCostResolution(selected, confirmed, estimate);
    }

    private MarginCostObservation? Current(CostValuationKind kind) =>
        _observations
            .Where(x => x.ValuationKind == kind)
            .OrderByDescending(x => x.Audit.RevisionNumber)
            .FirstOrDefault();
}
