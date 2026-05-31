using VybeDesk.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VybeDesk.App.ViewModels;

/// <summary>App settings — API key (DPAPI), model, and output location. Functional.</summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    /// <summary>
    /// Curated list of common Claude model IDs (shared with the per-project
    /// override picker on Projects — see <see cref="ProjectsViewModel"/>).
    /// The dropdown drives the Model textbox via <see cref="SelectedModel"/>;
    /// users can still type any custom ID directly into the textbox for
    /// previews / new releases the catalog hasn't been updated for.
    /// </summary>
    public IReadOnlyList<ModelOption> AvailableModels => ModelsCatalog.All;

    private readonly ISecureKeyStore _keyStore;
    private readonly ISettingsService _settings;
    private readonly IFilePickerService _picker;
    private readonly IAiCallStore _callStore;

    public override string Title => "General";
    public override string Glyph => "\U0001F527"; // \ud83d\udd27 \u2014 sub-page of the Settings section
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
        ModelsCatalog.All.FirstOrDefault(m => m.Id == Model)?.Tier
        ?? "Custom model ID — tier unknown.";

    [ObservableProperty]
    private string _outputPath = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private int _totalCalls;

    [ObservableProperty]
    private string _totalTokensSummary = "";

    [ObservableProperty]
    private string _totalCostSummary = "";

    [ObservableProperty]
    private string _cacheSummary = "";

    public string KeyStatus => HasKey
        ? "An API key is saved (encrypted at rest via DPAPI)."
        : "No API key saved yet.";

    public SettingsViewModel(
        ISecureKeyStore keyStore,
        ISettingsService settings,
        IFilePickerService picker,
        IAiCallStore callStore)
    {
        _keyStore = keyStore;
        _settings = settings;
        _picker = picker;
        _callStore = callStore;
        _hasKey = keyStore.HasKey;
        _model = settings.Current.Model;
        _outputPath = settings.Current.DefaultOutputPath;
        _selectedModel = ModelsCatalog.All.FirstOrDefault(m => m.Id == _model);
        _callStore.Changed += OnCallStoreChanged;
        _ = RefreshActivityAsync();
    }

    private void OnCallStoreChanged()
        => Avalonia.Threading.Dispatcher.UIThread.Post(async () => await RefreshActivityAsync());

    private async Task RefreshActivityAsync()
    {
        var s = await _callStore.GetSummaryAsync();
        TotalCalls = s.TotalCalls;
        TotalTokensSummary = $"{s.TotalInputTokens:N0} in · {s.TotalOutputTokens:N0} out";
        TotalCostSummary = $"${s.TotalCost:F4}";
        CacheSummary = $"{s.TotalCacheCreationTokens:N0} created · {s.TotalCacheReadTokens:N0} read";
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
