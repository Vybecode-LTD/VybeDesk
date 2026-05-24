using System.Collections.ObjectModel;
using Avalonia.Threading;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 1 — Documentation Manager. Scans a project's docs, runs a structural
/// and an AI semantic pass, and produces a report + Claude Code fix prompt.
/// </summary>
public sealed partial class DocumentationViewModel : PageViewModel
{
    private readonly IDocReconciliationService _docService;
    private readonly IProjectStore _projects;
    private readonly IFilePickerService _picker;
    private IReadOnlyList<DocFile> _scanned = Array.Empty<DocFile>();
    private IReadOnlyList<Finding> _structural = Array.Empty<Finding>();

    public override string Title => "Documentation";
    public override string Glyph => "\U0001F4C4";
    public override string Description =>
        "Scan, list, and reconcile project documentation.";

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<DocFile> Docs { get; } = new();
    public ObservableCollection<Finding> Findings { get; } = new();

    [ObservableProperty] private Project? _selectedProject;
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _semanticResult = "";
    [ObservableProperty] private string _fixPrompt = "";
    [ObservableProperty] private bool _isFixPromptVisible;
    [ObservableProperty] private string _statusMessage = "Pick a project or enter a folder path, then Scan.";

    // ── inline editor state (M2.6) ────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorTitle), nameof(IsDefaultViewVisible))]
    private DocFile? _selectedDoc;

    [ObservableProperty] private string _editorContent = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefaultViewVisible))]
    private bool _isEditorOpen;

    public string EditorTitle => SelectedDoc?.RelativePath ?? "";
    public bool IsDefaultViewVisible => !IsEditorOpen;

    // ── watch mode (M2.7) ────────────────────────────────────────────

    /// <summary>Debounce window before a file-change rescan fires.</summary>
    private static readonly TimeSpan WatchDebounce = TimeSpan.FromMilliseconds(750);

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;

    [ObservableProperty] private bool _isWatchModeEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    private int _docCount;

    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;

    public bool IsNotBusy => !IsBusy;
    public bool HasReport => DocCount > 0;
    public bool HasSemanticResult => !string.IsNullOrWhiteSpace(SemanticResult);

    public DocumentationViewModel(
        IDocReconciliationService docService,
        IProjectStore projects,
        IFilePickerService picker)
    {
        _docService = docService;
        _projects = projects;
        _picker = picker;
        _projects.Changed += OnProjectsChanged;
        _ = LoadProjectsAsync();
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await LoadProjectsAsync());

    partial void OnSelectedDocChanged(DocFile? value)
    {
        if (value is null)
        {
            EditorContent = "";
            IsEditorOpen = false;
            return;
        }

        try
        {
            EditorContent = File.ReadAllText(value.FullPath);
            IsEditorOpen = true;
            StatusMessage = "Editing " + value.RelativePath
                + " — Save writes to disk; Close returns to findings.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Couldn't open " + value.RelativePath + ": " + ex.Message;
            IsEditorOpen = false;
        }
    }

    [RelayCommand]
    private async Task SaveEditorAsync(CancellationToken ct)
    {
        if (SelectedDoc is null) return;
        try
        {
            await File.WriteAllTextAsync(SelectedDoc.FullPath, EditorContent, ct);
            StatusMessage = "Saved " + SelectedDoc.RelativePath
                + ". Run Scan to refresh findings.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void RevertEditor()
    {
        if (SelectedDoc is null) return;
        try
        {
            EditorContent = File.ReadAllText(SelectedDoc.FullPath);
            StatusMessage = "Reverted " + SelectedDoc.RelativePath + " to its on-disk content.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Revert failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsEditorOpen = false;
        SelectedDoc = null;
        EditorContent = "";
    }

    // ── watch mode plumbing ──────────────────────────────────────────

    partial void OnIsWatchModeEnabledChanged(bool value)
        => RebuildWatcher();

    partial void OnFolderPathChanged(string value)
    {
        if (IsWatchModeEnabled) RebuildWatcher();
    }

    private void RebuildWatcher()
    {
        DisposeWatcher();
        if (!IsWatchModeEnabled) return;

        var path = FolderPath?.Trim();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            StatusMessage = "Watch mode: folder doesn't exist yet — set a folder and re-toggle.";
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnDocFileChanged;
            _watcher.Created += OnDocFileChanged;
            _watcher.Deleted += OnDocFileChanged;
            _watcher.Renamed += OnDocFileChanged;
            StatusMessage = "Watch mode on — edits to .md / .txt under "
                + path + " trigger a rescan.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Watch mode failed to attach: " + ex.Message;
        }
    }

    private void OnDocFileChanged(object? sender, FileSystemEventArgs e)
    {
        var ext = Path.GetExtension(e.Name)?.ToLowerInvariant();
        if (ext is not (".md" or ".txt")) return;

        // Debounce: cancel any pending rescan and start a new timer. Saves
        // run on the UI thread when the debounce fires.
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(WatchDebounce, cts.Token);
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (IsBusy) return;
                    await ScanAsync(cts.Token);
                });
            }
            catch (OperationCanceledException) { /* superseded by newer change */ }
        });
    }

    private void DisposeWatcher()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _debounceCts?.Cancel();
        _debounceCts = null;
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var picked = await _picker.PickFolderAsync(
            title: "Pick the project folder to scan",
            startLocation: FolderPath);
        if (picked is not null) FolderPath = picked;
    }

    private async Task LoadProjectsAsync()
    {
        var all = await _projects.GetAllAsync();
        Projects.Clear();
        foreach (var p in all) Projects.Add(p);
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        if (value is not null && !string.IsNullOrWhiteSpace(value.FolderPath))
            FolderPath = value.FolderPath;
    }

    partial void OnSemanticResultChanged(string value)
        => OnPropertyChanged(nameof(HasSemanticResult));

    [RelayCommand]
    private async Task ScanAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            StatusMessage = "Enter a folder path first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Scanning\u2026";
        try
        {
            _scanned = await _docService.ScanAsync(FolderPath, ct);
            Docs.Clear();
            foreach (var d in _scanned) Docs.Add(d);

            _structural = await _docService.AnalyzeStructuralAsync(FolderPath, _scanned, ct);
            Findings.Clear();
            foreach (var f in _structural) Findings.Add(f);

            DocCount = Docs.Count;
            CriticalCount = _structural.Count(f => f.Severity == FindingSeverity.Critical);
            WarningCount = _structural.Count(f => f.Severity == FindingSeverity.Warning);
            InfoCount = _structural.Count(f => f.Severity == FindingSeverity.Info);

            SemanticResult = "";
            IsFixPromptVisible = false;
            StatusMessage = Docs.Count + " doc(s) scanned, " + _structural.Count
                + " structural finding(s). Run AI Analysis for doc-vs-doc checks.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Scan failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunSemanticAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (_scanned.Count == 0)
        {
            StatusMessage = "Scan a project first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Running AI semantic analysis\u2026";
        try
        {
            SemanticResult = await _docService.AnalyzeSemanticAsync(_scanned, ct);
            StatusMessage = "AI semantic analysis complete.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "AI analysis failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GenerateFixPrompt()
    {
        if (_scanned.Count == 0)
        {
            StatusMessage = "Scan a project first.";
            return;
        }
        FixPrompt = _docService.BuildFixPrompt(FolderPath, _structural, SemanticResult);
        IsFixPromptVisible = true;
        StatusMessage = "Fix prompt generated — copy it into Claude Code.";
    }

    [RelayCommand]
    private async Task ExportReportAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (_scanned.Count == 0)
        {
            StatusMessage = "Scan a project first.";
            return;
        }

        IsBusy = true;
        try
        {
            var markdown = _docService.BuildReportMarkdown(FolderPath, _structural, SemanticResult);
            var path = await _docService.SaveReportAsync(FolderPath, markdown, ct);
            StatusMessage = "Report saved to " + path;
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
