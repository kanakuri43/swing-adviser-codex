using System.Windows;
using Prism.DryIoc;
using Prism.Ioc;
using SwingAdviser.UiMock.Mock;
using SwingAdviser.UiMock.ViewModels;

namespace SwingAdviser.UiMock;

/// <summary>
/// UIモック専用の合成ルート。Infrastructure を参照しないため、SQLite/HTTP へは構造的に到達できない。
/// </summary>
public partial class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MockShellWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<MockScenarioState>();
        containerRegistry.RegisterSingleton<CandidateListViewModel>();
        containerRegistry.RegisterSingleton<PositionListViewModel>();
        containerRegistry.RegisterSingleton<TradeHistoryViewModel>();
    }
}
