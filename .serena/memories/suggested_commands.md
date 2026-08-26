# Suggested Commands (PowerShell, repository root)
- Restore: `dotnet restore .\\SwingAdviser.sln`
- Build: `dotnet build .\\SwingAdviser.sln`
- Test all: `dotnet test .\\SwingAdviser.sln`
- Run desktop app with the real local DB: `dotnet run --project .\\src\\SwingAdviser.Desktop\\SwingAdviser.Desktop.csproj`
- Run against isolated seeded development data: `dotnet run --project .\\src\\SwingAdviser.Desktop\\SwingAdviser.Desktop.csproj -- --development-data`
- List files: `rg --files`; search: `rg -n "pattern" src tests docs` (preferred over grep/Get-ChildItem recursion).
- Inspect changes: `git status --short`; `git diff -- <path>` (tracked path is `TODO.md`, uppercase).
- Validate Serena memory references: `serena memories check` from the repository root.
- Do not run `dotnet format` unless the repository explicitly adopts it.
