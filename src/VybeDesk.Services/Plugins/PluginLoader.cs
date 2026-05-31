using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VybeDesk.Plugin;

namespace VybeDesk.Services.Plugins;

/// <summary>
/// Composition-time plugin discovery + loading. Runs once, from
/// <c>Program.ConfigureServices</c>, BEFORE the DI provider is built — so it
/// is constructed directly (not resolved from DI). For every enabled,
/// host-compatible plugin folder it: loads the entry assembly into a collectible
/// <see cref="PluginLoadContext"/>, finds the <see cref="IVybeModule"/>, calls
/// its <see cref="IVybeModule.ConfigureServices"/>, and registers the module
/// instance so the module catalog can later collect its pages.
/// </summary>
public sealed class PluginLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly Version _hostVersion;
    private readonly ISet<string> _disabledIds;
    private readonly string? _explicitRoot;

    // Roots the load contexts for the app lifetime. (Registering each module as
    // a DI singleton already keeps its ALC alive, but holding them here makes
    // the intent explicit and gives a future unload path a handle.)
    private readonly List<PluginLoadContext> _contexts = new();

    /// <param name="pluginsDirectory">
    /// Override the plugins root (defaults to <see cref="PluginPaths.PluginsDirectory"/>).
    /// Tests pass a temp directory; production passes null.
    /// </param>
    public PluginLoader(Version hostVersion, IEnumerable<string>? disabledIds = null, string? pluginsDirectory = null)
    {
        _hostVersion = hostVersion;
        _disabledIds = new HashSet<string>(disabledIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _explicitRoot = pluginsDirectory;
    }

    /// <summary>
    /// Discover every plugin folder, load the enabled+compatible ones into
    /// <paramref name="services"/>, and return a registry describing the
    /// outcome of each (for the Plugins UI).
    /// </summary>
    public PluginRegistry LoadInto(IServiceCollection services)
    {
        var root = _explicitRoot ?? PluginPaths.PluginsDirectory;
        Directory.CreateDirectory(root);
        var infos = new List<PluginInfo>();

        foreach (var folder in Directory.EnumerateDirectories(root).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var info = TryLoad(folder, services);
            if (info is not null) infos.Add(info);
        }

        return new PluginRegistry(infos, root);
    }

    private PluginInfo? TryLoad(string folder, IServiceCollection services)
    {
        var manifestPath = Path.Combine(folder, "plugin.json");
        if (!File.Exists(manifestPath)) return null; // not a plugin folder — skip silently

        var folderName = Path.GetFileName(folder);

        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), JsonOpts);
        }
        catch (Exception ex)
        {
            return Failed(folder, folderName, null, $"Invalid plugin.json: {ex.Message}");
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
            return Failed(folder, folderName, manifest, "plugin.json is missing the required \"id\" field.");

        var id = manifest.Id;

        if (_disabledIds.Contains(id))
            return new PluginInfo { Id = id, Directory = folder, Manifest = manifest, Status = PluginStatus.Disabled };

        if (!IsHostCompatible(manifest, out var why))
            return new PluginInfo { Id = id, Directory = folder, Manifest = manifest, Status = PluginStatus.Incompatible, Error = why };

        try
        {
            if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
                return Failed(folder, id, manifest, "plugin.json is missing \"entryAssembly\".");

            var asmPath = Path.Combine(folder, manifest.EntryAssembly);
            if (!File.Exists(asmPath))
                return Failed(folder, id, manifest, $"Entry assembly not found: {manifest.EntryAssembly}");

            var ctx = new PluginLoadContext(asmPath);
            var asm = ctx.LoadFromAssemblyPath(asmPath);

            var moduleType = ResolveModuleType(asm, manifest.EntryType);
            if (moduleType is null)
                return Failed(folder, id, manifest, "No public IVybeModule implementation found in the entry assembly.");

            if (Activator.CreateInstance(moduleType) is not IVybeModule module)
                return Failed(folder, id, manifest, $"Type '{moduleType.FullName}' does not implement IVybeModule.");

            module.ConfigureServices(services);
            services.AddSingleton(module);

            _contexts.Add(ctx);
            return new PluginInfo { Id = id, Directory = folder, Manifest = manifest, Status = PluginStatus.Loaded };
        }
        catch (Exception ex)
        {
            // Surface the most specific message (reflection wraps the real cause).
            var msg = (ex as ReflectionTypeLoadException)?.LoaderExceptions.FirstOrDefault()?.Message
                      ?? ex.InnerException?.Message ?? ex.Message;
            return Failed(folder, id, manifest, $"Load failed: {msg}");
        }
    }

    private static Type? ResolveModuleType(Assembly asm, string? entryType)
    {
        if (!string.IsNullOrWhiteSpace(entryType))
            return asm.GetType(entryType, throwOnError: false, ignoreCase: false);

        return asm.GetTypes().FirstOrDefault(t =>
            typeof(IVybeModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });
    }

    private bool IsHostCompatible(PluginManifest m, out string error)
    {
        error = "";
        if (Version.TryParse(m.MinHostVersion, out var min) && _hostVersion < min)
        {
            error = $"Requires host ≥ {min} (this host is {_hostVersion}).";
            return false;
        }
        if (Version.TryParse(m.MaxHostVersion, out var max) && _hostVersion > max)
        {
            error = $"Requires host ≤ {max} (this host is {_hostVersion}).";
            return false;
        }
        return true;
    }

    private static PluginInfo Failed(string dir, string id, PluginManifest? m, string error) =>
        new() { Id = id, Directory = dir, Manifest = m, Status = PluginStatus.Failed, Error = error };
}
