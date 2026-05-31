using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VybeDesk.Core.Services;
using VybeDesk.Services.Plugins;

namespace VybeDesk.App.ViewModels;

/// <summary>
/// The "Settings → Plugins" management page. Lists every plugin the loader
/// discovered this session — loaded, disabled, incompatible, or failed — and
/// lets the user enable/disable them (effective next launch), install a plugin
/// from a .vybeplugin/zip package, and open the plugins folder. The page copy
/// surfaces the trust model: plugins run with full access and are not sandboxed.
/// </summary>
public sealed partial class PluginsViewModel : PageViewModel
{
    private readonly IPluginRegistry _registry;
    private readonly IFilePickerService _picker;

    public override string Title => "Plugins";
    public override string Glyph => "\U0001F9E9"; // 🧩
    public override string Description =>
        "Extend VybeDesk with community modules. Plugins run with full access to your data — only install ones you trust.";

    public ObservableCollection<PluginRowViewModel> Plugins { get; } = new();

    public bool HasPlugins => Plugins.Count > 0;
    public bool IsEmpty => Plugins.Count == 0;
    public string PluginsDirectory => _registry.PluginsDirectory;

    /// <summary>Shown as a banner after any enable/disable/install — those changes apply on restart.</summary>
    [ObservableProperty] private bool _restartNeeded;

    public PluginsViewModel(IPluginRegistry registry, IFilePickerService picker, IClipboardService clipboard)
    {
        _registry = registry;
        _picker = picker;
        Clipboard = clipboard;
        foreach (var p in registry.Plugins)
            Plugins.Add(new PluginRowViewModel(p));
    }

    [RelayCommand]
    private void OpenPluginsFolder()
    {
        try
        {
            Directory.CreateDirectory(_registry.PluginsDirectory);
            Process.Start(new ProcessStartInfo { FileName = _registry.PluginsDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleEnabled(PluginRowViewModel? row)
    {
        if (row is null) return;
        var enable = !row.IsEnabled;
        PluginState.SetEnabled(row.Id, enable);
        row.IsEnabled = enable;
        RestartNeeded = true;
        StatusMessage = $"{(enable ? "Enabled" : "Disabled")} {row.Name}. Restart to apply.";
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var package = await _picker.PickFileAsync(
            title: "Choose a plugin package",
            filters: new[] { new FilePickerFileType("VybeDesk plugin", new[] { "*.vybeplugin", "*.zip" }) });
        if (package is null) return;

        try
        {
            StatusMessage = InstallFromZip(package);
            RestartNeeded = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Install failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Extracts a plugin package to a temp dir, reads its manifest to learn the
    /// id, then moves it into <c>plugins/&lt;id&gt;</c>. Validates a plugin.json
    /// exists and carries an id before installing. Returns a user-facing message.
    /// </summary>
    private string InstallFromZip(string zipPath)
    {
        var temp = Path.Combine(Path.GetTempPath(), "vybedesk-plugin-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, temp);

            // plugin.json may be at the archive root or one folder down.
            var manifestPath = Directory
                .EnumerateFiles(temp, "plugin.json", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (manifestPath is null)
                return "That package has no plugin.json — it isn't a VybeDesk plugin.";

            var manifest = JsonSerializer.Deserialize<PluginManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
                return "The package's plugin.json is missing an \"id\".";

            var sourceDir = Path.GetDirectoryName(manifestPath)!;
            var destDir = Path.Combine(_registry.PluginsDirectory, manifest.Id);
            Directory.CreateDirectory(_registry.PluginsDirectory);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
            Directory.Move(sourceDir, destDir);

            return $"Installed “{manifest.Name}” {manifest.Version}. Restart VybeDesk to load it.";
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
        }
    }
}
