using System.Windows;
using System.Windows.Controls;
using SwingAdviser.UiMock;

namespace SwingAdviser.UiMock.Variants.B;

public partial class MasterDetailVariantView : UserControl
{
    public MasterDetailVariantView()
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
