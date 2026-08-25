using System.Collections;
using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using SwingAdviser.Domain.Common;
using SwingAdviser.UiMock.Mock;

namespace SwingAdviser.UiMock.ViewModels;

public sealed class ManualEntryRequestedEventArgs : EventArgs
{
    public ManualEntryRequestedEventArgs(string code, string name, PositionSide side)
    {
        Code = code;
        Name = name;
        Side = side;
    }

    public string Code { get; }
    public string Name { get; }
    public PositionSide Side { get; }
}

/// <summary>
/// 候補一覧セクションのUI向けファサード。データと模擬実行は <see cref="MockScenarioState"/> が持ち、
/// ここは「候補行から手動約定登録を要求する」という画面遷移だけを追加で持つ
/// （AGENT.md: 候補一覧から登録画面へ渡してよいのは銘柄と方向のみ）。
/// </summary>
public sealed class CandidateListViewModel : BindableBase
{
    private CandidateRowViewModel? _selectedCandidate;

    public CandidateListViewModel(MockScenarioState state)
    {
        State = state;
        RequestManualEntryCommand = new DelegateCommand<CandidateRowViewModel>(RequestManualEntry, row => row is not null);
    }

    public MockScenarioState State { get; }

    public ObservableCollection<CandidateRowViewModel> Candidates => State.Candidates;

    public ObservableCollection<ExclusionRowViewModel> Exclusions => State.Exclusions;

    public CandidateRowViewModel? SelectedCandidate
    {
        get => _selectedCandidate;
        set => SetProperty(ref _selectedCandidate, value);
    }

    public DelegateCommand<CandidateRowViewModel> RequestManualEntryCommand { get; }

    public DelegateCommand<IList> RunAiCheckCommand => State.RunAiCheckCommand;

    public DelegateCommand<CandidateRowViewModel> CancelQueuedCommand => State.CancelQueuedCommand;

    public DelegateCommand<CandidateRowViewModel> RetryCommand => State.RetryCommand;

    public event EventHandler<ManualEntryRequestedEventArgs>? ManualEntryRequested;

    private void RequestManualEntry(CandidateRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        // AGENT.md Non-negotiable rules: 候補一覧から渡してよいのは銘柄コード/銘柄名とLong/Shortのみ。
        ManualEntryRequested?.Invoke(this, new ManualEntryRequestedEventArgs(row.Code, row.Name, row.Side));
    }
}
