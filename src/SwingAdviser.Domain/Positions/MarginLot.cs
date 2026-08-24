using SwingAdviser.Domain.Common;

namespace SwingAdviser.Domain.Positions;

public sealed record MarginLot
{
    private MarginLot(
        MarginLotId id,
        PositionId positionId,
        TradeExecutionId openingExecutionId,
        TradeExecutionRevisionId initialOpeningRevisionId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        PositionId = positionId;
        OpeningExecutionId = openingExecutionId;
        InitialOpeningRevisionId = initialOpeningRevisionId;
        CreatedAtUtc = createdAtUtc;
    }

    public MarginLotId Id { get; }
    public PositionId PositionId { get; }
    public TradeExecutionId OpeningExecutionId { get; }
    public TradeExecutionRevisionId InitialOpeningRevisionId { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public static MarginLot FromUserConfirmedOpening(
        MarginLotId id,
        TradeExecution openingExecution,
        DateTimeOffset createdAtUtc)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Margin lot ID cannot be empty.", nameof(id));
        }

        if (openingExecution.Kind != ExecutionKind.Open ||
            openingExecution.Origin != ExecutionOrigin.UserConfirmed ||
            openingExecution.CurrentRevision.Disposition != RecordDisposition.Effective)
        {
            throw new DomainException("A margin lot requires an effective, user-confirmed opening execution.");
        }

        return new MarginLot(
            id,
            openingExecution.PositionId,
            openingExecution.Id,
            openingExecution.CurrentRevision.Id,
            DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc)));
    }
}

public sealed record MarginLotContractRevision
{
    public MarginLotContractRevision(
        MarginLotId marginLotId,
        TradeExecutionRevisionId openingExecutionRevisionId,
        RevisionMetadata audit,
        MarginType marginType,
        string broker,
        string productName,
        DateRange effectivePeriod,
        MarginTermType termType,
        DateTimeOffset? finalRepaymentAtUtc,
        decimal? buyerInterestRate,
        decimal? stockLendingRate,
        string? rateUnit,
        CurrencyCode contractCurrency,
        string? dayCountConvention,
        DateTimeOffset confirmedAtUtc,
        string evidence,
        ContractChangeKind changeKind)
    {
        if (termType == MarginTermType.FixedDate && finalRepaymentAtUtc is null)
        {
            throw new ArgumentException("A fixed-date contract requires a broker-confirmed final repayment instant.", nameof(finalRepaymentAtUtc));
        }

        if (termType != MarginTermType.FixedDate && finalRepaymentAtUtc is not null)
        {
            throw new ArgumentException("Only a fixed-date contract can carry a final repayment instant.", nameof(finalRepaymentAtUtc));
        }

        if (finalRepaymentAtUtc is not null)
        {
            DomainGuard.Utc(finalRepaymentAtUtc.Value, nameof(finalRepaymentAtUtc));
        }

        MarginLotId = marginLotId;
        OpeningExecutionRevisionId = openingExecutionRevisionId;
        Audit = audit;
        MarginType = marginType;
        Broker = DomainGuard.Required(broker, nameof(broker));
        ProductName = DomainGuard.Required(productName, nameof(productName));
        EffectivePeriod = effectivePeriod;
        TermType = termType;
        FinalRepaymentAtUtc = finalRepaymentAtUtc;
        BuyerInterestRate = buyerInterestRate;
        StockLendingRate = stockLendingRate;
        RateUnit = rateUnit?.Trim();
        ContractCurrency = contractCurrency;
        DayCountConvention = dayCountConvention?.Trim();
        ConfirmedAtUtc = DomainGuard.Utc(confirmedAtUtc, nameof(confirmedAtUtc));
        Evidence = DomainGuard.Required(evidence, nameof(evidence));
        ChangeKind = changeKind;
    }

    public MarginLotId MarginLotId { get; }
    public TradeExecutionRevisionId OpeningExecutionRevisionId { get; }
    public RevisionMetadata Audit { get; }
    public MarginType MarginType { get; }
    public string Broker { get; }
    public string ProductName { get; }
    public DateRange EffectivePeriod { get; }
    public MarginTermType TermType { get; }
    public DateTimeOffset? FinalRepaymentAtUtc { get; }
    public decimal? BuyerInterestRate { get; }
    public decimal? StockLendingRate { get; }
    public string? RateUnit { get; }
    public CurrencyCode ContractCurrency { get; }
    public string? DayCountConvention { get; }
    public DateTimeOffset ConfirmedAtUtc { get; }
    public string Evidence { get; }
    public ContractChangeKind ChangeKind { get; }
}
