# Project Core
- Windows desktop decision-support and recordkeeping app for Japanese equity margin swing trades; daily bars, holding horizon days to weeks.
- Never send brokerage orders, implement auto-trading, simulate instant fills, or derive trade history automatically from prices/signals/AI. Execution datetime, price, and quantity require explicit user entry/confirmation.
- Analysis must be point-in-time: never mix future prices, filings, news, or corporate actions into historical decisions. Preserve auditability and reproducibility over convenience.
- `AGENT.md` is authoritative when it conflicts with domain docs. Domain details live under `docs/`.
- Read `mem:tech_stack` for runtime/framework/package pins.
- Read `mem:suggested_commands` for Windows development and Serena commands; read `mem:task_completion` before declaring implementation work done.
- Read `mem:conventions` for repository-wide implementation rules.
- Layer responsibilities and dependency boundaries: `mem:domain/core`, `mem:application/core`, `mem:infrastructure/core`, `mem:presentation/core`; composition/runtime startup: `mem:desktop/core`; test strategy: `mem:tests/core`.
