using Prism.Commands;
using Prism.Mvvm;
using SwingAdviser.UiMock.Mock;
using SwingAdviser.UiMock.ViewModels;

namespace SwingAdviser.UiMock;

public enum MockVariant
{
    A,
    B,
    C,
}

/// <summary>
/// シェルの状態。3案が共有する <see cref="MockScenarioState"/> と、選択中の案・セクション・
/// ウィンドウサイズだけを保持する。案ごとのViewModelは無く、各Variant Viewはこれを
/// DataContextとしてそのまま継承する。
/// </summary>
public sealed class MockShellWindowViewModel : BindableBase
{
    private MockVariant _selectedVariant = MockVariant.A;
    private bool _isOverlayVisible = true;
    private double _requestedWidth = 1440;
    private double _requestedHeight = 900;

    public MockShellWindowViewModel(
        MockScenarioState state,
        CandidateListViewModel candidateList,
        PositionListViewModel positionList,
        TradeHistoryViewModel tradeHistory)
    {
        State = state;
        CandidateList = candidateList;
        PositionList = positionList;
        TradeHistory = tradeHistory;

        SelectVariantCommand = new DelegateCommand<string>(key =>
        {
            SelectedVariant = Enum.Parse<MockVariant>(key);
            IsOverlayVisible = false;
        });

        SelectSectionCommand = new DelegateCommand<string>(key => State.SelectedSection = Enum.Parse<MockSection>(key));

        ApplySizePresetCommand = new DelegateCommand<string>(ApplySizePreset);
    }

    public MockScenarioState State { get; }

    public CandidateListViewModel CandidateList { get; }

    public PositionListViewModel PositionList { get; }

    public TradeHistoryViewModel TradeHistory { get; }

    public MockVariant SelectedVariant
    {
        get => _selectedVariant;
        set
        {
            if (SetProperty(ref _selectedVariant, value))
            {
                RaisePropertyChanged(nameof(VariantDisplayName));
                RaisePropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        set => SetProperty(ref _isOverlayVisible, value);
    }

    public string VariantDisplayName => SelectedVariant switch
    {
        MockVariant.A => "案A タブ切替型",
        MockVariant.B => "案B マスタ詳細2ペイン型",
        MockVariant.C => "案C ダッシュボード型",
        _ => SelectedVariant.ToString(),
    };

    public string WindowTitle => $"UIモック — {VariantDisplayName}";

    public double RequestedWidth
    {
        get => _requestedWidth;
        private set => SetProperty(ref _requestedWidth, value);
    }

    public double RequestedHeight
    {
        get => _requestedHeight;
        private set => SetProperty(ref _requestedHeight, value);
    }

    public DelegateCommand<string> SelectVariantCommand { get; }

    public DelegateCommand<string> SelectSectionCommand { get; }

    public DelegateCommand<string> ApplySizePresetCommand { get; }

    public event EventHandler? SizePresetRequested;

    private void ApplySizePreset(string preset)
    {
        (RequestedWidth, RequestedHeight) = preset switch
        {
            "min" => (1100d, 700d),
            _ => (1440d, 900d),
        };
        SizePresetRequested?.Invoke(this, EventArgs.Empty);
    }
}
