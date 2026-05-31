namespace VybeDesk.Services.Plugins;

/// <summary>
/// Immutable result of a discovery+load pass. Built by <see cref="PluginLoader"/>
/// at composition time and registered as the <see cref="IPluginRegistry"/>
/// singleton.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
{
    public IReadOnlyList<PluginInfo> Plugins { get; }
    public string PluginsDirectory { get; }

    public PluginRegistry(IReadOnlyList<PluginInfo> plugins, string pluginsDirectory)
    {
        Plugins = plugins;
        PluginsDirectory = pluginsDirectory;
    }
}
