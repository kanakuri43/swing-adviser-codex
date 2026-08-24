# Conventions
- Prefer existing dependencies/naming/design; check BCL and existing packages before proposing a new NuGet dependency.
- C#: file-scoped namespaces, 4-space indentation, PascalCase public symbols, sealed concrete classes where extension is not intended, async/await for I/O.
- Long operations must not block the UI thread; expose cancellation, progress, duplicate-run prevention, and failed-item counts.
- Keep business logic out of WPF code-behind; ViewModels must not directly access DB, HTTP, files, or Codex CLI.
- EF model configuration belongs in Infrastructure and is discovered with `ApplyConfigurationsFromAssembly`; DB table/column names use snake_case mappings.
- Never rewrite an existing migration; add a new migration. Favor append-only revisions/history and explicit unknown/missing states over overwrite, deletion, or inferred zero values.
- Environment-specific values and secrets belong in configuration; never commit API keys or log secrets.
- Tests use xUnit; use independent expected values for financial algorithms rather than recomputing expectations with production logic.