# Infrastructure Layer
- `src/SwingAdviser.Infrastructure`; references Application and Domain. Owns SQLite/EF persistence, market/HTTP data, files, and Codex CLI integration.
- `Persistence/SwingAdviserDbContext` applies entity configurations from its assembly; design-time factory uses SQLite and migration history table `__ef_migrations_history`.
- `AddBusinessSchema` is the first business migration after the intentionally empty `InitialCreate`; it defines the current 54-table schema plus constraints/triggers. Never rewrite either migration; add later schema changes as new migrations.
- Runtime DB is `Path.Combine(AppContext.BaseDirectory, "swing-adviser.db")`; startup fails explicitly if it is not writable and never falls back elsewhere. `--development-data` selects the separate `swing-adviser.development.db` and runs the idempotent `DevelopmentDataSeeder`.
- `SqliteTradingWorkspaceRepository` implements the current read/manual-execution boundary transactionally, including explicit close-lot allocation, reconciliation guards, and append-only correction revisions.
- Isolate DB access in repository/persistence code; no SQL in ViewModels. Batch/transaction large updates and distinguish HTTP/rate-limit/timeout/invalid-data/SQLite-lock/cancellation failures.
- Daily bars and corporate actions are revisioned append-only; freeze analysis input manifest/hash, data revisions, engine/schema/strategy versions, and full normalized parameter snapshots for reproducibility.
