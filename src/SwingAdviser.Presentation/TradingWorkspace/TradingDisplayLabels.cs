using System.Globalization;
using SwingAdviser.Domain.Common;

namespace SwingAdviser.Presentation.TradingWorkspace;

public enum DisplaySeverity
{
    Muted,
    Neutral,
    Positive,
    Caution,
    Warning,
    Critical,
}

public static class TradingDisplayLabels
{
    private static readonly CultureInfo Ja = CultureInfo.GetCultureInfo("ja-JP");

    public const string ScoreCaption = "スコアは勝率・利益を保証するものではありません（候補の相対順位の参考情報）。";

    public static string PositionSideLabel(PositionSide side) => side switch
    {
        PositionSide.Long => "Long（買い）",
        PositionSide.Short => "Short（売り）",
        _ => side.ToString(),
    };

    public static string ConfidenceLabel(ConfidenceLevel level) => level switch
    {
        ConfidenceLevel.High => "高",
        ConfidenceLevel.Medium => "中",
        ConfidenceLevel.Low => "低",
        _ => level.ToString(),
    };

    public static string AiStateLabel(AiAttemptStatus? status, string? detail) => status switch
    {
        null => "未実行",
        AiAttemptStatus.Queued => "待機中",
        AiAttemptStatus.Running => "実行中",
        AiAttemptStatus.Succeeded => "成功",
        AiAttemptStatus.Failed => string.IsNullOrWhiteSpace(detail) ? "失敗" : $"失敗（{detail}）",
        AiAttemptStatus.TimedOut => string.IsNullOrWhiteSpace(detail) ? "タイムアウト" : $"タイムアウト（{detail}）",
        AiAttemptStatus.InsufficientInformation => string.IsNullOrWhiteSpace(detail) ? "情報不足" : $"情報不足（{detail}）",
        AiAttemptStatus.Cancelled => "キャンセル",
        _ => status.ToString()!,
    };

    public static string AiVerdictAlignmentLabel(PositionSide side, AiVerdict? verdict) => verdict switch
    {
        null => "判定なし",
        AiVerdict.Neutral => "中立（候補方向と分けて表示）",
        AiVerdict.Bullish when side == PositionSide.Long => "候補方向と整合（Long × 強気）",
        AiVerdict.Bullish => "候補方向と逆（Short × 強気）",
        AiVerdict.Bearish when side == PositionSide.Short => "候補方向と整合（Short × 弱気）",
        AiVerdict.Bearish => "候補方向と逆（Long × 弱気）",
        _ => verdict.ToString()!,
    };

    public static string ShortAvailabilityLabel(OpenPermissionStatus? status, string? note = null)
    {
        var text = status switch
        {
            null => "—（Long候補）",
            OpenPermissionStatus.Allowed => "売建可",
            OpenPermissionStatus.Restricted => "規制あり",
            OpenPermissionStatus.Prohibited => "売建不可",
            OpenPermissionStatus.Unknown => "不明（未取得）",
            _ => status.ToString()!,
        };
        return string.IsNullOrWhiteSpace(note) ? text : $"{text} — {note}";
    }

    public static string CostAmountLabel(AmountStatus status, decimal? amount, CostValuationKind valuationKind)
    {
        var suffix = valuationKind == CostValuationKind.Confirmed ? "確定" : "見積（未確定）";
        return status switch
        {
            AmountStatus.KnownAmount when amount is not null => $"{FormatYen(amount.Value)}（{suffix}）",
            AmountStatus.KnownZero => "¥0（確定）",
            AmountStatus.NotOccurred => "不発生",
            AmountStatus.Unpublished => "未公表",
            AmountStatus.FetchFailed => "取得失敗",
            AmountStatus.NotApplicable => "対象外",
            AmountStatus.Unknown => "不明",
            _ => status.ToString(),
        };
    }

    public static string DecisionLabel(ExitDecision? decision) => decision switch
    {
        null => "判定なし／要照合",
        ExitDecision.Hold => "Hold（継続保有）",
        ExitDecision.TakeProfit => "利確候補",
        ExitDecision.StopLoss => "損切候補",
        ExitDecision.Exit => "決済候補",
        _ => decision.ToString()!,
    };

    public static string ReconciliationLabel(ReconciliationStatus status) => status switch
    {
        ReconciliationStatus.Clear => "照合済み",
        ReconciliationStatus.Required => "要照合（再評価停止）",
        ReconciliationStatus.InProgress => "照合中",
        ReconciliationStatus.Resolved => "照合完了",
        _ => status.ToString(),
    };

    public static string TermLabel(MarginTermType termType, DateTimeOffset? finalRepaymentAtUtc) => termType switch
    {
        MarginTermType.FixedDate when finalRepaymentAtUtc is not null => $"{finalRepaymentAtUtc.Value.ToLocalTime():yyyy-MM-dd}",
        MarginTermType.NoFixedTerm => "無期限（契約条件を確認）",
        _ => "期限不明",
    };

    public static string ExecutionKindLabel(ExecutionKind kind) => kind == ExecutionKind.Open ? "新規建" : "決済";

    public static string ExecutionChangeLabel(ExecutionChangeKind kind) => kind switch
    {
        ExecutionChangeKind.Initial => "新規登録",
        ExecutionChangeKind.Correction => "訂正",
        ExecutionChangeKind.Void => "取消",
        _ => kind.ToString(),
    };

    public static string FormatYen(decimal amount) => amount.ToString("¥#,##0.##;−¥#,##0.##", Ja);
    public static string FormatYen(decimal? amount) => amount is null ? "算出不可" : FormatYen(amount.Value);
}
