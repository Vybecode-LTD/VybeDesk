using System.Reflection;
using System.Runtime.Loader;

namespace VybeDesk.Services.Plugins;

/// <summary>
/// A collectible <see cref="AssemblyLoadContext"/> for one plugin. The plugin's
/// own assemblies load here (isolated, and—because the context is
/// collectible—unloadable), while <b>shared contract assemblies defer to the
/// host's already-loaded copies</b>.
///
/// That deferral is the crux of the whole design: a plugin's
/// <c>IVybeModule</c>, its <c>PageViewModel</c>s, and its Avalonia controls
/// must be the SAME <see cref="Type"/> identities the host knows. If the plugin
/// loaded its own copy of <c>VybeDesk.Plugin.Abstractions</c> or <c>Avalonia</c>
/// into this context, an <c>is IVybeModule</c> check would fail and its
/// controls couldn't enter the host visual tree. Returning <c>null</c> from
/// <see cref="Load"/> for those assemblies routes them to the default context
/// (the host), guaranteeing one shared copy.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string mainAssemblyPath)
        : base(name: Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Host-shared contracts → defer to the default (host) context.
        if (IsHostShared(assemblyName.Name)) return null;

        // Plugin-private dependency → resolve + load into this context.
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }

    /// <summary>
    /// The contract + framework assemblies that MUST be a single shared copy
    /// across the host/plugin boundary. Everything else (the plugin's own deps)
    /// loads isolated.
    /// </summary>
    private static bool IsHostShared(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name is "VybeDesk.Plugin.Abstractions" or "VybeDesk.Core"
            || name.StartsWith("Avalonia", StringComparison.Ordinal)
            || name.StartsWith("CommunityToolkit", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal);
    }
}
