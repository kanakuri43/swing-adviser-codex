using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.MarginCosts;

namespace SwingAdviser.Domain.Positions;

public sealed record LotCostItemSelection(
    MarginCostItemId MarginCostItemId,
    MarginCostObservationId? ConfirmedObservationId,
    AmountStatus? ConfirmedStatus,
    MarginCostObservationId? EstimateObservationId,
    AmountStatus? EstimateStatus,
    MarginCostObservationId? ReferenceObservationId);

public sealed record LotCostAggregate
{
    internal LotCostAggregate(
        AmountStatus status,
        decimal? netCost,
        CurrencyCode currency,
        IReadOnlyList<MarginCostObservationId> countedObservationIds,
        IReadOnlyList<AmountStatus> blockingStatuses)
    {
        Status = status;
        NetCost = netCost;
        Currency = currency;
        CountedObservationIds = countedObservationIds.ToArray();
        BlockingStatuses = blockingStatuses.ToArray();
    }

    public AmountStatus Status { get; }
    public decimal? NetCost { get; }
    public CurrencyCode Currency { get; }
    public IReadOnlyList<MarginCostObservationId> CountedObservationIds { get; }
    public IReadOnlyList<AmountStatus> BlockingStatuses { get; }
    public bool IsComplete => NetCost is not null;
}

public sealed record LotMonetaryMetric
{
    internal LotMonetaryMetric(
        AmountStatus status,
        decimal? amount,
        CurrencyCode currency,
        IReadOnlyList<AmountStatus> blockingStatuses)
    {
        Status = status;
        Amount = amount;
        Currency = currency;
        BlockingStatuses = blockingStatuses.ToArray();
    }

    public AmountStatus Status { get; }
    public decimal? Amount { get; }
    public CurrencyCode Currency { get; }
    public IReadOnlyList<AmountStatus> BlockingStatuses { get; }
    public bool IsKnown => Amount is not null;
}

public sealed record LotRatioMetric
{
    internal LotRatioMetric(
        AmountStatus status,
        decimal? value,
        IReadOnlyList<AmountStatus> blockingStatuses)
    {
        Status = status;
        Value = value;
        BlockingStatuses = blockingStatuses.ToArray();
    }

    public AmountStatus Status { get; }
    public decimal? Value { get; }
    public IReadOnlyList<AmountStatus> BlockingStatuses { get; }
    public bool IsKnown => Value is not null;
}

public sealed record LotProfitAndLossResult
{
    internal LotProfitAndLossResult(
        MarginLotId marginLotId,
        decimal currentQuantity,
        LotMonetaryMetric priceProfitAndLoss,
        LotCostAggregate confirmedCosts,
        LotMonetaryMetric confirmedCostAdjustedProfitAndLoss,
        LotCostAggregate referenceCosts,
        LotMonetaryMetric estimatedNetProfitAndLoss,
        LotRatioMetric costToRRatio,
        IReadOnlyList<LotCostItemSelection> costSelections)
    {
        MarginLotId = marginLotId;
        CurrentQuantity = currentQuantity;
        PriceProfitAndLoss = priceProfitAndLoss;
        ConfirmedCosts = confirmedCosts;
        ConfirmedCostAdjustedProfitAndLoss = confirmedCostAdjustedProfitAndLoss;
        ReferenceCosts = referenceCosts;
        EstimatedNetProfitAndLoss = estimatedNetProfitAndLoss;
        CostToRRatio = costToRRatio;
        CostSelections = costSelections.ToArray();
    }

    public MarginLotId MarginLotId { get; }
    public decimal CurrentQuantity { get; }
    public LotMonetaryMetric PriceProfitAndLoss { get; }
    public LotCostAggregate ConfirmedCosts { get; }
    public LotMonetaryMetric ConfirmedCostAdjustedProfitAndLoss { get; }
    public LotCostAggregate ReferenceCosts { get; }
    public LotMonetaryMetric EstimatedNetProfitAndLoss { get; }
    public LotRatioMetric CostToRRatio { get; }
    public IReadOnlyList<LotCostItemSelection> CostSelections { get; }
}

public static class LotProfitAndLossCalculator
{
    public const string AlgorithmVersion = "lot-profit-and-loss-v1";

    public static LotProfitAndLossResult Calculate(
        RiskBasisSnapshot riskBasis,
        UnitizedRiskPrice currentPrice,
        decimal currentQuantity,
        IReadOnlyCollection<MarginCostItem> costItems)
    {
        ArgumentNullException.ThrowIfNull(riskBasis);
        ArgumentNullException.ThrowIfNull(costItems);
        DomainGuard.Positive(currentQuantity, nameof(currentQuantity));

        if (currentPrice.Unit != riskBasis.PriceUnit)
        {
            throw new DomainException("The current price and risk basis must use the same currency and share unit.");
        }

        if (!Enum.IsDefined(riskBasis.Side))
        {
            throw new DomainException("The risk basis has an unsupported position side.");
        }

        var orderedItems = costItems
            .OrderBy(item => item.Id.Value)
            .ToArray();
        ValidateItems(orderedItems, riskBasis.MarginLotId);

        var currency = riskBasis.PriceUnit.Currency;
        decimal priceProfitAndLoss;
        decimal totalRisk;
        try
        {
            var perSharePriceProfitAndLoss = riskBasis.Side == PositionSide.Long
                ? currentPrice.Amount.Value - riskBasis.EntryBasisPrice.Value
                : riskBasis.EntryBasisPrice.Value - currentPrice.Amount.Value;
            priceProfitAndLoss = perSharePriceProfitAndLoss * currentQuantity;
            totalRisk = riskBasis.RiskAmountR * currentQuantity;
        }
        catch (OverflowException exception)
        {
            throw new DomainException("The lot price profit/loss exceeds the supported decimal range.", exception);
        }

        if (totalRisk <= 0m)
        {
            throw new DomainException("The lot's total 1R cannot be represented as a positive decimal amount.");
        }

        var selections = new List<LotCostItemSelection>(orderedItems.Length);
        var confirmedAccumulator = new CostAccumulator(currency);
        var referenceAccumulator = new CostAccumulator(currency);

        foreach (var item in orderedItems)
        {
            var resolution = item.ResolveForReferenceTotal();
            var confirmed = resolution.ConfirmedState;
            var estimate = resolution.EstimateState;
            var reference = resolution.SelectedForReference;

            confirmedAccumulator.Add(confirmed, confirmed?.Amount.Status ?? AmountStatus.Unknown);

            var referenceBlockingStatus = reference?.Amount.Status
                ?? confirmed?.Amount.Status
                ?? AmountStatus.Unknown;
            referenceAccumulator.Add(reference, referenceBlockingStatus);

            selections.Add(new LotCostItemSelection(
                item.Id,
                confirmed?.Id,
                confirmed?.Amount.Status,
                estimate?.Id,
                estimate?.Amount.Status,
                reference?.Id));
        }

        var confirmedCosts = confirmedAccumulator.Build(orderedItems.Length > 0);
        var referenceCosts = referenceAccumulator.Build(orderedItems.Length > 0);
        var priceMetric = KnownMoney(priceProfitAndLoss, currency);
        var confirmedProfitAndLoss = AdjustProfitAndLoss(
            priceProfitAndLoss,
            confirmedCosts,
            currency);
        var estimatedNetProfitAndLoss = AdjustProfitAndLoss(
            priceProfitAndLoss,
            referenceCosts,
            currency);
        var costToRRatio = referenceCosts.NetCost is { } referenceNetCost
            ? KnownRatio(Divide(referenceNetCost, totalRisk))
            : MissingRatio(referenceCosts.BlockingStatuses);

        return new LotProfitAndLossResult(
            riskBasis.MarginLotId,
            currentQuantity,
            priceMetric,
            confirmedCosts,
            confirmedProfitAndLoss,
            referenceCosts,
            estimatedNetProfitAndLoss,
            costToRRatio,
            selections);
    }

    private static void ValidateItems(
        IReadOnlyList<MarginCostItem> items,
        MarginLotId marginLotId)
    {
        if (items.Any(item => item.MarginLotId != marginLotId))
        {
            throw new DomainException("Every cost item must belong to the evaluated margin lot.");
        }

        if (items.Select(item => item.Id).Distinct().Count() != items.Count)
        {
            throw new DomainException("A margin cost item cannot be aggregated more than once.");
        }

        if (items
            .GroupBy(item => (item.CostType, item.OccurrenceKey), new CostOccurrenceComparer())
            .Any(group => group.Count() > 1))
        {
            throw new DomainException("A logical margin cost occurrence cannot be aggregated more than once.");
        }
    }

    private static LotMonetaryMetric AdjustProfitAndLoss(
        decimal priceProfitAndLoss,
        LotCostAggregate costs,
        CurrencyCode currency)
    {
        if (costs.NetCost is not { } netCost)
        {
            return MissingMoney(currency, costs.BlockingStatuses);
        }

        try
        {
            return KnownMoney(priceProfitAndLoss - netCost, currency);
        }
        catch (OverflowException exception)
        {
            throw new DomainException("The cost-adjusted lot profit/loss exceeds the supported decimal range.", exception);
        }
    }

    private static decimal Divide(decimal amount, decimal totalRisk)
    {
        try
        {
            return amount / totalRisk;
        }
        catch (OverflowException exception)
        {
            throw new DomainException("The lot cost/R ratio exceeds the supported decimal range.", exception);
        }
    }

    private static LotMonetaryMetric KnownMoney(decimal amount, CurrencyCode currency) =>
        new(
            amount == 0m ? AmountStatus.KnownZero : AmountStatus.KnownAmount,
            amount,
            currency,
            []);

    private static LotMonetaryMetric MissingMoney(
        CurrencyCode currency,
        IReadOnlyList<AmountStatus> blockingStatuses) =>
        new(StatusForMissing(blockingStatuses), null, currency, blockingStatuses);

    private static LotRatioMetric KnownRatio(decimal value) =>
        new(value == 0m ? AmountStatus.KnownZero : AmountStatus.KnownAmount, value, []);

    private static LotRatioMetric MissingRatio(IReadOnlyList<AmountStatus> blockingStatuses) =>
        new(StatusForMissing(blockingStatuses), null, blockingStatuses);

    private static AmountStatus StatusForMissing(IReadOnlyList<AmountStatus> blockingStatuses)
    {
        var distinct = blockingStatuses.Distinct().ToArray();
        return distinct.Length == 1 ? distinct[0] : AmountStatus.Unknown;
    }

    private sealed class CostAccumulator(CurrencyCode currency)
    {
        private readonly List<MarginCostObservationId> _countedObservationIds = [];
        private readonly List<AmountStatus> _blockingStatuses = [];
        private readonly List<AmountStatus> _resolvedStatuses = [];
        private decimal _netCost;

        public void Add(MarginCostObservation? observation, AmountStatus missingStatus)
        {
            if (observation is null)
            {
                _blockingStatuses.Add(missingStatus);
                return;
            }

            if (!Enum.IsDefined(observation.Direction) || !Enum.IsDefined(observation.Amount.Status))
            {
                throw new DomainException("A margin cost observation has an unsupported state.");
            }

            ValidateAmountState(observation.Amount);

            if (!observation.Amount.IsResolved)
            {
                _blockingStatuses.Add(observation.Amount.Status);
                return;
            }

            var magnitude = observation.Amount.Amount ?? 0m;
            if (observation.Amount.Currency is { } amountCurrency && amountCurrency != currency)
            {
                throw new DomainException("A margin cost amount must use the risk basis currency.");
            }

            try
            {
                _netCost += observation.Direction == CostDirection.Charge
                    ? magnitude
                    : -magnitude;
            }
            catch (OverflowException exception)
            {
                throw new DomainException("The lot cost aggregate exceeds the supported decimal range.", exception);
            }

            _countedObservationIds.Add(observation.Id);
            _resolvedStatuses.Add(observation.Amount.Status);
        }

        private static void ValidateAmountState(CostAmount amount)
        {
            var hasCurrency = amount.Currency is { } amountCurrency &&
                !string.IsNullOrWhiteSpace(amountCurrency.Value);
            var isValid = amount.Status switch
            {
                AmountStatus.KnownAmount => amount.Amount is > 0m && hasCurrency,
                AmountStatus.KnownZero => amount.Amount == 0m && hasCurrency,
                AmountStatus.NotOccurred or
                    AmountStatus.Unpublished or
                    AmountStatus.FetchFailed or
                    AmountStatus.Unknown or
                    AmountStatus.NotApplicable => amount.Amount is null && amount.Currency is null,
                _ => false,
            };

            if (!isValid)
            {
                throw new DomainException("A margin cost observation has an invalid amount state.");
            }
        }

        public LotCostAggregate Build(bool hasItems)
        {
            if (!hasItems)
            {
                return new LotCostAggregate(
                    AmountStatus.Unknown,
                    null,
                    currency,
                    [],
                    [AmountStatus.Unknown]);
            }

            var blocking = _blockingStatuses.Distinct().Order().ToArray();
            if (blocking.Length > 0)
            {
                return new LotCostAggregate(
                    StatusForMissing(blocking),
                    null,
                    currency,
                    _countedObservationIds,
                    blocking);
            }

            var status = _netCost != 0m
                ? AmountStatus.KnownAmount
                : _resolvedStatuses.All(value => value == AmountStatus.NotOccurred)
                    ? AmountStatus.NotOccurred
                    : _resolvedStatuses.All(value => value == AmountStatus.NotApplicable)
                        ? AmountStatus.NotApplicable
                        : AmountStatus.KnownZero;
            return new LotCostAggregate(
                status,
                _netCost,
                currency,
                _countedObservationIds,
                []);
        }
    }

    private sealed class CostOccurrenceComparer : IEqualityComparer<(MarginCostType CostType, string OccurrenceKey)>
    {
        public bool Equals(
            (MarginCostType CostType, string OccurrenceKey) x,
            (MarginCostType CostType, string OccurrenceKey) y) =>
            x.CostType == y.CostType &&
            StringComparer.Ordinal.Equals(x.OccurrenceKey, y.OccurrenceKey);

        public int GetHashCode((MarginCostType CostType, string OccurrenceKey) value) =>
            HashCode.Combine(value.CostType, StringComparer.Ordinal.GetHashCode(value.OccurrenceKey));
    }
}
