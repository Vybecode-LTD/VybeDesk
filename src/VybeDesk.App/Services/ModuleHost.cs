using System.Reflection;
using VybeDesk.Plugin;
using VybeDesk.Services.Plugins;

namespace VybeDesk.App.Services;

/// <summary>
/// Host-side implementation of <see cref="IModuleHost"/> — the small facade
/// plugins constructor-inject to learn their runtime environment without
/// referencing the host application.
/// </summary>
public sealed class ModuleHost : IModuleHost
{
    public Version HostVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public string GetPluginDataDirectory(string pluginId) =>
        PluginPaths.PluginDataDirectory(pluginId);
}
