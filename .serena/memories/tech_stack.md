# Tech Stack
- C# with nullable reference types and implicit usings enabled.
- .NET SDK pinned by `global.json` to 10.0.303 with latestPatch roll-forward; libraries/tests target net10.0, WPF targets net10.0-windows.
- WPF + MVVM; Prism.DryIoc 8.1.97; MahApps.Metro 2.4.11.
- SQLite 3 via EF Core Sqlite/Design 10.0.11; schema evolution uses EF Core Migrations.
- xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4, coverlet.collector 6.0.4.
- NuGet versions are centrally managed in `Directory.Packages.props`; project files omit versions.
- Windows/PowerShell is the supported development environment.