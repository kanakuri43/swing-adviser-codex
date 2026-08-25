using System.Collections.ObjectModel;
using Prism.Mvvm;
using SwingAdviser.UiMock.Mock;

namespace SwingAdviser.UiMock.ViewModels;

public sealed class TradeHistoryViewModel : BindableBase
{
    private TradeExecutionRowViewModel? _selectedExecution;

    public TradeHistoryViewModel(MockScenarioState state)
    {
        State = state;
    }

    public MockScenarioState State { get; }

    public ObservableCollection<TradeExecutionRowViewModel> Executions => State.Executions;

    public TradeExecutionRowViewModel? SelectedExecution
    {
        get => _selectedExecution;
        set => SetProperty(ref _selectedExecution, value);
    }
}
