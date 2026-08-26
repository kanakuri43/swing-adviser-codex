using System.Collections.ObjectModel;
using Prism.Mvvm;
using SwingAdviser.Domain.Common;
using SwingAdviser.UiMock.Mock;
using SwingAdviser.UiMock.Shared;

namespace SwingAdviser.UiMock.ViewModels;

/// <summary>
/// 候補一覧の1行。AI状態だけが利用者操作で遷移しうるので可変にしてある。
/// このクラスは整形済み文字列と <see cref="MockSeverity"/> だけを公開し、生のenumは公開しない
/// （3案どのXAMLも同じラベル規則しか参照できないようにするための境界）。
/// </summary>
public sealed class CandidateRowViewModel : BindableBase
{
    private readonly ObservableCollection<string> _attemptLog = new();
    private MockAiState _aiState;
    private AiVerdict? _aiVerdict;
    private DateOnly? _aiStaleEvaluationBarDate;
    private string? _aiFailureDetail;
    private bool _isSelected;

    public CandidateRowViewModel(MockCandidateSeed seed)
    {
        Seed = seed;
        _aiState = seed.AiState;
        _aiVerdict = seed.AiVerdict;
        _aiStaleEvaluationBarDate = seed.AiStaleEvaluationBarDate;
        _aiFailureDetail = seed.AiFailureDetail;

        var (initialText, _) = MockLabels.AiStateLabel(_aiState, _aiStaleEvaluationBarDate, _aiFailureDetail);
        _attemptLog.Add($"初期状態: {initialText}");
    }

    public MockCandidateSeed Seed { get; }

    public string Code => Seed.Code;
    public string Name => Seed.Name;
    public PositionSide Side => Seed.Side;
    public string SideLabel => MockLabels.PositionSideLabel(Seed.Side);
    public int Score => Seed.Score;
    public string ConfidenceLabel => MockLabels.ConfidenceLabel(Seed.Confidence);
    public string EvaluationBarDateText => MockLabels.EvaluationBarDateText(Seed.EvaluationBarDate);
    public string EvaluationBarDateLabel => MockLabels.EvaluationBarDateLabel(Seed.EvaluationBarDate);
    public string AnalyzedAtText => MockLabels.AnalyzedAtText(Seed.AnalyzedAtUtc);
    public string AnalyzedAtLabel => MockLabels.AnalyzedAtLabel(Seed.AnalyzedAtUtc);
    public string StrategyLabel => Seed.StrategyLabel;
    public string PrimaryReason => Seed.PrimaryReason;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public MockAiState AiState => _aiState;

    public string AiStateText => MockLabels.AiStateLabel(_aiState, _aiStaleEvaluationBarDate, _aiFailureDetail).Text;

    public MockSeverity AiStateSeverity => MockLabels.AiStateLabel(_aiState, _aiStaleEvaluationBarDate, _aiFailureDetail).Severity;

    public string AiAlignmentText => MockLabels.AiVerdictAlignmentLabel(Seed.Side, _aiVerdict);

    public (string Text, MockSeverity Severity) ShortAvailability => MockLabels.ShortAvailabilityLabel(Seed.ShortOpenStatus, Seed.ShortRestrictionNote);

    public string ShortAvailabilityText => ShortAvailability.Text;

    public MockSeverity ShortAvailabilitySeverity => ShortAvailability.Severity;

    public bool IsShortCandidate => Seed.Side == PositionSide.Short;

    /// <summary>待機中/実行中への遷移かどうか（重複投入ガードの判定に使う）。</summary>
    public bool IsInFlight => _aiState is MockAiState.Queued or MockAiState.Running;

    /// <summary>(再)実行を受け付けられる状態か。</summary>
    public bool CanRequestAiCheck => _aiState is MockAiState.NotRun or MockAiState.Failed or MockAiState.FailedInterrupted
        or MockAiState.TimedOut or MockAiState.InsufficientInformation or MockAiState.Cancelled or MockAiState.Stale;

    public void ApplyAiTransition(MockAiState state, AiVerdict? verdict = null, string? failureDetail = null, string? logLine = null)
    {
        _aiState = state;
        _aiVerdict = verdict ?? (state == MockAiState.Succeeded ? _aiVerdict : null);
        _aiFailureDetail = failureDetail;
        if (state != MockAiState.Stale)
        {
            _aiStaleEvaluationBarDate = null;
        }

        var (text, _) = MockLabels.AiStateLabel(state, _aiStaleEvaluationBarDate, failureDetail);
        _attemptLog.Add(logLine ?? $"{DateTime.Now:HH:mm:ss} {text}");

        RaisePropertyChanged(nameof(AiState));
        RaisePropertyChanged(nameof(AiStateText));
        RaisePropertyChanged(nameof(AiStateSeverity));
        RaisePropertyChanged(nameof(AiAlignmentText));
        RaisePropertyChanged(nameof(IsInFlight));
        RaisePropertyChanged(nameof(CanRequestAiCheck));
    }

    public IReadOnlyList<string> AttemptLog => _attemptLog;
}

public sealed class ExclusionRowViewModel
{
    public ExclusionRowViewModel(MockExclusionSeed seed)
    {
        Seed = seed;
    }

    public MockExclusionSeed Seed { get; }

    public string Code => Seed.Code;
    public string Name => Seed.Name;
    public string ReasonText => MockLabels.ExclusionReasonLabel(Seed.Outcome, Seed.BarCount, Seed.RequiredBarCount, Seed.Reason);
}

public sealed class MockCostLineRowViewModel
{
    public MockCostLineRowViewModel(MockCostLineSeed seed)
    {
        Seed = seed;
    }

    public MockCostLineSeed Seed { get; }

    public string Label => Seed.Label;
    public string DirectionLabel => Seed.Direction == CostDirection.Charge ? "支払" : "受取";
    public string AmountText => MockLabels.CostAmountLabel(Seed.AmountStatus, Seed.Amount, Seed.ValuationKind);
}

public sealed class PositionRowViewModel
{
    public PositionRowViewModel(MockPositionSeed seed)
    {
        Seed = seed;
        CostLines = seed.CostLines.Select(c => new MockCostLineRowViewModel(c)).ToList();
    }

    public MockPositionSeed Seed { get; }

    public string Code => Seed.Code;
    public string Name => Seed.Name;
    public PositionSide Side => Seed.Side;
    public string SideLabel => MockLabels.PositionSideLabel(Seed.Side);
    public long Quantity => Seed.Quantity;
    public string QuantityText => $"{Quantity:#,0}株";
    public string AppliedStrategy => Seed.AppliedStrategy;
    public string DecisionLabel => MockLabels.DecisionLabel(Seed.Decision);
    public MockSeverity DecisionSeverity => MockLabels.DecisionSeverity(Seed.Decision);

    /// <summary>Hold以外（利確/損切/決済候補）のときだけ、この保有からの手動約定登録を許可する。</summary>
    public bool IsExitActionable => Seed.Decision is ExitDecision.TakeProfit or ExitDecision.StopLoss or ExitDecision.Exit;

    public string DecisionReason => Seed.DecisionReason;
    public string EvaluationBarDateText => MockLabels.EvaluationBarDateText(Seed.EvaluationBarDate);
    public string EvaluationBarDateLabel => MockLabels.EvaluationBarDateLabel(Seed.EvaluationBarDate);

    public string FixedAtrText => $"固定ATR {Seed.FixedAtr:0.0}（基準日 {Seed.AtrReferenceBarDate:yyyy-MM-dd}、期間{Seed.AtrPeriod}日）";
    public string CurrentAtrReferenceText => Seed.CurrentAtrReferenceOnly is { } atr
        ? $"現在ATR {atr:0.0}（参考・固定ATRとは別）"
        : "現在ATR: 算出不可（要照合中）";
    public string StopMultiplierText => $"損切倍率 {Seed.StopMultiplier:0.0}";
    public string StopPriceText => MockLabels.FormatYen(Seed.StopPrice);
    public string TakeProfitPriceText => MockLabels.FormatYen(Seed.TakeProfitPrice);

    /// <summary>現在基準（分割等調整後）のエントリー時価格。企業アクション要照合中は算出しない。</summary>
    public string EntryPriceText => Seed.EntryBasisPrice is { } entry ? MockLabels.FormatYen(entry) : "算出不可（要照合中）";

    /// <summary>参考情報としての現在価格。約定価格として自動採用しない。</summary>
    public string CurrentPriceText => Seed.CurrentPrice is { } current ? MockLabels.FormatYen(current) : "算出不可（要照合中）";

    public string PartialExitText => MockLabels.PartialExitLabel(Seed.PartialExitStatus, Seed.PartialExitQuantity, Seed.PartialExitEffectiveFraction, Seed.PartialExitNote);

    public string ReconciliationStatusLabel => MockLabels.ReconciliationStatusLabel(Seed.ReconciliationStatus);
    public bool IsReconciliationRequired => Seed.ReconciliationStatus == ReconciliationStatus.Required;
    public string? CorporateActionNote => Seed.CorporateActionNote;

    public (string Text, MockSeverity Severity) DeadlineState => MockLabels.DeadlineStateLabel(Seed.TermType, Seed.RemainingBusinessDays);
    public string DeadlineStateText => DeadlineState.Text;
    public MockSeverity DeadlineSeverity => DeadlineState.Severity;
    public string RemainingBusinessDaysText => MockLabels.RemainingBusinessDaysLabel(Seed.TermType, Seed.RemainingBusinessDays);
    public string? DeadlineChangeNote => Seed.DeadlineChangeNote;

    public IReadOnlyList<MockCostLineRowViewModel> CostLines { get; }

    public string PriceProfitAndLossText => MockLabels.ProfitLossLabel(Seed.PriceProfitAndLoss);
    public string ConfirmedCostProfitAndLossText => MockLabels.ProfitLossLabel(Seed.ConfirmedCostProfitAndLoss);
    public string EstimatedNetProfitAndLossText => MockLabels.ProfitLossLabel(Seed.EstimatedNetProfitAndLoss);
    public string CostToRRatioText => MockLabels.CostToRRatioLabel(Seed.CostToRRatio);
}

public sealed class TradeExecutionRevisionRowViewModel
{
    public TradeExecutionRevisionRowViewModel(MockExecutionRevisionSeed seed)
    {
        Seed = seed;
    }

    public MockExecutionRevisionSeed Seed { get; }

    public string RevisionLabel => $"rev{Seed.RevisionNumber}: {MockLabels.ExecutionChangeKindLabel(Seed.ChangeKind)}";
    public string ExecutedAtText => Seed.ExecutedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string PriceText => MockLabels.FormatYen(Seed.Price);
    public string QuantityText => $"{Seed.Quantity:#,0}株";
    public string UserConfirmedAtText => Seed.UserConfirmedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string? Note => Seed.Note;
    public bool IsVoid => Seed.ChangeKind == ExecutionChangeKind.Void;
}

public sealed class TradeExecutionRowViewModel
{
    public TradeExecutionRowViewModel(MockExecutionSeed seed)
    {
        Seed = seed;
        Revisions = seed.Revisions.Select(r => new TradeExecutionRevisionRowViewModel(r)).ToList();
    }

    public MockExecutionSeed Seed { get; }

    public string Code => Seed.Code;
    public string Name => Seed.Name;
    public string SideLabel => MockLabels.PositionSideLabel(Seed.Side);
    public string KindLabel => Seed.Kind == ExecutionKind.Open ? "新規建" : "決済";
    public string OriginLabel => "利用者手入力（確認済み）";
    public string? LotAllocationNote => Seed.LotAllocationNote;

    public IReadOnlyList<TradeExecutionRevisionRowViewModel> Revisions { get; }

    public TradeExecutionRevisionRowViewModel CurrentRevision => Revisions[^1];

    public bool HasCorrections => Revisions.Count > 1;
}
