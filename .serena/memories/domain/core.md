# Domain Layer
- `src/SwingAdviser.Domain`; dependency-free domain model. Must not reference WPF, Prism, SQLite/EF, HTTP, files, or Codex CLI.
- Owns instruments, daily bars, corporate actions, positions, MarginLot/credit contract terms, credit-cost ledger, position adjustments, executions, strategies, signals, and analysis results.
- Preserve original user-entered executions as audit originals; corporate-action conversions are separate adjustment history.
- Model unknown/unpublished/missing separately from zero or not-applicable, especially margin costs and deadlines.