namespace VybeDesk.Services.Plugins;

/// <summary>Outcome of trying to discover + load one plugin folder.</summary>
public enum PluginStatus
{
    /// <summary>Loaded into its ALC and its services/pages were registered.</summary>
    Loaded,

    /// <summary>Found and valid, but the user has disabled it (not loaded this session).</summary>
    Disabled,

    /// <summary>Its manifest's host-version range excludes the running host.</summary>
    Incompatible,

    /// <summary>Discovery or load failed (bad manifest, missing assembly, no module, threw).</summary>
    Failed,
}

/// <summary>
/// A read-only record of one discovered plugin, surfaced to the Plugins UI.
/// Captures success, disablement, incompatibility, and failure alike so the
/// user can see <em>why</em> a plugin isn't running.
/// </summary>
public sealed class PluginInfo
{
    /// <summary>Plugin id (from the manifest; falls back to the folder name).</summary>
    public required string Id { get; init; }

    /// <summary>Absolute path to the plugin's folder.</summary>
    public required string Directory { get; init; }

    /// <summary>Parsed manifest, or <c>null</c> if it couldn't be read.</summary>
    public PluginManifest? Manifest { get; init; }

    /// <summary>Load outcome.</summary>
    public required PluginStatus Status { get; set; }

    /// <summary>Human-readable reason, set for <see cref="PluginStatus.Incompatible"/> / <see cref="PluginStatus.Failed"/>.</summary>
    public string? Error { get; set; }

    public string Name => Manifest?.Name is { Length: > 0 } n ? n : Id;
    public string Version => Manifest?.Version ?? "";
    public string Author => Manifest?.Author ?? "";
    public string Description => Manifest?.Description ?? "";
    public IReadOnlyList<string> Capabilities => Manifest?.Capabilities ?? Array.Empty<string>();
}
