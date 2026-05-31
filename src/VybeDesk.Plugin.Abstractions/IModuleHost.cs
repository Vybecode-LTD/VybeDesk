namespace VybeDesk.Plugin;

/// <summary>
/// Host facade handed to plugins via dependency injection so they can discover
/// their runtime environment without referencing the host application. The host
/// registers a singleton implementation; constructor-inject it into your view
/// models or services.
/// </summary>
public interface IModuleHost
{
    /// <summary>
    /// The running VybeDesk host version. Compare against your manifest's
    /// host-version range if you need runtime feature gating beyond the
    /// load-time compatibility check the host already performs.
    /// </summary>
    Version HostVersion { get; }

    /// <summary>
    /// Returns an existing, writable directory unique to the given plugin
    /// (<c>%LOCALAPPDATA%\VybeDesk\plugin-data\&lt;pluginId&gt;\</c>), creating
    /// it on first request. Use it for any state your plugin needs to persist;
    /// it survives plugin updates and is removed when the plugin is uninstalled.
    /// </summary>
    /// <param name="pluginId">The requesting plugin's <see cref="ModuleManifest.Id"/>.</param>
    string GetPluginDataDirectory(string pluginId);
}
