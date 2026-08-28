using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Common;
using SwingAdviser.Presentation.TradingWorkspace;

namespace SwingAdviser.Presentation.Tests.TradingWorkspace;

public sealed class PositionRowViewModelTests
{
    [Fact]
    public void StaleEvaluation_NeverEnablesManualExitRegistration()
    {
        var row = new PositionRowViewModel(CreateItem(
            decision: ExitDecision.TakeProfit,
            reconciliationStatus: ReconciliationStatus.Clear,
            isEvaluationStale: true));

        Assert.False(row.IsExitActionable);
        Assert.Equal("再評価結果が古い（参考）", row.EvaluationStatusText);
        Assert.Equal("一部利確候補（古い参考）", row.PartialExitText);
    }

    [Fact]
    public void FailClosedEvaluation_NeverUsesHoldOrEnablesManualExitRegistration()
    {
        var row = new PositionRowViewModel(CreateItem(
            decision: null,
            reconciliationStatus: ReconciliationStatus.Required,
            isEvaluationStale: false,
            outcome: PositionEvaluationOutcome.ReconciliationRequired));

        Assert.False(row.IsExitActionable);
        Assert.Contains("要照合", row.EvaluationStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("Hold", row.DecisionLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingCostMetrics_AreNotDisplayedAsZero()
    {
        var row = new PositionRowViewModel(CreateItem(
            decision: ExitDecision.Hold,
            reconciliationStatus: ReconciliationStatus.Clear,
            isEvaluationStale: false,
            confirmedCostProfitAndLoss: null,
            estimatedNetProfitAndLoss: null,
            costToRRatio: null));

        Assert.Equal("算出不可", row.ConfirmedCostProfitAndLossText);
        Assert.Equal("算出不可", row.EstimatedNetProfitAndLossText);
        Assert.Equal("算出不可", row.CostToRRatioText);
    }

    private static PositionListItem CreateItem(
        ExitDecision? decision,
        ReconciliationStatus reconciliationStatus,
        bool isEvaluationStale,
        PositionEvaluationOutcome outcome = PositionEvaluationOutcome.Evaluated,
        decimal? confirmedCostProfitAndLoss = 990m,
        decimal? estimatedNetProfitAndLoss = 980m,
        decimal? costToRRatio = 0.02m) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "7203",
            "テスト銘柄",
            PositionSide.Long,
            100m,
            1000m,
            1010m,
            new DateOnly(2026, 8, 25),
            outcome,
            new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero),
            isEvaluationStale,
            "Test 1",
            decision,
            "テスト理由",
            1000m,
            confirmedCostProfitAndLoss,
            estimatedNetProfitAndLoss,
            costToRRatio,
            50,
            PartialExitStatus.Candidate,
            950m,
            1100m,
            MarginTermType.NoFixedTerm,
            null,
            reconciliationStatus,
            [new MarginLotListItem(Guid.NewGuid(), Guid.NewGuid(), "テストlot", 100m, DateTimeOffset.UtcNow)]);
}
