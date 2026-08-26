using System.Windows;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Prism.DryIoc;
using Prism.Ioc;
using SwingAdviser.Application.TradingWorkspace;
using SwingAdviser.Infrastructure.Persistence;
using SwingAdviser.Infrastructure.TradingWorkspace;
using SwingAdviser.Presentation;

namespace SwingAdviser.Desktop;

public partial class App : PrismApplication
{
    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        var useDevelopmentData = Environment.GetCommandLineArgs()
            .Any(argument => argument.Equals("--development-data", StringComparison.OrdinalIgnoreCase));
        var databasePath = useDevelopmentData
            ? Path.Combine(AppContext.BaseDirectory, "swing-adviser.development.db")
            : SwingAdviserSqliteDatabase.RuntimeDatabasePath;
        SwingAdviserSqliteDatabase.InitializeDatabase(databasePath);

        var dbContextOptions = new DbContextOptionsBuilder<SwingAdviserDbContext>()
            .UseSwingAdviserSqlite(
                SwingAdviserSqliteDatabase.CreateConnectionString(
                    databasePath))
            .Options;

        using (var context = new SwingAdviserDbContext(dbContextOptions))
        {
            context.Database.Migrate();
        }

        if (useDevelopmentData)
        {
            Task.Run(() => DevelopmentDataSeeder.SeedAsync(dbContextOptions)).GetAwaiter().GetResult();
        }

        containerRegistry.RegisterInstance(dbContextOptions);
        containerRegistry.RegisterInstance(new TradingWorkspaceEnvironment(databasePath, useDevelopmentData));
        containerRegistry.Register<SwingAdviserDbContext>();
        containerRegistry.RegisterSingleton<ITradingWorkspaceRepository, SqliteTradingWorkspaceRepository>();
        containerRegistry.RegisterSingleton<TradingWorkspaceService>();
    }
}
