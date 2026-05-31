namespace VybeDesk.Services.Plugins;

/// <summary>
/// Filesystem locations for plugins, rooted under the same
/// <c>%LOCALAPPDATA%\VybeDesk</c> directory the rest of the app (and the
/// uninstaller's "remove all user data") already uses.
/// </summary>
public static class PluginPaths
{
    /// <summary>
    /// <c>%LOCALAPPDATA%\VybeDesk\plugins</c> — one subfolder per installed
    /// plugin, each holding a <c>plugin.json</c> + the plugin's assembly.
    /// Created on access.
    /// </summary>
    public static string PluginsDirectory
    {
        get
        {
            var dir = Path.Combine(Paths.AppDataDir(), "plugins");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// <c>%LOCALAPPDATA%\VybeDesk\plugin-data\&lt;id&gt;</c> — a writable,
    /// per-plugin state directory handed out via <c>IModuleHost</c>. Created
    /// on access. The plugin id is sanitised for use as a folder name.
    /// </summary>
    public static string PluginDataDirectory(string pluginId)
    {
        var safe = pluginId;
        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        var dir = Path.Combine(Paths.AppDataDir(), "plugin-data", safe);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
