# Application Layer
- `src/SwingAdviser.Application`; references Domain only.
- Owns use-case orchestration: market-data update, universe scan/candidate extraction, held-position reevaluation, AI checks, and manual execution registration.
- Keep infrastructure behind abstractions; use async/cancellation for I/O workflows and preserve explicit user confirmation boundaries for executions.