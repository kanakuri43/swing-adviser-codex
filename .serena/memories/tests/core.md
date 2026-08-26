# Tests
- Tests live under `tests/`: `SwingAdviser.Infrastructure.Tests` covers Domain/Application/Infrastructure integration, persistence, migrations, architecture, and trading workspace behavior; `SwingAdviser.Presentation.Tests` covers presentation-only behavior. Both use xUnit.
- Architecture contract tests enforce project-reference direction: Domain -> none, Application -> Domain, Infrastructure -> Application+Domain, Presentation -> Application, Desktop -> Infrastructure+Presentation; inner layers and Presentation also have forbidden technical dependency checks.
- Persistence tests use SQLite in-memory with an explicitly opened connection and apply real EF migrations.
- High-priority coverage: indicator/signal/risk boundaries; Long/Short and Entry/Exit; date/future-data boundaries; splits/corporate-action revisions; MarginLot deadlines/cost states; partial-exit allocation; no automatic trade history; AI fallback; repositories/migrations.
- Financial expected values must come from independent fixtures, known datasets, or hand calculations, not duplicated production algorithms.
