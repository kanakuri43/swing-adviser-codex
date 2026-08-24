# Tests
- Tests live under `tests/`; current project `SwingAdviser.Infrastructure.Tests` references Infrastructure and uses xUnit.
- Persistence tests use SQLite in-memory with an explicitly opened connection and apply real EF migrations.
- High-priority coverage: indicator/signal/risk boundaries; Long/Short and Entry/Exit; date/future-data boundaries; splits/corporate-action revisions; MarginLot deadlines/cost states; partial-exit allocation; no automatic trade history; AI fallback; repositories/migrations.
- Financial expected values must come from independent fixtures, known datasets, or hand calculations, not duplicated production algorithms.