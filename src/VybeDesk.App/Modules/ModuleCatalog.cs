namespace VybeDesk.App.Modules;

/// <summary>
/// Immutable snapshot of the sidebar page set, assembled once at composition
/// time (see <c>Program.ConfigureServices</c>). The ordering decision lives in
/// the composition root, not here — this type just holds the result.
/// </summary>
public sealed class ModuleCatalog : IModuleCatalog
{
    public IReadOnlyList<PageViewModel> Pages { get; }

    public ModuleCatalog(IEnumerable<PageViewModel> pages) => Pages = pages.ToList();
}
