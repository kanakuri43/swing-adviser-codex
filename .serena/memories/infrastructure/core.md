# Infrastructure Layer
- `src/SwingAdviser.Infrastructure`; references Application and Domain. Owns SQLite/EF persistence, market/HTTP data, files, and Codex CLI integration.
- `Persistence/SwingAdviserDbContext` applies entity configurations from its assembly; design-time factory uses SQLite and migration history table `__ef_migrations_history`.
- Isolate DB access in repository/persistence code; no SQL in ViewModels. Batch/transaction large updates and distinguish HTTP/rate-limit/timeout/invalid-data/SQLite-lock/cancellation failures.
- Daily bars and corporate actions are revisioned append-only; freeze analysis input manifest/hash, data revisions, engine/schema/strategy versions, and full normalized parameter snapshots for reproducibility.
- Existing `InitialCreate` migration is an intentionally empty foundation; add future business schema via new migrations.