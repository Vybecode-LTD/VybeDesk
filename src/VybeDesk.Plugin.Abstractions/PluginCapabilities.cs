namespace VybeDesk.Plugin;

/// <summary>
/// Well-known capability identifiers a plugin may declare in its manifest so
/// the host can disclose them to the user at enable-time. These are advisory
/// transparency labels, NOT an enforced permission boundary — an in-process
/// .NET plugin runs with full host privileges regardless of what it declares.
/// See the trust model in <c>docs/PLUGINS.md</c>. The list is open: a plugin
/// may declare any string, but using these constants keeps the disclosure UI
/// consistent.
/// </summary>
public static class PluginCapabilities
{
    /// <summary>Reads or writes files outside its own plugin data directory.</summary>
    public const string FileSystem = "filesystem";

    /// <summary>Makes outbound network requests.</summary>
    public const string Network = "network";

    /// <summary>Reads or writes the system clipboard.</summary>
    public const string Clipboard = "clipboard";

    /// <summary>Calls the Anthropic API through the host's <c>IAiService</c>.</summary>
    public const string Ai = "ai";

    /// <summary>Launches external processes.</summary>
    public const string Process = "process";
}
