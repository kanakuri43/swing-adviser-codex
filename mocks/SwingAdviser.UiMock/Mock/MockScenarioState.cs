using System.Collections.ObjectModel;
using System.Windows.Threading;
using Prism.Commands;
using Prism.Mvvm;
using SwingAdviser.Domain.Common;
using SwingAdviser.UiMock.Shared;
using SwingAdviser.UiMock.ViewModels;

namespace SwingAdviser.UiMock.Mock;

public enum MockSection
{
    Candidates,
    Positions,
    History,
}

/// <summary>
/// 進捗表示は日次更新とAIキューを分離する（product-spec.md UI/UX 節）。
/// </summary>
public sealed class DailyUpdateProgressViewModel : BindableBase
{
    private int _completed;

    public int Total { get; private set; }
    public int Failed { get; private set; }
    public AnalysisRunStatus Status { get; private set; }
    public bool IsRunning { get; private set; }

    public int Completed
    {
        get => _completed;
        private set => SetProperty(ref _completed, value);
    }

    public string StatusText => IsRunning
        ? $"更新中 {Completed:#,0}/{Total:#,0}"
        : $"更新{MockLabels.AnalysisRunStatusLabel(Status)} {Completed:#,0}/{Total:#,0}（失敗 {Failed}件）";

    public void Load(MockUpdateProgressSeed seed)
    {
        Total = seed.Total;
        Failed = seed.Failed;
        Status = seed.Status;
        IsRunning = false;
        Completed = seed.Total;
        RaiseAll();
    }

    public void BeginRun()
    {
        IsRunning = true;
        Completed = 0;
        RaiseAll();
    }

    public bool Advance(int step, MockUpdateProgressSeed target)
    {
        Completed = Math.Min(Total, Completed + step);
        var done = Completed >= Total;
        if (done)
        {
            IsRunning = false;
            Failed = target.Failed;
            Status = target.Status;
        }

        RaiseAll();
        return done;
    }

    private void RaiseAll()
    {
        RaisePropertyChanged(nameof(Total));
        RaisePropertyChanged(nameof(Failed));
        RaisePropertyChanged(nameof(Status));
        RaisePropertyChanged(nameof(IsRunning));
        RaisePropertyChanged(nameof(StatusText));
    }
}

public sealed class AiQueueProgressViewModel : BindableBase
{
    public int Total { get; private set; }
    public int Running { get; private set; }
    public int Queued { get; private set; }
    public int Completed { get; private set; }
    public int Failed { get; private set; }

    public string StatusText => Total == 0
        ? "AIチェック: 対象なし"
        : $"AIチェック継続中 {Completed}/{Total}（実行中{Running}・待機中{Queued}・失敗{Failed}）";

    public void Load(MockAiQueueProgressSeed seed)
    {
        Total = seed.Total;
        Running = seed.Running;
        Queued = seed.Queued;
        Completed = seed.Completed;
        Failed = seed.Failed;
        RaiseAll();
    }

    public void AdjustDelta(int runningDelta, int queuedDelta, int completedDelta, int failedDelta)
    {
        Running = Math.Max(0, Running + runningDelta);
        Queued = Math.Max(0, Queued + queuedDelta);
        Completed = Math.Max(0, Completed + completedDelta);
        Failed = Math.Max(0, Failed + failedDelta);
        Total = Math.Max(Total, Running + Queued + Completed);
        RaiseAll();
    }

    private void RaiseAll()
    {
        RaisePropertyChanged(nameof(Total));
        RaisePropertyChanged(nameof(Running));
        RaisePropertyChanged(nameof(Queued));
        RaisePropertyChanged(nameof(Completed));
        RaisePropertyChanged(nameof(Failed));
        RaisePropertyChanged(nameof(StatusText));
    }
}

/// <summary>
/// 3案が共有する唯一の可変状態。シングルトンとして登録し、案を切り替えても
/// 選択行・進捗・シナリオが引き継がれるようにする。
/// 結果は全てシード行にスクリプト化されており、<see cref="Random"/> は使わない。
/// </summary>
public sealed class MockScenarioState : BindableBase
{
    private const int QueuedTicks = 2;
    private const int RunningTicks = 2;
    private const int UpdateStepPerTick = 240;

    private readonly DispatcherTimer _timer;
    private readonly Dictionary<CandidateRowViewModel, int> _inFlightTicks = new();
    private readonly Dictionary<CandidateRowViewModel, MockAiState> _inFlightPhase = new();
    private MockScenario _scenario = MockDataSet.Normal;
    private MockSection _selectedSection = MockSection.Candidates;
    private object? _selectedVariantContext;

    public MockScenarioState()
    {
        DailyUpdate = new DailyUpdateProgressViewModel();
        AiQueue = new AiQueueProgressViewModel();
        Candidates = new ObservableCollection<CandidateRowViewModel>();
        Exclusions = new ObservableCollection<ExclusionRowViewModel>();
        Positions = new ObservableCollection<PositionRowViewModel>();
        Executions = new ObservableCollection<TradeExecutionRowViewModel>();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += OnTick;

        SwitchScenarioCommand = new DelegateCommand<string>(key =>
        {
            var scenario = MockDataSet.All.First(s => s.Key == key);
            LoadScenario(scenario);
        });

        StartDailyUpdateCommand = new DelegateCommand(StartDailyUpdate, () => !DailyUpdate.IsRunning);
        CompleteNowCommand = new DelegateCommand(CompleteNow, () => DailyUpdate.IsRunning || _inFlightTicks.Count > 0);
        ResetCommand = new DelegateCommand(ResetScenario);

        RunAiCheckCommand = new DelegateCommand<System.Collections.IList>(RunAiCheck);
        CancelQueuedCommand = new DelegateCommand<CandidateRowViewModel>(CancelQueued, row => row is { AiState: MockAiState.Queued });
        // 「明示的な再試行」は失敗系だけでなく、情報不足・キャンセル・旧結果からも許可する
        // （product-spec.md: 「単件/複数選択、待機中キャンセル、明示的な再試行を可能にする」）。
        // CandidateRowViewModel.CanRequestAiCheck と対象状態を揃えること。
        RetryCommand = new DelegateCommand<CandidateRowViewModel>(Retry, row => row is not null && row.CanRequestAiCheck && row.AiState != MockAiState.NotRun);

        // コマンドを全て構築した後でロードする。LoadScenario は RefreshCommands 経由で
        // 上記コマンドの RaiseCanExecuteChanged を呼ぶため、これより前に呼ぶとNREになる。
        LoadScenario(MockDataSet.Normal);
    }

    public DailyUpdateProgressViewModel DailyUpdate { get; }

    public AiQueueProgressViewModel AiQueue { get; }

    public ObservableCollection<CandidateRowViewModel> Candidates { get; }

    public ObservableCollection<ExclusionRowViewModel> Exclusions { get; }

    public ObservableCollection<PositionRowViewModel> Positions { get; }

    public ObservableCollection<TradeExecutionRowViewModel> Executions { get; }

    public string? EmptyStateNote => _scenario.EmptyStateNote;

    public string ScenarioDisplayName => _scenario.DisplayName;

    // 案C ダッシュボード型のサマリタイル用の集計。スキャン結果が変わるのはシナリオロード時だけなので、
    // 都度計算で十分（キャッシュしない）。
    public int LongCandidateCount => Candidates.Count(c => c.Side == PositionSide.Long);

    public int ShortCandidateCount => Candidates.Count(c => c.Side == PositionSide.Short);

    public int PositionCount => Positions.Count;

    public int ReconciliationRequiredCount => Positions.Count(p => p.Seed.ReconciliationStatus == ReconciliationStatus.Required);

    // 「期限不明」はDeadlineStateLabelの都合上Warning severityを共有するが、期限接近とは別状態として
    // 数えなければならない（risk-management.md:50 「期限不明、期限変更、期限接近、期限超過を別状態にし」）。
    public int DeadlineApproachingCount => Positions.Count(p =>
        p.Seed.TermType != MarginTermType.Unknown
        && p.Seed.RemainingBusinessDays is not null
        && p.DeadlineSeverity is MockSeverity.Warning or MockSeverity.Caution);

    public string OverallStatusText => AiQueue.Total == 0
        ? $"分析{DailyUpdate.StatusText}"
        : $"分析{DailyUpdate.StatusText}・{AiQueue.StatusText}";

    public MockSection SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }

    /// <summary>案A/B/Cが自由に使ってよい汎用の選択状態スロット（明細表示中の候補行など）。</summary>
    public object? SelectedVariantContext
    {
        get => _selectedVariantContext;
        set => SetProperty(ref _selectedVariantContext, value);
    }

    public DelegateCommand<string> SwitchScenarioCommand { get; }
    public DelegateCommand StartDailyUpdateCommand { get; }
    public DelegateCommand CompleteNowCommand { get; }
    public DelegateCommand ResetCommand { get; }
    public DelegateCommand<System.Collections.IList> RunAiCheckCommand { get; }
    public DelegateCommand<CandidateRowViewModel> CancelQueuedCommand { get; }
    public DelegateCommand<CandidateRowViewModel> RetryCommand { get; }

    private void LoadScenario(MockScenario scenario)
    {
        _scenario = scenario;
        _timer.Stop();
        _inFlightTicks.Clear();
        _inFlightPhase.Clear();

        Candidates.Clear();
        foreach (var seed in scenario.Candidates)
        {
            Candidates.Add(new CandidateRowViewModel(seed));
        }

        Exclusions.Clear();
        foreach (var seed in scenario.Exclusions)
        {
            Exclusions.Add(new ExclusionRowViewModel(seed));
        }

        Positions.Clear();
        foreach (var seed in scenario.Positions)
        {
            Positions.Add(new PositionRowViewModel(seed));
        }

        Executions.Clear();
        foreach (var seed in scenario.Executions)
        {
            Executions.Add(new TradeExecutionRowViewModel(seed));
        }

        DailyUpdate.Load(scenario.UpdateProgress);
        AiQueue.Load(scenario.AiQueueProgress);

        RaisePropertyChanged(nameof(EmptyStateNote));
        RaisePropertyChanged(nameof(ScenarioDisplayName));
        RaisePropertyChanged(nameof(OverallStatusText));
        RaisePropertyChanged(nameof(LongCandidateCount));
        RaisePropertyChanged(nameof(ShortCandidateCount));
        RaisePropertyChanged(nameof(PositionCount));
        RaisePropertyChanged(nameof(ReconciliationRequiredCount));
        RaisePropertyChanged(nameof(DeadlineApproachingCount));
        RefreshCommands();
    }

    private void ResetScenario() => LoadScenario(_scenario);

    private void StartDailyUpdate()
    {
        DailyUpdate.BeginRun();
        RaisePropertyChanged(nameof(OverallStatusText));
        EnsureTimerRunning();
        RefreshCommands();
    }

    /// <summary>
    /// 候補選択に対してAIチェックを要求する。実行中/待機中の重複投入は無視する
    /// （product-spec.md: 「実行中または同一入力の重複投入を防ぐ」）。
    /// </summary>
    private void RunAiCheck(System.Collections.IList? selectedRows)
    {
        if (selectedRows is null)
        {
            return;
        }

        var newlyQueued = 0;
        foreach (var item in selectedRows.Cast<object>().OfType<CandidateRowViewModel>())
        {
            if (!item.CanRequestAiCheck)
            {
                continue;
            }

            item.ApplyAiTransition(MockAiState.Queued, logLine: $"{DateTime.Now:HH:mm:ss} 待機中（利用者要求）");
            _inFlightTicks[item] = QueuedTicks;
            _inFlightPhase[item] = MockAiState.Queued;
            newlyQueued++;
        }

        AiQueue.AdjustDelta(runningDelta: 0, queuedDelta: newlyQueued, completedDelta: 0, failedDelta: 0);
        RaisePropertyChanged(nameof(OverallStatusText));
        EnsureTimerRunning();
        RefreshCommands();
    }

    private void CancelQueued(CandidateRowViewModel? row)
    {
        if (row is not { AiState: MockAiState.Queued })
        {
            return;
        }

        _inFlightTicks.Remove(row);
        _inFlightPhase.Remove(row);
        row.ApplyAiTransition(MockAiState.Cancelled, failureDetail: "利用者が待機中にキャンセル", logLine: $"{DateTime.Now:HH:mm:ss} キャンセル（待機中）");
        AiQueue.AdjustDelta(runningDelta: 0, queuedDelta: -1, completedDelta: 1, failedDelta: 0);
        RaisePropertyChanged(nameof(OverallStatusText));
        RefreshCommands();
    }

    private void Retry(CandidateRowViewModel? row)
    {
        if (row is null || row.IsInFlight)
        {
            return;
        }

        row.ApplyAiTransition(MockAiState.Queued, logLine: $"{DateTime.Now:HH:mm:ss} 待機中（再試行、旧結果は上書きしない）");
        _inFlightTicks[row] = QueuedTicks;
        _inFlightPhase[row] = MockAiState.Queued;
        AiQueue.AdjustDelta(runningDelta: 0, queuedDelta: 1, completedDelta: 0, failedDelta: 0);
        RaisePropertyChanged(nameof(OverallStatusText));
        EnsureTimerRunning();
        RefreshCommands();
    }

    private void CompleteNow()
    {
        while (DailyUpdate.IsRunning)
        {
            DailyUpdate.Advance(DailyUpdate.Total, _scenario.UpdateProgress);
        }

        foreach (var row in _inFlightTicks.Keys.ToList())
        {
            ForceFinish(row);
        }

        RaisePropertyChanged(nameof(OverallStatusText));
        _timer.Stop();
        RefreshCommands();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (DailyUpdate.IsRunning)
        {
            DailyUpdate.Advance(UpdateStepPerTick, _scenario.UpdateProgress);
            RaisePropertyChanged(nameof(OverallStatusText));
        }

        foreach (var row in _inFlightTicks.Keys.ToList())
        {
            var remaining = _inFlightTicks[row] - 1;
            if (remaining > 0)
            {
                _inFlightTicks[row] = remaining;
                continue;
            }

            AdvancePhase(row);
        }

        if (!DailyUpdate.IsRunning && _inFlightTicks.Count == 0)
        {
            _timer.Stop();
        }

        RefreshCommands();
    }

    private void AdvancePhase(CandidateRowViewModel row)
    {
        var phase = _inFlightPhase[row];
        if (phase == MockAiState.Queued)
        {
            row.ApplyAiTransition(MockAiState.Running, logLine: $"{DateTime.Now:HH:mm:ss} 実行中");
            _inFlightPhase[row] = MockAiState.Running;
            _inFlightTicks[row] = RunningTicks;
            AiQueue.AdjustDelta(runningDelta: 1, queuedDelta: -1, completedDelta: 0, failedDelta: 0);
        }
        else
        {
            FinishRow(row);
        }
    }

    private void ForceFinish(CandidateRowViewModel row)
    {
        if (_inFlightPhase.TryGetValue(row, out var phase) && phase == MockAiState.Queued)
        {
            row.ApplyAiTransition(MockAiState.Running, logLine: $"{DateTime.Now:HH:mm:ss} 実行中（即時完了）");
            AiQueue.AdjustDelta(runningDelta: 1, queuedDelta: -1, completedDelta: 0, failedDelta: 0);
        }

        FinishRow(row);
    }

    private void FinishRow(CandidateRowViewModel row)
    {
        _inFlightTicks.Remove(row);
        _inFlightPhase.Remove(row);

        var (state, verdict) = ScriptedFinalOutcome(row);
        row.ApplyAiTransition(state, verdict, logLine: $"{DateTime.Now:HH:mm:ss} {MockLabels.AiStateLabel(state, null, null).Text}");
        AiQueue.AdjustDelta(runningDelta: -1, queuedDelta: 0, completedDelta: 1, failedDelta: state is MockAiState.Failed or MockAiState.FailedInterrupted or MockAiState.TimedOut ? 1 : 0);
        RaisePropertyChanged(nameof(OverallStatusText));
    }

    /// <summary>
    /// 利用者が対話的に実行/再試行したときの最終結果は、銘柄コードで決定論的に決める
    /// （Randomは使わない。同じ操作をすれば誰が試しても同じ結果になる）。
    /// </summary>
    private static (MockAiState State, AiVerdict? Verdict) ScriptedFinalOutcome(CandidateRowViewModel row) => row.Code switch
    {
        "8306" => (MockAiState.Succeeded, AiVerdict.Neutral),
        "2413" => (MockAiState.Succeeded, AiVerdict.Bearish),
        "4385" => (MockAiState.InsufficientInformation, null),
        "4063" => (MockAiState.Succeeded, AiVerdict.Bullish),
        "6501" => (MockAiState.Succeeded, AiVerdict.Bullish),
        "7267" => (MockAiState.Succeeded, AiVerdict.Bullish),
        "6902" => (MockAiState.Succeeded, AiVerdict.Neutral),
        "4568" => (MockAiState.Succeeded, AiVerdict.Bullish),
        "9984" => (MockAiState.Succeeded, AiVerdict.Neutral),
        "6098" => (MockAiState.Succeeded, AiVerdict.Bullish),
        _ => (MockAiState.Succeeded, AiVerdict.Neutral),
    };

    private void EnsureTimerRunning()
    {
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void RefreshCommands()
    {
        StartDailyUpdateCommand.RaiseCanExecuteChanged();
        CompleteNowCommand.RaiseCanExecuteChanged();
        CancelQueuedCommand.RaiseCanExecuteChanged();
        RetryCommand.RaiseCanExecuteChanged();
    }
}
