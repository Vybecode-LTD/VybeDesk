using CommunityToolkit.Mvvm.ComponentModel;

namespace VybeDesk.Plugin;

/// <summary>
/// Root base class for every VybeDesk view model. Provides
/// <see cref="ObservableObject"/> change-notification plumbing. Plugin
/// authors normally derive from <see cref="PageViewModel"/> (a sidebar page)
/// or <see cref="ProjectScopedViewModel"/> rather than this directly.
/// </summary>
public abstract class ViewModelBase : ObservableObject;
