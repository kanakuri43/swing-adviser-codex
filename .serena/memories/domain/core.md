# Domain Layer
- `src/SwingAdviser.Domain`; dependency-free domain model. Must not reference WPF, Prism, SQLite/EF, HTTP, files, or Codex CLI.
- Owns instruments, daily bars, corporate actions, positions, MarginLot/credit contract terms, credit-cost ledger, position adjustments, executions, strategies, signals, and analysis results.
- `TechnicalIndicatorEngine` calculates PIT-verified EMA/MACD/ATR/volume evidence and returns the run/manifest/instrument/date/hash identity; `CandidateScoringEngine` applies directional MACD, strict EMA stack, and side-specific volume gates, then produces immutable, split-invariant 0-100 score components from frozen typed parameters. Strategy snapshot hashes include strategy and algorithm versions as well as the typed parameter body.
- Preserve original user-entered executions as audit originals; corporate-action conversions are separate adjustment history.
- Model unknown/unpublished/missing separately from zero or not-applicable, especially margin costs and deadlines.
