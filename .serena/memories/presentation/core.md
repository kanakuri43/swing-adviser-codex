# Presentation Layer
- `src/SwingAdviser.Presentation`; WPF net10.0-windows using Prism.DryIoc, MVVM, and MahApps.Metro. References Application and Infrastructure for composition.
- `App` is the Prism composition root; `MainWindow` uses ViewModelLocator and derives from MetroWindow.
- Code-behind contains UI-only concerns; business rules stay outside Presentation, and ViewModels do not call DB/HTTP/files/CLI directly.
- UI must make failures visible and must not imply guaranteed profits, instant fills, order submission, or automated trade-history creation.