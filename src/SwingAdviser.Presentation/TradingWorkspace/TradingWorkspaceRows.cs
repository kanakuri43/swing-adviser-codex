using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Presentation.TradingWorkspace;

public sealed class CandidateRowViewModel(CandidateListItem item)
{
    public CandidateListItem Item { get; } = item;
    public string Code => Item.Code;
    public string Name => Item.Name;
    public string SideLabel => TradingDisplayLabels.PositionSideLabel(Item.Side);
    public string EvaluationBarDateText => Item.EvaluationBarDate.ToString("yyyy-MM-dd");
    public string AnalyzedAtText => Item.AnalyzedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string StrategyLabel => Item.StrategyLabel;
    public long Score => Item.Score;
    public string ConfidenceLabel => TradingDisplayLabels.ConfidenceLabel(Item.Confidence);
    public string PrimaryReason => Item.PrimaryReason;
    public string AiStateText => TradingDisplayLabels.AiStateLabel(Item.AiStatus, Item.AiFailureDetail);
    public string AiAlignmentText => TradingDisplayLabels.AiVerdictAlignmentLabel(Item.Side, Item.AiVerdict);
    public string ShortAvailabilityText => TradingDisplayLabels.ShortAvailabilityLabel(Item.ShortOpenStatus, Item.ShortRestrictionNote);
}

public sealed class PositionRowViewModel(PositionListItem item)
{
    public PositionListItem Item { get; } = item;
    public string Code => Item.Code;
    public string Name => Item.Name;
    public string SideLabel => TradingDisplayLabels.PositionSideLabel(Item.Side);
    public string QuantityText => $"{Item.Quantity:#,0}株";
    public string EntryPriceText => TradingDisplayLabels.FormatYen(Item.EntryBasisPrice);
    public string CurrentPriceText => TradingDisplayLabels.FormatYen(Item.CurrentPrice);
    public string PriceProfitAndLossText => TradingDisplayLabels.FormatYen(Item.PriceProfitAndLoss);
    public string EvaluationBarDateText => Item.EvaluationBarDate?.ToString("yyyy-MM-dd") ?? "未評価";
    public string EvaluationCreatedAtText => Item.EvaluationCreatedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";
    public string EvaluationStatusText => TradingDisplayLabels.EvaluationOutcomeLabel(Item.EvaluationOutcome, Item.IsEvaluationStale);
    public string StrategyLabel => Item.StrategyLabel;
    public string DecisionLabel => TradingDisplayLabels.DecisionLabel(Item.Decision);
    public string DecisionReason => Item.DecisionReason;
    public string ConfirmedCostProfitAndLossText => TradingDisplayLabels.FormatYen(Item.ConfirmedCostProfitAndLoss);
    public string EstimatedNetProfitAndLossText => TradingDisplayLabels.FormatYen(Item.EstimatedNetProfitAndLoss);
    public string CostToRRatioText => TradingDisplayLabels.FormatRiskRatio(Item.CostToRRatio);
    public string PartialExitText => TradingDisplayLabels.PartialExitLabel(
        Item.PartialExitStatus,
        Item.PartialExitQuantity,
        Item.IsEvaluationStale);
    public string StopPriceText => TradingDisplayLabels.FormatYen(Item.StopPrice);
    public string TakeProfitPriceText => TradingDisplayLabels.FormatYen(Item.TakeProfitPrice);
    public string TermText => TradingDisplayLabels.TermLabel(Item.TermType, Item.FinalRepaymentAtUtc);
    public string ReconciliationText => TradingDisplayLabels.ReconciliationLabel(Item.ReconciliationStatus);
    public bool IsExitActionable =>
        Item.ReconciliationStatus is ReconciliationStatus.Clear or ReconciliationStatus.Resolved &&
        Item.EvaluationOutcome == PositionEvaluationOutcome.Evaluated &&
        !Item.IsEvaluationStale &&
        Item.Lots.Count != 0 &&
        Item.Decision is ExitDecision.TakeProfit or ExitDecision.StopLoss or ExitDecision.Exit;
}

public sealed class ExecutionRevisionRowViewModel(TradeExecutionRevisionListItem item)
{
    public TradeExecutionRevisionListItem Item { get; } = item;
    public string RevisionText => $"rev{Item.RevisionNumber} {TradingDisplayLabels.ExecutionChangeLabel(Item.ChangeKind)}";
    public string ExecutedAtText => Item.ExecutedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string PriceText => TradingDisplayLabels.FormatYen(Item.Price);
    public string QuantityText => $"{Item.Quantity:#,0}株";
    public string UserConfirmedAtText => Item.UserConfirmedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string Note => Item.CorrectionReason ?? Item.UserNote ?? string.Empty;
}

public sealed class ExecutionRowViewModel(TradeExecutionListItem item)
{
    public TradeExecutionListItem Item { get; } = item;
    public string Code => Item.Code;
    public string Name => Item.Name;
    public string SideLabel => TradingDisplayLabels.PositionSideLabel(Item.Side);
    public string KindLabel => TradingDisplayLabels.ExecutionKindLabel(Item.Kind);
    public string OriginLabel => "利用者手入力（確認済み）";
    public IReadOnlyList<ExecutionRevisionRowViewModel> Revisions { get; } =
        item.Revisions.Select(x => new ExecutionRevisionRowViewModel(x)).ToList();
    public ExecutionRevisionRowViewModel CurrentRevision => Revisions[^1];
    public bool HasCorrections => Revisions.Count > 1;
}
