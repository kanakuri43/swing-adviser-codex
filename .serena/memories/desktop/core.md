# Desktop Composition Root
- `src/SwingAdviser.Desktop` is the WPF `WinExe` and Prism composition root; it references Infrastructure and Presentation. Presentation is a library and is not directly runnable.
- `App.RegisterTypes` selects the SQLite file, verifies write access, applies EF migrations, registers `ITradingWorkspaceRepository`/`TradingWorkspaceService`, and seeds only when `--development-data` is present.
- Production and development data must remain isolated: default is the executable-directory `swing-adviser.db`; `--development-data` uses `swing-adviser.development.db` in the same directory.
