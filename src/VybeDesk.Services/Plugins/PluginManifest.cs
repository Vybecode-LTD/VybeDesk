using System.Text.Json.Serialization;

namespace VybeDesk.Services.Plugins;

/// <summary>
/// The on-disk <c>plugin.json</c> schema. Carries everything the host needs to
/// list, validate, and gate a plugin <em>before</em> any of its code is loaded
/// (id, version, host-compat range, declared capabilities) plus the entry
/// point the loader uses to find the plugin's <c>IVybeModule</c>.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Manifest schema version. Currently <c>1</c>.</summary>
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 1;

    /// <summary>Stable unique id; reverse-DNS recommended (e.g. <c>com.acme.todo</c>).</summary>
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>Human-readable display name.</summary>
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>Plugin semantic version (e.g. <c>1.0.0</c>).</summary>
    [JsonPropertyName("version")] public string Version { get; set; } = "";

    /// <summary>Author or organization.</summary>
    [JsonPropertyName("author")] public string Author { get; set; } = "";

    /// <summary>One-line description.</summary>
    [JsonPropertyName("description")] public string Description { get; set; } = "";

    /// <summary>The plugin's main assembly file name, relative to its folder (e.g. <c>Acme.Todo.dll</c>).</summary>
    [JsonPropertyName("entryAssembly")] public string EntryAssembly { get; set; } = "";

    /// <summary>
    /// Optional fully-qualified type name of the <c>IVybeModule</c>
    /// implementation. When omitted, the loader scans the entry assembly for
    /// the first public, concrete <c>IVybeModule</c>.
    /// </summary>
    [JsonPropertyName("entryType")] public string? EntryType { get; set; }

    /// <summary>Minimum compatible host version, inclusive (e.g. <c>1.1.0</c>). Optional.</summary>
    [JsonPropertyName("minHostVersion")] public string? MinHostVersion { get; set; }

    /// <summary>Maximum compatible host version, inclusive. Optional.</summary>
    [JsonPropertyName("maxHostVersion")] public string? MaxHostVersion { get; set; }

    /// <summary>
    /// Declared capabilities, shown to the user at enable-time (see
    /// <c>PluginCapabilities</c>). Advisory disclosure only — NOT an enforced
    /// sandbox. See the trust model in <c>docs/PLUGINS.md</c>.
    /// </summary>
    [JsonPropertyName("capabilities")] public string[] Capabilities { get; set; } = Array.Empty<string>();
}
