using System.Globalization;
using SwingAdviser.Domain.Analysis;
using SwingAdviser.Domain.Common;
using SwingAdviser.Domain.Positions;

namespace SwingAdviser.Infrastructure.Tests.Domain;

public sealed class LotRiskEvaluatorTests
{
    private static readonly DateTimeOffset ExecutedAtUtc =
        new(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset RecordedAtUtc = ExecutedAtUtc.AddMinutes(1);
    private static readonly DateOnly EvaluationBarDate = new(2026, 8, 28);
    private static readonly DateTimeOffset RiskPlanCutoffAtUtc =
        new(2026, 8, 27, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("1449", "701", ExitDecision.Hold)]
    [InlineData("1449", "700", ExitDecision.StopLoss)]
    [InlineData("1449", "699", ExitDecision.StopLoss)]
    [InlineData("1450", "701", ExitDecision.TakeProfit)]
    [InlineData("1451", "701", ExitDecision.TakeProfit)]
    public void Long_UsesLowForStopAndHighForTakeProfit_WithInclusiveLines(
        string highText,
        string lowText,
        ExitDecision expected)
    {
        var bundle = CreateInitialPlan(PositionSide.Long);

        var result = Evaluate(bundle, highText, lowText);

        Assert.Equal(expected, result.Decision);
        Assert.Equal(bundle.RiskBasis.Id, result.RiskBasisSnapshotId);
        Assert.Equal(bundle.RiskBasis.MarginLotId, result.MarginLotId);
        Assert.Equal(bundle.RiskPlan.Audit.Id, result.RiskPlanRevisionId);
    }

    [Theory]
    [InlineData("1249", "626", ExitDecision.Hold)]
    [InlineData("1250", "626", ExitDecision.StopLoss)]
    [InlineData("1251", "626", ExitDecision.StopLoss)]
    [InlineData("1249", "625", ExitDecision.TakeProfit)]
    [InlineData("1249", "624", ExitDecision.TakeProfit)]
    public void Short_UsesHighForStopAndLowForTakeProfit_WithInclusiveLines(
        string highText,
        string lowText,
        ExitDecision expected)
    {
        var bundle = CreateInitialPlan(PositionSide.Short);

        var result = Evaluate(bundle, highText, lowText);

        Assert.Equal(expected, result.Decision);
    }

    [Theory]
    [InlineData(PositionSide.Long, "1450", "700")]
    [InlineData(PositionSide.Short, "1250", "625")]
    public void StopLoss_HasPriorityWhenBothLinesAreReached(
        PositionSide side,
        string highText,
        string lowText)
    {
        var bundle = CreateInitialPlan(side);

        var result = Evaluate(bundle, highText, lowText);

        Assert.Equal(ExitDecision.StopLoss, result.Decision);
        Assert.All(result.Reasons, reason => Assert.True(reason.Reached));
    }

    [Fact]
    public void Long_ReturnsTypedReasonsForBothReachedAndNotReachedLines()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);

        var result = Evaluate(bundle, "1449", "701");

        Assert.Equal("holding-risk-evaluation-v1", LotRiskEvaluator.AlgorithmVersion);
        var stop = Assert.Single(result.Reasons, reason => reason.LineKind == RiskLineKind.StopLoss);
        Assert.Equal(DailyBarPriceField.Low, stop.ObservedField);
        Assert.Equal(PriceLineComparison.LessThanOrEqual, stop.Comparison);
        Assert.Equal(701m, stop.ObservedPrice.Value);
        Assert.Equal(700m, stop.LinePrice.Value);
        Assert.False(stop.Reached);
        var target = Assert.Single(result.Reasons, reason => reason.LineKind == RiskLineKind.TakeProfit);
        Assert.Equal(DailyBarPriceField.High, target.ObservedField);
        Assert.Equal(PriceLineComparison.GreaterThanOrEqual, target.Comparison);
        Assert.Equal(1_449m, target.ObservedPrice.Value);
        Assert.Equal(1_450m, target.LinePrice.Value);
        Assert.False(target.Reached);
    }

    [Fact]
    public void Short_ReturnsDirectionSpecificStructuredReasons()
    {
        var bundle = CreateInitialPlan(PositionSide.Short);

        var result = Evaluate(bundle, "1249", "626");

        var stop = Assert.Single(result.Reasons, reason => reason.LineKind == RiskLineKind.StopLoss);
        Assert.Equal(DailyBarPriceField.High, stop.ObservedField);
        Assert.Equal(PriceLineComparison.GreaterThanOrEqual, stop.Comparison);
        var target = Assert.Single(result.Reasons, reason => reason.LineKind == RiskLineKind.TakeProfit);
        Assert.Equal(DailyBarPriceField.Low, target.ObservedField);
        Assert.Equal(PriceLineComparison.LessThanOrEqual, target.Comparison);
    }

    [Fact]
    public void Evaluation_UsesTheOnlyActiveLatestPlanLeaf()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var corrected = NextPlan(
            bundle,
            bundle.RiskPlan,
            stopPrice: 800m,
            takeProfitPrice: 1_300m);

        var result = Evaluate(bundle, "1350", "900", [bundle.RiskPlan, corrected]);

        Assert.Equal(ExitDecision.TakeProfit, result.Decision);
        Assert.Equal(corrected.Audit.Id, result.RiskPlanRevisionId);
        Assert.Equal(800m, result.StopPrice.Value);
        Assert.Equal(1_300m, result.TakeProfitPrice.Value);
    }

    [Fact]
    public void Evaluation_IgnoresPlanRevisionRecordedOrEffectiveAfterTheCutoff()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var current = NextPlan(bundle, bundle.RiskPlan, 800m, 1_300m);
        var future = NextPlan(
            bundle,
            current,
            950m,
            1_050m,
            RiskPlanCutoffAtUtc.AddMinutes(1),
            RiskPlanCutoffAtUtc.AddMinutes(1));

        var result = Evaluate(bundle, "1350", "900", [bundle.RiskPlan, current, future]);

        Assert.Equal(ExitDecision.TakeProfit, result.Decision);
        Assert.Equal(current.Audit.Id, result.RiskPlanRevisionId);
        Assert.Equal(RiskPlanCutoffAtUtc, result.RiskPlanCutoffAtUtc);
    }

    [Fact]
    public void Evaluation_RejectsMissingOrBranchedRiskPlanGraph()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var firstBranch = NextPlan(bundle, bundle.RiskPlan, 800m, 1_300m);
        var secondBranch = NextPlan(bundle, bundle.RiskPlan, 850m, 1_350m);

        Assert.Throws<DomainException>(() => Evaluate(bundle, "1000", "900", []));
        Assert.Throws<DomainException>(() => Evaluate(
            bundle,
            "1000",
            "900",
            [bundle.RiskPlan, firstBranch, secondBranch]));
    }

    [Fact]
    public void Evaluation_RejectsDisconnectedOrForeignRiskPlanGraph()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var disconnectedId = Guid.NewGuid();
        var disconnected = new RiskPlanRevision(
            bundle.RiskBasis.Id,
            new RevisionMetadata(disconnectedId, 2, Guid.NewGuid(), Hash('d'), RecordedAtUtc.AddMinutes(1)),
            new PositivePrice(800m),
            new PositivePrice(1_300m),
            RiskPlanReason.UserCorrection,
            RecordedAtUtc.AddMinutes(1));
        var other = CreateInitialPlan(PositionSide.Long);

        Assert.Throws<DomainException>(() => Evaluate(bundle, "1000", "900", [disconnected]));
        Assert.Throws<DomainException>(() => Evaluate(bundle, "1000", "900", [other.RiskPlan]));
    }

    [Fact]
    public void Evaluation_RejectsPriceUnitMismatchOrInvalidDailyRange()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var otherUnit = new RiskPriceUnit(
            bundle.RiskBasis.PriceUnit.InstrumentId,
            bundle.RiskBasis.PriceUnit.Currency,
            Hash('e'));

        Assert.Throws<DomainException>(() => LotRiskEvaluator.Evaluate(
            bundle.RiskBasis,
            [bundle.RiskPlan],
            EvaluationBarDate,
            RiskPlanCutoffAtUtc,
            new UnitizedRiskPrice(new PositivePrice(1_000m), otherUnit),
            new UnitizedRiskPrice(new PositivePrice(900m), otherUnit)));
        Assert.Throws<DomainException>(() => Evaluate(bundle, "900", "1000"));
    }

    [Fact]
    public void RiskPlanRevision_RequiresAnInitialRootAndTriggerFreeUserCorrection()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var rootId = Guid.NewGuid();
        var correctionId = Guid.NewGuid();

        Assert.Throws<DomainException>(() => new RiskPlanRevision(
            bundle.RiskBasis.Id,
            new RevisionMetadata(rootId, 1, null, Hash('d'), RecordedAtUtc),
            new PositivePrice(800m),
            new PositivePrice(1_300m),
            RiskPlanReason.UserCorrection,
            RecordedAtUtc));
        Assert.Throws<DomainException>(() => new RiskPlanRevision(
            bundle.RiskBasis.Id,
            new RevisionMetadata(
                correctionId,
                2,
                bundle.RiskPlan.Audit.Id,
                Hash('d'),
                RecordedAtUtc.AddMinutes(1)),
            new PositivePrice(800m),
            new PositivePrice(1_300m),
            RiskPlanReason.UserCorrection,
            RecordedAtUtc.AddMinutes(1),
            TradeExecutionId.New()));
    }

    [Theory]
    [InlineData(PositionSide.Long)]
    [InlineData(PositionSide.Short)]
    public void MacdReversalAlone_ProducesLotExit_WhenTargetIsReached(PositionSide side)
    {
        var bundle = CreateInitialPlan(side);
        var input = side == PositionSide.Long
            ? Technical(bundle, new(1m, -1m), new(1m, 0m), null, null)
            : Technical(bundle, new(1m, 3m), new(1m, 2m), null, null);

        var result = EvaluateHoldingAtTarget(bundle, input);

        Assert.Equal(LotHoldingEvaluationOutcome.Evaluated, result.Outcome);
        Assert.Equal(ExitDecision.Exit, result.Decision);
        Assert.Equal(TechnicalReversalStatus.Matched, result.TechnicalReversal.Status);
        Assert.Equal(bundle.RiskBasis.MarginLotId, result.MarginLotId);
        var macd = Assert.IsType<MacdReversalReason>(
            Assert.Single(result.TechnicalReversal.Reasons, reason =>
                reason.Condition == ReversalConditionKind.MacdCross));
        Assert.Equal(ReversalConditionStatus.Matched, macd.Status);
        Assert.Equal(ReversalConditionStatus.Missing, Assert.Single(
            result.TechnicalReversal.Reasons,
            reason => reason.Condition == ReversalConditionKind.Ema20State).Status);
    }

    [Theory]
    [InlineData(PositionSide.Long, "900", "950")]
    [InlineData(PositionSide.Short, "1100", "1050")]
    public void Ema20ReversalAlone_ProducesLotExit_WhenTargetIsReached(
        PositionSide side,
        string closeText,
        string emaText)
    {
        var bundle = CreateInitialPlan(side);
        var input = Technical(
            bundle,
            null,
            null,
            Parse(closeText),
            Parse(emaText));

        var result = EvaluateHoldingAtTarget(bundle, input);

        Assert.Equal(ExitDecision.Exit, result.Decision);
        Assert.Equal(TechnicalReversalStatus.Matched, result.TechnicalReversal.Status);
        var ema = Assert.IsType<Ema20ReversalReason>(Assert.Single(
            result.TechnicalReversal.Reasons,
            reason => reason.Condition == ReversalConditionKind.Ema20State));
        Assert.Equal(ReversalConditionStatus.Matched, ema.Status);
        Assert.Equal(Parse(closeText), ema.Close!.Value.Value);
        Assert.Equal(Parse(emaText), ema.Ema20!.Value.Value);
    }

    [Fact]
    public void BothReversalConditionsMatched_StillProduceOneLotExit()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var input = Technical(bundle, new(2m, -1m), new(1m, 0m), 900m, 950m);

        var result = EvaluateHoldingAtTarget(bundle, input);

        Assert.Equal(ExitDecision.Exit, result.Decision);
        Assert.All(result.TechnicalReversal.Reasons, reason =>
            Assert.Equal(ReversalConditionStatus.Matched, reason.Status));
    }

    [Theory]
    [InlineData(PositionSide.Long, "2", "1", "1", "1")]
    [InlineData(PositionSide.Short, "0", "1", "1", "1")]
    public void MacdCurrentEquality_IsNotAReversal(
        PositionSide side,
        string previousLine,
        string currentLine,
        string previousSignal,
        string currentSignal)
    {
        var bundle = CreateInitialPlan(side);
        var input = Technical(
            bundle,
            new(Parse(previousLine), Parse(currentLine)),
            new(Parse(previousSignal), Parse(currentSignal)),
            1_000m,
            1_000m);

        var result = EvaluateHoldingAtTarget(bundle, input);

        Assert.Equal(TechnicalReversalStatus.NotMatched, result.TechnicalReversal.Status);
        Assert.Equal(ExitDecision.TakeProfit, result.Decision);
    }

    [Fact]
    public void BothConditionsNotMatched_ProducesTakeProfitInsteadOfExit()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var input = Technical(bundle, new(0m, 2m), new(1m, 1m), 1_000m, 1_000m);

        var result = EvaluateHoldingAtTarget(bundle, input);

        Assert.Equal(TechnicalReversalStatus.NotMatched, result.TechnicalReversal.Status);
        Assert.Equal(ExitDecision.TakeProfit, result.Decision);
        Assert.Equal(LotHoldingEvaluationOutcome.Evaluated, result.Outcome);
    }

    [Fact]
    public void OneNotMatchedAndOneMissing_IsIndeterminateAndNeverFallsBack()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var input = Technical(bundle, new(0m, 2m), new(1m, 1m), null, null);

        var result = EvaluateHoldingAtTarget(bundle, input);

        Assert.Equal(TechnicalReversalStatus.Indeterminate, result.TechnicalReversal.Status);
        Assert.Equal(LotHoldingEvaluationOutcome.Indeterminate, result.Outcome);
        Assert.Null(result.Decision);
    }

    [Fact]
    public void BothConditionsMissing_IsIndeterminateAfterTargetReach()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);

        var result = EvaluateHoldingAtTarget(bundle, technicalInput: null);

        Assert.Equal(TechnicalReversalStatus.Indeterminate, result.TechnicalReversal.Status);
        Assert.Equal(LotHoldingEvaluationOutcome.Indeterminate, result.Outcome);
        Assert.Null(result.Decision);
    }

    [Fact]
    public void MissingTechnicalIndicators_AreNotRequiredBeforeAnyTargetReach()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);

        var result = EvaluateHolding(
            bundle,
            high: "1200",
            low: "900",
            PriorTakeProfitReach.NotReached,
            technicalInput: null);

        Assert.Equal(ExitDecision.Hold, result.Decision);
        Assert.Equal(TechnicalReversalStatus.NotApplicable, result.TechnicalReversal.Status);
    }

    [Theory]
    [InlineData(PositionSide.Long)]
    [InlineData(PositionSide.Short)]
    public void ExactPriorTargetReachEvidence_EnablesReversalExitOnALaterBar(PositionSide side)
    {
        var bundle = CreateInitialPlan(side);
        var prior = PriorReach(bundle);
        var input = side == PositionSide.Long
            ? Technical(bundle, new(1m, -1m), new(1m, 0m), 1_000m, 900m)
            : Technical(bundle, new(1m, 3m), new(1m, 2m), 1_000m, 1_100m);

        var result = side == PositionSide.Long
            ? EvaluateHolding(bundle, "1200", "900", prior, input)
            : EvaluateHolding(bundle, "1100", "900", prior, input);

        Assert.Equal(ExitDecision.Exit, result.Decision);
        Assert.True(result.TakeProfitReached);
        Assert.Same(prior, result.PriorTakeProfitReach);
        Assert.Equal(bundle.RiskPlan.Audit.Id, prior.Evidence!.RiskPlanRevisionId);
    }

    [Fact]
    public void IndeterminatePriorReach_PreventsHoldWhenCurrentTargetIsNotReached()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);

        var result = EvaluateHolding(
            bundle,
            "1200",
            "900",
            PriorTakeProfitReach.Indeterminate,
            Technical(bundle, new(0m, 2m), new(1m, 1m), 1_000m, 1_000m));

        Assert.Equal(LotHoldingEvaluationOutcome.Indeterminate, result.Outcome);
        Assert.Null(result.Decision);
        Assert.Null(result.TakeProfitReached);
        Assert.Equal(TechnicalReversalStatus.NotApplicable, result.TechnicalReversal.Status);
    }

    [Fact]
    public void CurrentTargetReach_IsSufficientEvenWhenPriorReachWasIndeterminate()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var input = Technical(bundle, new(1m, -1m), new(1m, 0m), null, null);

        var result = EvaluateHoldingAtTarget(
            bundle,
            input,
            PriorTakeProfitReach.Indeterminate);

        Assert.Equal(ExitDecision.Exit, result.Decision);
        Assert.Equal(LotHoldingEvaluationOutcome.Evaluated, result.Outcome);
    }

    [Fact]
    public void StopLossOverridesReversalAndPreservesMatchedTechnicalReasons()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var input = Technical(bundle, new(1m, -1m), new(1m, 0m), 900m, 950m);

        var result = EvaluateHolding(
            bundle,
            high: "1450",
            low: "700",
            PriorTakeProfitReach.NotReached,
            input);

        Assert.Equal(ExitDecision.StopLoss, result.Decision);
        Assert.Equal(TechnicalReversalStatus.Matched, result.TechnicalReversal.Status);
        Assert.All(result.PriceLineEvaluation.Reasons, reason => Assert.True(reason.Reached));
    }

    [Fact]
    public void StopLossRemainsEvaluatedWhenReversalIndicatorsAreMissing()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);

        var result = EvaluateHolding(
            bundle,
            high: "1450",
            low: "700",
            PriorTakeProfitReach.NotReached,
            technicalInput: null);

        Assert.Equal(ExitDecision.StopLoss, result.Decision);
        Assert.Equal(LotHoldingEvaluationOutcome.Evaluated, result.Outcome);
        Assert.Equal(TechnicalReversalStatus.Indeterminate, result.TechnicalReversal.Status);
    }

    [Fact]
    public void StopLossRemainsEvaluatedWhenPriorReachIsIndeterminate()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);

        var result = EvaluateHolding(
            bundle,
            high: "1200",
            low: "700",
            PriorTakeProfitReach.Indeterminate,
            technicalInput: null);

        Assert.Equal(ExitDecision.StopLoss, result.Decision);
        Assert.Equal(LotHoldingEvaluationOutcome.Evaluated, result.Outcome);
        Assert.Null(result.TakeProfitReached);
        Assert.Equal(TechnicalReversalStatus.NotApplicable, result.TechnicalReversal.Status);
    }

    [Fact]
    public void ReversalEvaluation_RejectsUnitMismatchAndInvalidPriorEvidence()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var otherUnit = new RiskPriceUnit(
            bundle.RiskBasis.PriceUnit.InstrumentId,
            bundle.RiskBasis.PriceUnit.Currency,
            Hash('e'));
        var mismatched = new LotTechnicalReversalInput(
            otherUnit,
            new(1m, -1m),
            new(1m, 0m),
            null,
            null);
        var invalidEvidence = PriorTakeProfitReach.Reached(new LotTakeProfitReachEvidence(
            EvaluationBarDate.AddDays(-1),
            Guid.NewGuid(),
            bundle.RiskPlan.Audit.Id,
            DailyBarPriceField.Low,
            Price("1450", bundle.RiskBasis.PriceUnit),
            Price("1450", bundle.RiskBasis.PriceUnit)));

        Assert.Throws<DomainException>(() => EvaluateHoldingAtTarget(bundle, mismatched));
        Assert.Throws<DomainException>(() => EvaluateHolding(
            bundle,
            "1200",
            "900",
            invalidEvidence,
            technicalInput: null));
    }

    [Fact]
    public void PriorReachState_RequiresEvidenceOnlyForReachedStatus()
    {
        var bundle = CreateInitialPlan(PositionSide.Long);
        var evidence = PriorReach(bundle).Evidence!;

        Assert.Throws<DomainException>(() => new PriorTakeProfitReach(
            PriorTakeProfitReachStatus.Reached));
        Assert.Throws<DomainException>(() => new PriorTakeProfitReach(
            PriorTakeProfitReachStatus.NotReached,
            evidence));
    }

    private static LotRiskEvaluation Evaluate(
        InitialRiskPlanBundle bundle,
        string highText,
        string lowText,
        IReadOnlyCollection<RiskPlanRevision>? plans = null) =>
        LotRiskEvaluator.Evaluate(
            bundle.RiskBasis,
            plans ?? [bundle.RiskPlan],
            EvaluationBarDate,
            RiskPlanCutoffAtUtc,
            Price(highText, bundle.RiskBasis.PriceUnit),
            Price(lowText, bundle.RiskBasis.PriceUnit));

    private static LotHoldingRiskEvaluation EvaluateHoldingAtTarget(
        InitialRiskPlanBundle bundle,
        LotTechnicalReversalInput? technicalInput,
        PriorTakeProfitReach? priorReach = null) =>
        bundle.RiskBasis.Side == PositionSide.Long
            ? EvaluateHolding(
                bundle,
                "1450",
                "900",
                priorReach ?? PriorTakeProfitReach.NotReached,
                technicalInput)
            : EvaluateHolding(
                bundle,
                "1100",
                "625",
                priorReach ?? PriorTakeProfitReach.NotReached,
                technicalInput);

    private static LotHoldingRiskEvaluation EvaluateHolding(
        InitialRiskPlanBundle bundle,
        string high,
        string low,
        PriorTakeProfitReach priorReach,
        LotTechnicalReversalInput? technicalInput) =>
        LotHoldingRiskEvaluator.Evaluate(
            bundle.RiskBasis,
            [bundle.RiskPlan],
            EvaluationBarDate,
            RiskPlanCutoffAtUtc,
            Price(high, bundle.RiskBasis.PriceUnit),
            Price(low, bundle.RiskBasis.PriceUnit),
            priorReach,
            technicalInput);

    private static LotTechnicalReversalInput Technical(
        InitialRiskPlanBundle bundle,
        CurrentAndPreviousValue? line,
        CurrentAndPreviousValue? signal,
        decimal? close,
        decimal? ema20) =>
        new(
            bundle.RiskBasis.PriceUnit,
            line,
            signal,
            close is { } closeValue ? new PositivePrice(closeValue) : null,
            ema20 is { } emaValue ? new PositivePrice(emaValue) : null);

    private static PriorTakeProfitReach PriorReach(InitialRiskPlanBundle bundle)
    {
        var observedField = bundle.RiskBasis.Side == PositionSide.Long
            ? DailyBarPriceField.High
            : DailyBarPriceField.Low;
        return PriorTakeProfitReach.Reached(new LotTakeProfitReachEvidence(
            EvaluationBarDate.AddDays(-1),
            Guid.NewGuid(),
            bundle.RiskPlan.Audit.Id,
            observedField,
            new UnitizedRiskPrice(bundle.RiskPlan.TakeProfitPrice, bundle.RiskBasis.PriceUnit),
            new UnitizedRiskPrice(bundle.RiskPlan.TakeProfitPrice, bundle.RiskBasis.PriceUnit)));
    }

    private static InitialRiskPlanBundle CreateInitialPlan(PositionSide side)
    {
        var position = new Position(
            PositionId.New(),
            InstrumentId.New(),
            side,
            null,
            null,
            ExecutedAtUtc);
        var executionId = TradeExecutionId.New();
        var executionRevisionId = TradeExecutionRevisionId.New();
        var execution = position.RegisterUserConfirmedExecution(
            executionId,
            ExecutionKind.Open,
            new UserConfirmedExecutionInput(
                ExecutedAtUtc,
                new PositivePrice(1_000m),
                new WholeShareQuantity(100),
                CurrencyCode.Jpy,
                ExecutedAtUtc),
            executionRevisionId,
            new RevisionMetadata(executionRevisionId.Value, 1, null, Hash('a'), ExecutedAtUtc),
            ExecutedAtUtc);
        var lot = MarginLot.FromUserConfirmedOpening(MarginLotId.New(), execution, ExecutedAtUtc);
        var unit = new RiskPriceUnit(position.InstrumentId, CurrencyCode.Jpy, Hash('b'));
        var planId = Guid.NewGuid();
        return InitialRiskPlanBundle.Create(
            Guid.NewGuid(),
            new RevisionMetadata(planId, 1, null, Hash('c'), RecordedAtUtc),
            position,
            lot,
            new UnitizedRiskPrice(new PositivePrice(1_000m), unit),
            new DateOnly(2026, 8, 26),
            new UnitizedRiskPrice(new PositivePrice(100m), unit),
            14,
            "wilder-atr-v1",
            RiskManagementParameters.Initial,
            Hash('c'),
            ExecutedAtUtc,
            RecordedAtUtc);
    }

    private static RiskPlanRevision NextPlan(
        InitialRiskPlanBundle bundle,
        RiskPlanRevision predecessor,
        decimal stopPrice,
        decimal takeProfitPrice,
        DateTimeOffset? recordedAtUtc = null,
        DateTimeOffset? effectiveAtUtc = null)
    {
        var id = Guid.NewGuid();
        var recordedAt = recordedAtUtc ?? predecessor.Audit.RecordedAtUtc.AddMinutes(1);
        return new RiskPlanRevision(
            bundle.RiskBasis.Id,
            new RevisionMetadata(
                id,
                predecessor.Audit.RevisionNumber + 1,
                predecessor.Audit.Id,
                Hash('d'),
                recordedAt),
            new PositivePrice(stopPrice),
            new PositivePrice(takeProfitPrice),
            RiskPlanReason.UserCorrection,
            effectiveAtUtc ?? predecessor.EffectiveAtUtc.AddMinutes(1));
    }

    private static UnitizedRiskPrice Price(string value, RiskPriceUnit unit) =>
        new(new PositivePrice(decimal.Parse(value, CultureInfo.InvariantCulture)), unit);

    private static decimal Parse(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);

    private static Sha256Hash Hash(char value) => new(new string(value, 64));
}
