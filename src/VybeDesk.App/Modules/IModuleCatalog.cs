namespace VybeDesk.App.Modules;

/// <summary>
/// The ordered set of sidebar pages the shell renders: the built-in modules in
/// their curated order, then any pages contributed by loaded plugins, then
/// Settings pinned last. <see cref="MainWindowViewModel"/> consumes this rather
/// than a hard-coded constructor list, so a plugin extends the sidebar through
/// exactly the same path the built-in modules use.
/// </summary>
public interface IModuleCatalog
{
    /// <summary>Sidebar pages in display order. Never empty (Settings is always present).</summary>
    IReadOnlyList<PageViewModel> Pages { get; }
}
