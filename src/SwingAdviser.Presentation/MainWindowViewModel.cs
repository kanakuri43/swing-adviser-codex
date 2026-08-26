using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Presentation.TradingWorkspace;

namespace SwingAdviser.Presentation;

public sealed class MainWindowViewModel : BindableBase
{
    private readonly TradingWorkspaceService _service;
    private bool _isBusy;
    private string _statusMessage = "ローカルSQLiteを読み込んでいます。";
    private string? _errorMessage;
    private DateTimeOffset? _lastLoadedAtUtc;

    public MainWindowViewModel(TradingWorkspaceService service, TradingWorkspaceEnvironment environment)
    {
        _service = service;
        DatabaseNotice = environment.Notice;
        ReloadCommand = new DelegateCommand(async () => await ReloadAsync(), () => !IsBusy);
        RequestCandidateEntryCommand = new DelegateCommand<CandidateRowViewModel>(
            row => RequestManualEntry(row),
            row => row is not null);
        RequestPositionExitCommand = new DelegateCommand<PositionRowViewModel>(
            row => RequestManualEntry(row),
            row => row?.IsExitActionable == true);
        RequestCorrectionCommand = new DelegateCommand<ExecutionRowViewModel>(
            row => RequestCorrection(row),
            row => row is not null);
    }

    public string Title => "Swing Adviser — 日本株スイング判断支援";
    public string ScoreCaption => TradingDisplayLabels.ScoreCaption;
    public string SafetyNotice => "分析結果は参考情報です。注文・自動売買は行いません。約定は証券会社の通知を確認し、利用者が入力・確認した内容だけを保存します。";
    public string DatabaseNotice { get; }
    public ObservableCollection<CandidateRowViewModel> Candidates { get; } = [];
    public ObservableCollection<PositionRowViewModel> Positions { get; } = [];
    public ObservableCollection<ExecutionRowViewModel> Executions { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ReloadCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string LastLoadedText => _lastLoadedAtUtc is null
        ? "未読込"
        : $"最終読込: {_lastLoadedAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

    public DelegateCommand ReloadCommand { get; }
    public DelegateCommand<CandidateRowViewModel> RequestCandidateEntryCommand { get; }
    public DelegateCommand<PositionRowViewModel> RequestPositionExitCommand { get; }
    public DelegateCommand<ExecutionRowViewModel> RequestCorrectionCommand { get; }

    public event EventHandler<ManualExecutionDialogRequestEventArgs>? ManualExecutionDialogRequested;

    public ManualExecutionEntryViewModel CreateManualExecutionEntry(
        ManualExecutionDialogRequestEventArgs request) => new(_service, request);

    public async Task ReloadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "ローカルSQLiteから一覧を読み込んでいます…";
        try
        {
            var snapshot = await _service.LoadAsync();
            Replace(Candidates, snapshot.Candidates.Select(x => new CandidateRowViewModel(x)));
            Replace(Positions, snapshot.Positions.Select(x => new PositionRowViewModel(x)));
            Replace(Executions, snapshot.Executions.Select(x => new ExecutionRowViewModel(x)));
            _lastLoadedAtUtc = snapshot.LoadedAtUtc;
            RaisePropertyChanged(nameof(LastLoadedText));
            StatusMessage = $"候補 {Candidates.Count}件 / 保有 {Positions.Count}件 / 約定 {Executions.Count}件";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "読込をキャンセルしました。";
        }
        catch (Exception exception)
        {
            ErrorMessage = $"一覧を読み込めませんでした: {exception.Message}";
            StatusMessage = "読込失敗";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task NotifyDialogCompletedAsync() => await ReloadAsync();

    private void RequestManualEntry(CandidateRowViewModel? row)
    {
        if (row is not null)
        {
            ManualExecutionDialogRequested?.Invoke(this, ManualExecutionDialogRequestEventArgs.ForCandidate(row.Item));
        }
    }

    private void RequestManualEntry(PositionRowViewModel? row)
    {
        if (row?.IsExitActionable == true)
        {
            ManualExecutionDialogRequested?.Invoke(this, ManualExecutionDialogRequestEventArgs.ForPosition(row.Item));
        }
    }

    private void RequestCorrection(ExecutionRowViewModel? row)
    {
        if (row is not null)
        {
            ManualExecutionDialogRequested?.Invoke(this, ManualExecutionDialogRequestEventArgs.ForCorrection(row.Item));
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
