using Prism.Mvvm;

namespace SwingAdviser.Presentation;

public sealed class MainWindowViewModel : BindableBase
{
    public string Title => "Swing Adviser";

    public string StatusMessage => "アプリケーション基盤を準備しました。";
}
