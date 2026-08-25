using System.Globalization;
using SwingAdviser.Domain.Common;
using SwingAdviser.UiMock.Mock;

namespace SwingAdviser.UiMock.Shared;

/// <summary>
/// 表示の強弱・警告度。バッジの色分けに使う。ラベル文言そのものではない。
/// </summary>
public enum MockSeverity
{
    Muted,
    Neutral,
    Positive,
    Caution,
    Warning,
    Critical,
}

/// <summary>
/// 全てのラベル整形をここに集約する。AGENT.md / docs の non-negotiable な表現規則
/// （スコア≠勝率、AI verdictは候補方向とセット、欠損は¥0にしない 等）はこのクラスの外に出さない。
/// 3案の全ての行ViewModelはここを経由した文字列だけを公開し、生のenumをXAMLへ渡さない。
/// </summary>
public static class MockLabels
{
    private static readonly CultureInfo Ja = CultureInfo.GetCultureInfo("ja-JP");

    public static string ScoreCaption { get; } = "スコアは勝率・利益を保証するものではありません（候補の相対順位の参考情報）。";

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

    public static string EvaluationBarDateText(DateOnly date) => date.ToString("yyyy-MM-dd");

    public static string EvaluationBarDateLabel(DateOnly date) => $"判定基準バー日: {EvaluationBarDateText(date)}";

    public static string AnalyzedAtText(DateTimeOffset analyzedAtUtc)
    {
        var jst = TimeZoneInfo.ConvertTime(analyzedAtUtc, GetJstTimeZone());
        return $"{jst:yyyy-MM-dd HH:mm} JST";
    }

    public static string AnalyzedAtLabel(DateTimeOffset analyzedAtUtc) => $"分析実行日時: {AnalyzedAtText(analyzedAtUtc)}";

    private static TimeZoneInfo GetJstTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        }
    }

    public static (string Text, MockSeverity Severity) AiStateLabel(
        MockAiState state,
        DateOnly? staleEvaluationBarDate,
        string? failureDetail)
    {
        return state switch
        {
            MockAiState.NotRun => ("未実行", MockSeverity.Muted),
            MockAiState.Queued => ("待機中", MockSeverity.Neutral),
            MockAiState.Running => ("実行中", MockSeverity.Neutral),
            MockAiState.Succeeded => ("成功", MockSeverity.Positive),
            MockAiState.Failed => (failureDetail is null ? "失敗" : $"失敗（{failureDetail}）", MockSeverity.Warning),
            MockAiState.FailedInterrupted => (failureDetail is null ? "失敗（中断）" : $"失敗（中断・{failureDetail}）", MockSeverity.Warning),
            MockAiState.TimedOut => (failureDetail is null ? "タイムアウト" : $"タイムアウト（{failureDetail}）", MockSeverity.Warning),
            MockAiState.InsufficientInformation => (failureDetail is null ? "情報不足" : $"情報不足（{failureDetail}）", MockSeverity.Caution),
            MockAiState.Cancelled => (failureDetail is null ? "キャンセル" : $"キャンセル（{failureDetail}）", MockSeverity.Muted),
            MockAiState.Stale => (staleEvaluationBarDate is { } d
                ? $"旧結果（判定基準バー {d:yyyy-MM-dd} 時点）"
                : "旧結果", MockSeverity.Caution),
            _ => (state.ToString(), MockSeverity.Neutral),
        };
    }

    /// <summary>
    /// AI Verdict は候補方向と切り離して表示しない（product-spec.md UI/UX 節）。
    /// Verdict が null（情報不足等）の場合は「判定なし」とし、中立へ変換しない。
    /// </summary>
    public static string AiVerdictAlignmentLabel(PositionSide side, AiVerdict? verdict)
    {
        if (verdict is null)
        {
            return "判定なし";
        }

        return verdict.Value switch
        {
            AiVerdict.Neutral => "中立",
            AiVerdict.Bullish when side == PositionSide.Long => "候補方向と整合（Long × 強気）",
            AiVerdict.Bullish => "候補方向と逆（Short × 強気）",
            AiVerdict.Bearish when side == PositionSide.Short => "候補方向と整合（Short × 弱気）",
            AiVerdict.Bearish => "候補方向と逆（Long × 弱気）",
            _ => verdict.Value.ToString(),
        };
    }

    /// <summary>
    /// テクニカルなShort Entryと実際に売建可能かは別情報として扱う（data-sources.md）。
    /// 不明時は「不明」を返し、売建可能と推測しない。
    /// </summary>
    public static (string Text, MockSeverity Severity) ShortAvailabilityLabel(OpenPermissionStatus? status, string? note)
    {
        var (text, severity) = status switch
        {
            null => ("—（Long候補）", MockSeverity.Muted),
            OpenPermissionStatus.Allowed => ("売建可", MockSeverity.Positive),
            OpenPermissionStatus.Restricted => ("規制あり", MockSeverity.Caution),
            OpenPermissionStatus.Prohibited => ("売建不可", MockSeverity.Critical),
            OpenPermissionStatus.Unknown => ("不明（未取得）", MockSeverity.Caution),
            _ => (status.Value.ToString(), MockSeverity.Neutral),
        };

        return note is null ? (text, severity) : ($"{text} — {note}", severity);
    }

    public static string ExclusionReasonLabel(TechnicalAnalysisOutcome outcome, int? barCount, int? requiredBarCount, string reason)
    {
        var head = outcome switch
        {
            TechnicalAnalysisOutcome.InsufficientHistory => "履歴不足",
            TechnicalAnalysisOutcome.HistoryIncomplete => "履歴不完全",
            TechnicalAnalysisOutcome.InvalidData => "データ不正",
            TechnicalAnalysisOutcome.PointInTimeUnverified => "point-in-time未検証",
            TechnicalAnalysisOutcome.ReconciliationRequired => "要照合",
            TechnicalAnalysisOutcome.Failed => "判定失敗",
            _ => outcome.ToString(),
        };

        var counts = barCount is not null && requiredBarCount is not null
            ? $"（保有{barCount}本 / 必要{requiredBarCount}本）"
            : string.Empty;

        return $"{head}{counts}: {reason}";
    }

    public static string DecisionLabel(ExitDecision? decision) => decision switch
    {
        null => "要照合のため判定保留",
        ExitDecision.Hold => "Hold（継続保有）",
        ExitDecision.TakeProfit => "利確候補",
        ExitDecision.StopLoss => "損切候補",
        ExitDecision.Exit => "決済候補",
        _ => decision.Value.ToString(),
    };

    /// <summary>
    /// 欠損（未公表/取得失敗/不明/不発生）を ¥0 と混同しない。KnownZero だけが「¥0（確定）」になる。
    /// </summary>
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

    public static string FormatYen(decimal amount) => amount.ToString("¥#,##0;−¥#,##0", Ja);

    /// <summary>
    /// 損益は null（算出不可）を ¥0 として表示しない。
    /// </summary>
    public static string ProfitLossLabel(decimal? amount) => amount is null ? "算出不可" : FormatYen(amount.Value);

    public static string CostToRRatioLabel(decimal? ratio) => ratio is null ? "算出不可" : ratio.Value.ToString("0.00", Ja);

    /// <summary>
    /// 期限不明/期限接近/期限超過は別状態にし、警告だけで決済済みにしない（risk-management.md:50）。
    /// 閾値は 30/10/5/1 営業日前。
    /// </summary>
    public static (string Text, MockSeverity Severity) DeadlineStateLabel(MarginTermType termType, int? remainingBusinessDays)
    {
        if (termType == MarginTermType.Unknown || remainingBusinessDays is null)
        {
            return ("期限不明", MockSeverity.Warning);
        }

        var days = remainingBusinessDays.Value;
        return days switch
        {
            < 0 => ($"期限超過（{-days}営業日経過）", MockSeverity.Critical),
            <= 1 => ($"期限超過間近（残{days}営業日）", MockSeverity.Critical),
            <= 5 => ($"期限接近（残{days}営業日）", MockSeverity.Warning),
            <= 10 => ($"期限接近（残{days}営業日）", MockSeverity.Caution),
            <= 30 => ($"期限接近（残{days}営業日）", MockSeverity.Neutral),
            _ => ($"通常（残{days}営業日）", MockSeverity.Muted),
        };
    }

    public static string RemainingBusinessDaysLabel(MarginTermType termType, int? remainingBusinessDays)
    {
        if (termType == MarginTermType.Unknown || remainingBusinessDays is null)
        {
            return "不明";
        }

        return remainingBusinessDays.Value < 0
            ? $"{-remainingBusinessDays.Value}営業日超過"
            : $"残{remainingBusinessDays.Value}営業日";
    }

    public static string PartialExitLabel(PartialExitStatus status, long? quantity, decimal? effectiveFraction, string? note)
    {
        var head = status switch
        {
            PartialExitStatus.NotApplicable => "対象外",
            PartialExitStatus.Candidate when quantity is not null =>
                effectiveFraction is not null && Math.Abs(effectiveFraction.Value - 0.5m) > 0.001m
                    ? $"一部利確候補 {quantity}株（実効割合 {effectiveFraction.Value:P1}）"
                    : $"一部利確候補 {quantity}株",
            PartialExitStatus.Candidate => "一部利確候補",
            PartialExitStatus.NotFeasible => "分割不能",
            _ => status.ToString(),
        };

        return note is null ? head : $"{head} — {note}";
    }

    public static string ExecutionChangeKindLabel(ExecutionChangeKind kind) => kind switch
    {
        ExecutionChangeKind.Initial => "新規登録",
        ExecutionChangeKind.Correction => "訂正",
        ExecutionChangeKind.Void => "取消",
        _ => kind.ToString(),
    };

    public static string ReconciliationStatusLabel(ReconciliationStatus status) => status switch
    {
        ReconciliationStatus.Clear => "照合済み",
        ReconciliationStatus.Required => "要照合（自動再評価停止中）",
        ReconciliationStatus.InProgress => "照合中",
        ReconciliationStatus.Resolved => "照合完了",
        _ => status.ToString(),
    };

    public static string AnalysisRunStatusLabel(AnalysisRunStatus status) => status switch
    {
        AnalysisRunStatus.Queued => "待機中",
        AnalysisRunStatus.Running => "実行中",
        AnalysisRunStatus.Succeeded => "完了",
        AnalysisRunStatus.PartiallySucceeded => "完了（一部失敗）",
        AnalysisRunStatus.Failed => "失敗",
        AnalysisRunStatus.Cancelled => "キャンセル",
        _ => status.ToString(),
    };
}
