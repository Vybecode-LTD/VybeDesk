using System.Text.Json;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Storage;

/// <summary>Persists AppSettings as JSON in the per-user app data directory.</summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(Paths.AppDataDir(), "settings.json");

    public JsonSettingsService() => Current = Load();

    public AppSettings Current { get; private set; }

    public void Save(AppSettings settings)
    {
        Current = settings;
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, Opts));
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path))
                       ?? new AppSettings();
        }
        catch { /* corrupt file -> fall back to defaults */ }
        return new AppSettings();
    }
}
