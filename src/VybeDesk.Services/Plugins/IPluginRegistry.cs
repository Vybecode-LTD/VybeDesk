namespace VybeDesk.Services.Plugins;

/// <summary>
/// Read model of what the loader discovered this session — every plugin folder
/// it found, whether it loaded, was disabled, was incompatible, or failed.
/// Registered as a singleton so the Plugins management UI can render it.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>All discovered plugins, in folder-enumeration order.</summary>
    IReadOnlyList<PluginInfo> Plugins { get; }

    /// <summary>The folder plugins are installed into.</summary>
    string PluginsDirectory { get; }
}
