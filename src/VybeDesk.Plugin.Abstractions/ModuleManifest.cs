namespace VybeDesk.Plugin;

/// <summary>
/// A module's self-description, returned by <see cref="IVybeModule.Manifest"/>.
/// Mirrors the descriptive fields of the on-disk <c>plugin.json</c>; the host
/// cross-checks the two and warns on a mismatch. Keep <see cref="Id"/> stable
/// across versions — it is the plugin's identity for enable/disable state and
/// for its private data directory.
/// </summary>
public sealed record ModuleManifest
{
    /// <summary>Stable unique id; reverse-DNS recommended (e.g. <c>com.acme.todo</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable display name shown in the Plugins UI.</summary>
    public required string Name { get; init; }

    /// <summary>Semantic version of this plugin build (e.g. <c>1.0.0</c>).</summary>
    public required string Version { get; init; }

    /// <summary>Author or organization.</summary>
    public string Author { get; init; } = "";

    /// <summary>One-line description shown in the Plugins UI.</summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Capabilities this plugin uses, declared for user disclosure (use the
    /// constants on <see cref="PluginCapabilities"/>). These are advisory
    /// labels shown at enable-time — the host does NOT and CANNOT sandbox an
    /// in-process plugin. See the trust model in <c>docs/PLUGINS.md</c>.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}
