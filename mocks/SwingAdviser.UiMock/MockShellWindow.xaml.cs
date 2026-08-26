using System.Windows;
using MahApps.Metro.Controls;
using Prism.Ioc;
using SwingAdviser.Domain.Common;
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

        // 候補からの登録は新規建（Open）、保有からの登録は決済（Close）を初期選択する。
        // どちらも銘柄コード/銘柄名/Long-Shortだけを渡し、価格・日時・株数・充当lotは利用者が入力する。
        viewModel.CandidateList.ManualEntryRequested += (_, e) => OpenManualEntry(e, ExecutionKind.Open);
        viewModel.PositionList.ManualEntryRequested += (_, e) => OpenManualEntry(e, ExecutionKind.Close);
    }

    private void OpenManualEntry(ManualEntryRequestedEventArgs e, ExecutionKind initialKind)
    {
        var viewModel = ContainerLocator.Container.Resolve<ManualExecutionEntryViewModel>();
        viewModel.Prefill(e.Code, e.Name, e.Side, initialKind);

        var window = new ManualExecutionEntryWindow(viewModel)
        {
            Owner = this,
        };
        window.ShowDialog();
    }
}
