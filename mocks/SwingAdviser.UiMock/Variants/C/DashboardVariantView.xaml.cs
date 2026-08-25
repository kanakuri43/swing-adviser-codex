using System.Windows;
using System.Windows.Controls;
using SwingAdviser.UiMock;

namespace SwingAdviser.UiMock.Variants.C;

public partial class DashboardVariantView : UserControl
{
    public DashboardVariantView()
    {
        InitializeComponent();
    }

    private void RunAiCheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MockShellWindowViewModel shell)
        {
            shell.CandidateList.RunAiCheckCommand.Execute(CandidatesList.SelectedItems);
        }
    }
}
