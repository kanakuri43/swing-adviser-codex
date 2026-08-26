# Presentation Layer
- `src/SwingAdviser.Presentation`; WPF net10.0-windows using Prism.DryIoc, MVVM, and MahApps.Metro. References only Application; architecture tests enforce no Infrastructure/DB/HTTP/CLI dependency.
- `MainWindow` uses ViewModelLocator and derives from MetroWindow. The adopted tabbed UI is connected through `TradingWorkspaceService` to candidate/position/execution lists and explicit manual entry/exit/correction dialogs; the former three-variant mock project has been removed.
- `App`/`PrismApplication` and dependency composition live in Desktop, not Presentation; read `mem:desktop/core` for startup and database selection.
- Code-behind contains UI-only concerns; business rules stay outside Presentation, and ViewModels do not call DB/HTTP/files/CLI directly.
- UI must make failures visible and must not imply guaranteed profits, instant fills, order submission, or automated trade-history creation.
