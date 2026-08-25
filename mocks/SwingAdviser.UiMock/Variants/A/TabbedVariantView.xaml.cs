using System.Windows;
using System.Windows.Controls;
using SwingAdviser.UiMock;

namespace SwingAdviser.UiMock.Variants.A;

/// <summary>
/// 案A タブ切替型。UI専用の配線のみ（DataGrid.SelectedItems は DependencyProperty ではなく
/// バインドしても更新されないため、クリック時にコードビハインドで読み取る）。
/// </summary>
public partial class TabbedVariantView : UserControl
{
    public TabbedVariantView()
    {
        InitializeComponent();
    }

    private void RunAiCheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MockShellWindowViewModel shell)
        {
            shell.CandidateList.RunAiCheckCommand.Execute(CandidatesGrid.SelectedItems);
        }
    }
}
