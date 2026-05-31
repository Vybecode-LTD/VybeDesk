// The plugin SDK base view models (ViewModelBase, PageViewModel,
// ProjectScopedViewModel) live in the VybeDesk.Plugin.Abstractions assembly
// under the VybeDesk.Plugin namespace. Every concrete view model in this app
// derives from them, so a single global using keeps those derivations and the
// MainWindowViewModel shell free of per-file using noise.
global using VybeDesk.Plugin;
