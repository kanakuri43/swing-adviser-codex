using System.Windows;
using MahApps.Metro.Controls;
using Prism.Ioc;
using SwingAdviser.UiMock.Shared;
using SwingAdviser.UiMock.ViewModels;

namespace SwingAdviser.UiMock;

/// <summary>
/// UI専用の配線のみ。サイズプリセットの適用と、手動約定登録ダイアログの起動を担当する。
/// </summary>
public partial class MockShellWindow : MetroWindow
{
    public MockShellWindow(MockShellWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.SizePresetRequested += (_, _) =>
        {
            Width = viewModel.RequestedWidth;
            Height = viewModel.RequestedHeight;
        };

        viewModel.CandidateList.ManualEntryRequested += OnManualEntryRequested;
    }

    private void OnManualEntryRequested(object? sender, ManualEntryRequestedEventArgs e)
    {
        var viewModel = ContainerLocator.Container.Resolve<ManualExecutionEntryViewModel>();
        viewModel.Prefill(e.Code, e.Name, e.Side);

        var window = new ManualExecutionEntryWindow(viewModel)
        {
            Owner = this,
        };
        window.ShowDialog();
    }
}
