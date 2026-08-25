using System.Windows;
using System.Windows.Controls;

namespace SwingAdviser.UiMock.Shared;

/// <summary>
/// Severity→色 の対応をここに一元化する。ラベル文字列は <see cref="MockLabels"/> が作るので、
/// このコントロールは色分けの見た目だけを担当する。
/// </summary>
public partial class SeverityBadge : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(SeverityBadge), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(MockSeverity), typeof(SeverityBadge), new PropertyMetadata(MockSeverity.Neutral));

    public SeverityBadge()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MockSeverity Severity
    {
        get => (MockSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }
}
