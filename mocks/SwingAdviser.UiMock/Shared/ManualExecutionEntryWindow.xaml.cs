using System.Windows;
using MahApps.Metro.Controls;
using SwingAdviser.Domain.Common;
using SwingAdviser.UiMock.ViewModels;

namespace SwingAdviser.UiMock.Shared;

/// <summary>
/// UI専用の配線のみ（RadioButtonの選択とViewModelプロパティの同期、登録完了時にダイアログを閉じる）。
/// 業務ロジックはここに置かない。
/// </summary>
public partial class ManualExecutionEntryWindow : MetroWindow
{
    public ManualExecutionEntryWindow(ManualExecutionEntryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RegistrationCompleted += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void OpenKindRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ManualExecutionEntryViewModel vm)
        {
            vm.Kind = ExecutionKind.Open;
        }
    }

    private void CloseKindRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ManualExecutionEntryViewModel vm)
        {
            vm.Kind = ExecutionKind.Close;
        }
    }
}
