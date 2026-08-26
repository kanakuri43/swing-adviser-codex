using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;
using SwingAdviser.UiMock.Mock;

namespace SwingAdviser.UiMock.ViewModels;

/// <summary>
/// 保有一覧セクションのUI向けファサード。<see cref="CandidateListViewModel"/> と対になる形で、
/// 「保有行から手動約定登録（決済）を要求する」という画面遷移だけを追加で持つ
/// （AGENT.md: 一覧から登録画面へ渡してよいのは銘柄と方向のみ。Hold中は登録できない）。
/// </summary>
public sealed class PositionListViewModel : BindableBase
{
    private PositionRowViewModel? _selectedPosition;

    public PositionListViewModel(MockScenarioState state)
    {
        State = state;
        RequestManualEntryCommand = new DelegateCommand<PositionRowViewModel>(RequestManualEntry, row => row is not null && row.IsExitActionable);
    }

    public MockScenarioState State { get; }

    public ObservableCollection<PositionRowViewModel> Positions => State.Positions;

    public PositionRowViewModel? SelectedPosition
    {
        get => _selectedPosition;
        set => SetProperty(ref _selectedPosition, value);
    }

    public DelegateCommand<PositionRowViewModel> RequestManualEntryCommand { get; }

    public event EventHandler<ManualEntryRequestedEventArgs>? ManualEntryRequested;

    private void RequestManualEntry(PositionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        // AGENT.md Non-negotiable rules: 保有一覧から渡してよいのは銘柄コード/銘柄名とLong/Shortのみ。
        ManualEntryRequested?.Invoke(this, new ManualEntryRequestedEventArgs(row.Code, row.Name, row.Side));
    }
}
