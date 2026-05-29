using System.Text.Json;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Services.Storage;

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
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Opts));
        }
        catch (Exception)
        {
            // Settings save is best-effort — the app should not crash if
            // the file is locked or the disk is full. The in-memory
            // Current is already updated, so this session's settings are
            // correct; they just won't survive a restart.
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path))
                               ?? new AppSettings();
                if (MigrateModelId(settings))
                    File.WriteAllText(_path, JsonSerializer.Serialize(settings, Opts));
                return settings;
            }
        }
        catch (Exception ex)
        {
            // Corrupt or inaccessible settings file — fall back to defaults
            // but surface the issue so it doesn't go unnoticed.
            Console.Error.WriteLine($"[Settings] Failed to read {_path}: {ex.Message}");
        }
        return new AppSettings();
    }

    /// <summary>Replaces known-bad / fabricated model IDs with the current default.
    /// Returns true if a migration was applied (caller should persist).</summary>
    private static bool MigrateModelId(AppSettings settings)
    {
        // These IDs were shipped in earlier versions of the catalog but never existed
        // in the Anthropic API — they cause 400/404 errors on every AI call.
        var badIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "claude-opus-4-7",
            "claude-sonnet-4-6",
            "claude-haiku-4-5",
            "claude-opus-4-6",
            "claude-sonnet-4-5",
            "claude-opus-4-5",
            "claude-opus-4-1",
            "claude-sonnet-4-7",
        };

        if (!string.IsNullOrWhiteSpace(settings.Model) && badIds.Contains(settings.Model))
        {
            settings.Model = "claude-sonnet-4-20250514";
            return true;
        }
        return false;
    }
}
