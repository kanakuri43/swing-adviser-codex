using System.Collections.ObjectModel;
using Prism.Mvvm;
using SwingAdviser.UiMock.Mock;

namespace SwingAdviser.UiMock.ViewModels;

public sealed class PositionListViewModel : BindableBase
{
    private PositionRowViewModel? _selectedPosition;

    public PositionListViewModel(MockScenarioState state)
    {
        State = state;
    }

    public MockScenarioState State { get; }

    public ObservableCollection<PositionRowViewModel> Positions => State.Positions;

    public PositionRowViewModel? SelectedPosition
    {
        get => _selectedPosition;
        set => SetProperty(ref _selectedPosition, value);
    }
}
