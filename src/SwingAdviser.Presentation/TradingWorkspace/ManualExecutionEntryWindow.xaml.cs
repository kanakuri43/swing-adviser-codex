using MahApps.Metro.Controls;

namespace SwingAdviser.Presentation.TradingWorkspace;

public partial class ManualExecutionEntryWindow : MetroWindow
{
    public ManualExecutionEntryWindow(ManualExecutionEntryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += OnSaved;
        Closed += (_, _) => viewModel.Saved -= OnSaved;
    }

    private void OnSaved(object? sender, EventArgs eventArgs)
    {
        DialogResult = true;
        Close();
    }
}
