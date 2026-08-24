# Task Completion
- Confirm the change respects `AGENT.md`, especially no order execution/auto-fill behavior and no future-data leakage.
- Run from repository root: `dotnet restore .\\SwingAdviser.sln`, `dotnet build .\\SwingAdviser.sln`, `dotnet test .\\SwingAdviser.sln`; add focused tests for changed financial/persistence behavior.
- Inspect warnings, failures, logs, `git diff`, and `git status --short`; preserve unrelated user changes.
- Do not claim completion if build/tests fail. If a required check cannot run, report the exact skipped check and remaining risk.
- Do not run `dotnet format` unless an established repository workflow introduces it.
- Report changed behavior, verification results, and unresolved assumptions concisely.