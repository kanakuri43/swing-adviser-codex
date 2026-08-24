using System.Windows;
using Microsoft.EntityFrameworkCore;
using Prism.DryIoc;
using Prism.Ioc;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Presentation;

namespace SwingAdviser.Desktop;

public partial class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        SwingAdviserSqliteDatabase.InitializeRuntimeDatabase();

        var dbContextOptions = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(
                SwingAdviserSqliteDatabase.CreateConnectionString(
                    SwingAdviserSqliteDatabase.RuntimeDatabasePath))
            .Options;

        containerRegistry.RegisterInstance(dbContextOptions);
        containerRegistry.Register<SwingAdviserDbContext>();
    }
}
