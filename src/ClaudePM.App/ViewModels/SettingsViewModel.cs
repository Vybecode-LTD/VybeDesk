using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>A quick-pick entry for the model dropdown in Settings.</summary>
public sealed record ModelOption(string Id, string DisplayName, string Tier);

/// <summary>App settings — API key (DPAPI), model, and output location. Functional.</summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    /// <summary>
    /// Curated list of common Claude model IDs. The dropdown drives the
    /// Model textbox via <see cref="SelectedModel"/>; users can still type
    /// any custom ID directly into the textbox for previews / new releases
    /// the dropdown hasn't been updated for.
    /// </summary>
    public IReadOnlyList<ModelOption> AvailableModels => ModelsCatalog;

    private static readonly IReadOnlyList<ModelOption> ModelsCatalog = new[]
    {
        new ModelOption("claude-opus-4-7",   "Claude Opus 4.7",
            "Most capable · slowest · most expensive ($$$)"),
        new ModelOption("claude-opus-4-5",   "Claude Opus 4.5",
            "Previous-gen Opus · most capable ($$$)"),
        new ModelOption("claude-sonnet-4-7", "Claude Sonnet 4.7",
            "Balanced · ~5× cheaper than Opus ($$) · recommended default"),
        new ModelOption("claude-sonnet-4-5", "Claude Sonnet 4.5",
            "Previous-gen Sonnet · balanced ($$)"),
        new ModelOption("claude-haiku-4-5",  "Claude Haiku 4.5",
            "Fastest · cheapest ($) · great for quick edits / classification"),
    };

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
    [NotifyPropertyChangedFor(nameof(SelectedModelTier))]
    private string _model = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedModelTier))]
    private ModelOption? _selectedModel;

    public string SelectedModelTier =>
        ModelsCatalog.FirstOrDefault(m => m.Id == Model)?.Tier
        ?? "Custom model ID — tier unknown.";

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
        _selectedModel = ModelsCatalog.FirstOrDefault(m => m.Id == _model);
    }

    partial void OnSelectedModelChanged(ModelOption? value)
    {
        if (value is not null && value.Id != Model)
            Model = value.Id;
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
