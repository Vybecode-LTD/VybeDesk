using System.Text.Json;

namespace VybeDesk.Services.Plugins;

/// <summary>
/// Persists per-plugin enable/disable state in
/// <c>%LOCALAPPDATA%\VybeDesk\plugins-state.json</c>. Read at composition time
/// (before the DI provider exists), so it is a plain static helper rather than
/// a DI service. Enable/disable takes effect on the next launch — true
/// mid-session unload of a collectible context only completes once every
/// reference (visual tree, DI singletons) is released.
/// </summary>
public static class PluginState
{
    private sealed class State
    {
        public List<string> Disabled { get; set; } = new();
    }

    private static string FilePath(string appDataDir) => Path.Combine(appDataDir, "plugins-state.json");

    /// <summary>Ids the user has disabled. Empty (not throwing) if the file is absent or unreadable.</summary>
    public static ISet<string> LoadDisabled() => LoadDisabled(Paths.AppDataDir());

    /// <summary>Enable or disable a plugin id and persist. Effective next launch.</summary>
    public static void SetEnabled(string pluginId, bool enabled) => SetEnabled(pluginId, enabled, Paths.AppDataDir());

    // Internal overloads take the data directory so tests can point at a temp
    // folder (see InternalsVisibleTo VybeDesk.Tests in VybeDesk.Services.csproj).
    internal static ISet<string> LoadDisabled(string appDataDir)
    {
        try
        {
            var path = FilePath(appDataDir);
            if (!File.Exists(path)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var state = JsonSerializer.Deserialize<State>(File.ReadAllText(path));
            return new HashSet<string>(state?.Disabled ?? new(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal static void SetEnabled(string pluginId, bool enabled, string appDataDir)
    {
        var disabled = LoadDisabled(appDataDir);
        if (enabled) disabled.Remove(pluginId);
        else disabled.Add(pluginId);

        var state = new State { Disabled = disabled.ToList() };
        File.WriteAllText(FilePath(appDataDir), JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }
}
