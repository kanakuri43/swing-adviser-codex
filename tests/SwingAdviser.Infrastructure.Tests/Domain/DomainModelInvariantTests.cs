using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.MarginCosts;
using SwingAdviser.Domain.Positions;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class DomainModelInvariantTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Position_RegistersOnlyExplicitlyUserConfirmedExecutionValues()
    {
        var position = new Position(
            new PositionId(Guid.NewGuid()),
            new InstrumentId(Guid.NewGuid()),
            PositionSide.Long,
            null,
            null,
            Now);
        var revisionId = Guid.NewGuid();
        var input = new UserConfirmedExecutionInput(
            Now,
            new PositivePrice(1_234.5m),
            new WholeShareQuantity(100),
            CurrencyCode.Jpy,
            Now);

        var execution = position.RegisterUserConfirmedExecution(
            new TradeExecutionId(Guid.NewGuid()),
            ExecutionKind.Open,
            input,
            new TradeExecutionRevisionId(revisionId),
            Revision(revisionId, 1, null, 'a'),
            Now);

        Assert.Equal(ExecutionOrigin.UserConfirmed, execution.Origin);
        Assert.Equal(input, execution.CurrentRevision.Input);
        Assert.Null(execution.CandidateContextId);
    }

    [Fact]
    public void ExecutionCorrection_MustDirectlySupersedeCurrentLeaf()
    {
        var position = new Position(
            new PositionId(Guid.NewGuid()),
            new InstrumentId(Guid.NewGuid()),
            PositionSide.Short,
            null,
            null,
            Now);
        var firstRevisionId = Guid.NewGuid();
        var input = new UserConfirmedExecutionInput(
            Now,
            new PositivePrice(1_000m),
            new WholeShareQuantity(100),
            CurrencyCode.Jpy,
            Now);
        var execution = position.RegisterUserConfirmedExecution(
            new TradeExecutionId(Guid.NewGuid()),
            ExecutionKind.Open,
            input,
            new TradeExecutionRevisionId(firstRevisionId),
            Revision(firstRevisionId, 1, null, 'a'),
            Now);

        var invalidRevisionId = Guid.NewGuid();
        var unrelatedPredecessor = Guid.NewGuid();
        Assert.Throws<DomainException>(() => execution.AppendCorrection(
            new TradeExecutionRevisionId(invalidRevisionId),
            Revision(invalidRevisionId, 2, unrelatedPredecessor, 'b'),
            input,
            "correction"));
    }

    [Fact]
    public void RevisionMetadata_RejectsSelfSupersession()
    {
        var revisionId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => Revision(revisionId, 2, revisionId, 'a'));
    }

    [Fact]
    public void ResolvedConfirmedCost_ReplacesEstimateEvenWithoutReconciliationPointer()
    {
        var itemId = new MarginCostItemId(Guid.NewGuid());
        var item = new MarginCostItem(
            itemId,
            new MarginLotId(Guid.NewGuid()),
            MarginCostType.BuyerInterest,
            "interest-2026-08",
            new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            null,
            Now);
        var estimateId = Guid.NewGuid();
        item.AppendObservation(new MarginCostObservation(
            new MarginCostObservationId(estimateId),
            itemId,
            CostValuationKind.Estimate,
            CostDirection.Charge,
            CostAmount.Known(500m, CurrencyCode.Jpy),
            null,
            null,
            null,
            null,
            null,
            CostSourceKind.ApplicationEstimate,
            null,
            Revision(estimateId, 1, null, 'a'),
            Now,
            null));
        var confirmedId = Guid.NewGuid();
        var confirmed = new MarginCostObservation(
            new MarginCostObservationId(confirmedId),
            itemId,
            CostValuationKind.Confirmed,
            CostDirection.Charge,
            CostAmount.Known(450m, CurrencyCode.Jpy),
            null,
            null,
            null,
            null,
            null,
            CostSourceKind.BrokerStatement,
            null,
            Revision(confirmedId, 1, null, 'b'),
            Now,
            Now);
        item.AppendObservation(confirmed);

        var resolution = item.ResolveForReferenceTotal();

        Assert.Same(confirmed, resolution.SelectedForReference);
        Assert.Equal(450m, resolution.SelectedForReference!.Amount.Amount);
    }

    [Fact]
    public void ConfirmedCost_CanReconcileOnlyCurrentEstimateLeaf()
    {
        var itemId = new MarginCostItemId(Guid.NewGuid());
        var item = new MarginCostItem(
            itemId,
            new MarginLotId(Guid.NewGuid()),
            MarginCostType.BuyerInterest,
            "interest-2026-08",
            new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)),
            null,
            Now);
        var firstEstimateId = Guid.NewGuid();
        item.AppendObservation(Observation(
            itemId,
            firstEstimateId,
            CostValuationKind.Estimate,
            null,
            Revision(firstEstimateId, 1, null, 'a')));
        var currentEstimateId = Guid.NewGuid();
        item.AppendObservation(Observation(
            itemId,
            currentEstimateId,
            CostValuationKind.Estimate,
            null,
            Revision(currentEstimateId, 2, firstEstimateId, 'b')));
        var confirmedId = Guid.NewGuid();

        Assert.Throws<DomainException>(() => item.AppendObservation(Observation(
            itemId,
            confirmedId,
            CostValuationKind.Confirmed,
            new MarginCostObservationId(firstEstimateId),
            Revision(confirmedId, 1, null, 'c'))));
    }

    private static MarginCostObservation Observation(
        MarginCostItemId itemId,
        Guid id,
        CostValuationKind valuationKind,
        MarginCostObservationId? reconcilesEstimateId,
        RevisionMetadata audit) =>
        new(
            new MarginCostObservationId(id),
            itemId,
            valuationKind,
            CostDirection.Charge,
            CostAmount.Known(500m, CurrencyCode.Jpy),
            null,
            null,
            null,
            null,
            null,
            valuationKind == CostValuationKind.Estimate
                ? CostSourceKind.ApplicationEstimate
                : CostSourceKind.BrokerStatement,
            reconcilesEstimateId,
            audit,
            Now,
            valuationKind == CostValuationKind.Confirmed ? Now : null);

    private static RevisionMetadata Revision(
        Guid id,
        int revisionNumber,
        Guid? supersedesId,
        char hashCharacter) =>
        new(
            id,
            revisionNumber,
            supersedesId,
            new Sha256Hash(new string(hashCharacter, 64)),
            Now);
}
