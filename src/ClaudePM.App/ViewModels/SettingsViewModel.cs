using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>App settings — API key (DPAPI), model, and output location. Functional.</summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ISecureKeyStore _keyStore;
    private readonly ISettingsService _settings;
    private readonly IFilePickerService _picker;

    public override string Title => "Settings";
    public override string Glyph => "\u2699";
    public override string Description => "API key, model, and default output location.";

    [ObservableProperty]
    private string _apiKeyInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyStatus))]
    private bool _hasKey;

    [ObservableProperty]
    private string _model = "";

    [ObservableProperty]
    private string _outputPath = "";

    [ObservableProperty]
    private string _status = "";

    public string KeyStatus => HasKey
        ? "An API key is saved (encrypted at rest via DPAPI)."
        : "No API key saved yet.";

    public SettingsViewModel(
        ISecureKeyStore keyStore,
        ISettingsService settings,
        IFilePickerService picker)
    {
        _keyStore = keyStore;
        _settings = settings;
        _picker = picker;
        _hasKey = keyStore.HasKey;
        _model = settings.Current.Model;
        _outputPath = settings.Current.DefaultOutputPath;
    }

    [RelayCommand]
    private async Task BrowseOutputPathAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick a default output folder",
            startLocation: OutputPath);
        if (picked is not null) OutputPath = picked;
    }

    [RelayCommand]
    private void SaveKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            Status = "Enter a key first.";
            return;
        }

        try
        {
            _keyStore.SaveKey(ApiKeyInput.Trim());
        }
        catch (ArgumentException ex)
        {
            Status = ex.Message;
            return;
        }

        ApiKeyInput = "";
        HasKey = true;
        Status = "API key saved.";
    }

    [RelayCommand]
    private void ClearKey()
    {
        _keyStore.ClearKey();
        HasKey = false;
        Status = "API key cleared.";
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var s = _settings.Current;
        s.Model = Model;
        s.DefaultOutputPath = OutputPath;
        _settings.Save(s);
        Status = "Settings saved.";
    }
}
