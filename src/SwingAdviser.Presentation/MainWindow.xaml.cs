using MahApps.Metro.Controls;
using SwingAdviser.Presentation.TradingWorkspace;

namespace SwingAdviser.Presentation;

public partial class MainWindow : MetroWindow
{
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_initialized || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        _initialized = true;
        viewModel.ManualExecutionDialogRequested += OnManualExecutionDialogRequested;
        Closed += (_, _) => viewModel.ManualExecutionDialogRequested -= OnManualExecutionDialogRequested;
        await viewModel.ReloadAsync();
    }

    private async void OnManualExecutionDialogRequested(
        object? sender,
        ManualExecutionDialogRequestEventArgs request)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var dialog = new ManualExecutionEntryWindow(viewModel.CreateManualExecutionEntry(request))
        {
            Owner = this,
        };
        if (dialog.ShowDialog() == true)
        {
            await viewModel.NotifyDialogCompletedAsync();
        }
    }
}
