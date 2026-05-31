using CommunityToolkit.Mvvm.ComponentModel;
using VybeDesk.Services.Plugins;

namespace VybeDesk.App.ViewModels;

/// <summary>One row in the Plugins list — a view over a discovered <see cref="PluginInfo"/>.</summary>
public sealed partial class PluginRowViewModel : ObservableObject
{
    private readonly PluginInfo _info;

    public PluginRowViewModel(PluginInfo info)
    {
        _info = info;
        _isEnabled = info.Status != PluginStatus.Disabled;
    }

    /// <summary>Reflects the user's enable/disable choice. Effective on next launch.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleLabel))]
    private bool _isEnabled;

    public string Id => _info.Id;
    public string Name => _info.Name;
    public string Version => string.IsNullOrEmpty(_info.Version) ? "" : $"v{_info.Version}";
    public string Author => string.IsNullOrEmpty(_info.Author) ? "" : $"by {_info.Author}";
    public string Description => _info.Description;

    public string StatusText => _info.Status switch
    {
        PluginStatus.Loaded => "Loaded",
        PluginStatus.Disabled => "Disabled",
        PluginStatus.Incompatible => "Incompatible",
        PluginStatus.Failed => "Failed to load",
        _ => "",
    };

    public bool HasError => !string.IsNullOrEmpty(_info.Error);
    public string Error => _info.Error ?? "";

    public string CapabilitiesText => _info.Capabilities.Count > 0
        ? "Uses: " + string.Join(", ", _info.Capabilities)
        : "No special capabilities declared";

    public string ToggleLabel => IsEnabled ? "Disable" : "Enable";
}
