# Application Layer
- `src/SwingAdviser.Application`; references Domain only.
- Owns use-case orchestration. Target scope includes market-data update, universe scan/candidate extraction, held-position reevaluation, AI checks, and manual execution registration; do not infer that all are implemented.
- Currently implemented boundary: `TradingWorkspaceService`/`ITradingWorkspaceRepository` for candidate/position/execution reads, explicitly user-confirmed manual execution registration, exact explicit lot allocation on closes, and append-only execution correction requests. Indicator calculation, scanning/scoring, reevaluation, AI execution, and market-data update remain unimplemented.
- Keep infrastructure behind abstractions; use async/cancellation for I/O workflows and preserve explicit user confirmation boundaries for executions.
