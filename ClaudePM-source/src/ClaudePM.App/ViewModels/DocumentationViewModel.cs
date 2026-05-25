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
    private readonly IClipboardService _clipboard;
    private readonly INotebookOpener _notebookOpener;
    private readonly IActiveProjectContext _activeProject;
    private IReadOnlyList<DocFile> _scanned = Array.Empty<DocFile>();
    private IReadOnlyList<Finding> _structural = Array.Empty<Finding>();

    public override string Title => "Documentation";
    public override string Glyph => "\U0001F4C4";
    public override string Description =>
        "Scan, list, and reconcile project documentation.";

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<DocFile> Docs { get; } = new();
    public ObservableCollection<Finding> Findings { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyReconciliationFixPromptWithAiCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyAuditFixPromptWithAiCommand))]
    private Project? _selectedProject;

    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _semanticResult = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyReconciliationFixPromptWithAiCommand))]
    private string _fixPrompt = "";

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
    public bool IsDefaultViewVisible => !IsEditorOpen && !IsAuditOpen;

    // ── project audit (M2.5) ─────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasAuditReport),
        nameof(AuditDesign),
        nameof(AuditItems),
        nameof(AuditComplete),
        nameof(AuditIncomplete),
        nameof(AuditInconsistencies),
        nameof(HasInconsistencies))]
    private ProjectAuditReport? _auditReport;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDefaultViewVisible))]
    private bool _isAuditOpen;

    public bool HasAuditReport => AuditReport is not null;
    public string AuditDesign => AuditReport?.Design ?? "";
    public IReadOnlyList<AuditRoadmapItem> AuditItems =>
        AuditReport?.RoadmapItems ?? Array.Empty<AuditRoadmapItem>();
    public IReadOnlyList<AuditRoadmapItem> AuditComplete =>
        AuditReport?.Complete ?? Array.Empty<AuditRoadmapItem>();
    public IReadOnlyList<AuditRoadmapItem> AuditIncomplete =>
        AuditReport?.Incomplete ?? Array.Empty<AuditRoadmapItem>();
    public IReadOnlyList<AuditInconsistency> AuditInconsistencies =>
        AuditReport?.Inconsistencies ?? Array.Empty<AuditInconsistency>();
    public bool HasInconsistencies => AuditInconsistencies.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyAuditFixPromptWithAiCommand))]
    private string _auditFixPrompt = "";

    [ObservableProperty] private bool _isAuditFixPromptVisible;

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
        IFilePickerService picker,
        IClipboardService clipboard,
        INotebookOpener notebookOpener,
        IActiveProjectContext activeProject)
    {
        _docService = docService;
        _projects = projects;
        _picker = picker;
        _clipboard = clipboard;
        _notebookOpener = notebookOpener;
        _activeProject = activeProject;
        _projects.Changed += OnProjectsChanged;
        _ = LoadProjectsAsync();
    }

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (await _clipboard.SetTextAsync(text))
            StatusMessage = "Copied to clipboard.";
    }

    /// <summary>
    /// Hand the reconciliation fix-prompt off to the Notebook for the user
    /// to review + send. The Notebook is scoped to the currently selected
    /// project so any agent file actions stay confined to that project's
    /// folder via the existing AgentActionService preview/execute/undo gate.
    /// Does NOT auto-send — the user clicks Send in the Notebook themselves
    /// so they can edit the prompt first if they want.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyReconciliationFixPromptWithAi))]
    private void ApplyReconciliationFixPromptWithAi()
    {
        if (SelectedProject is null || string.IsNullOrWhiteSpace(FixPrompt)) return;
        _notebookOpener.OpenWithFixPrompt(SelectedProject, FixPrompt);
        StatusMessage = "Fix prompt sent to Notebook. Review + click Send to apply.";
    }

    private bool CanApplyReconciliationFixPromptWithAi()
        => SelectedProject is not null && !string.IsNullOrWhiteSpace(FixPrompt);

    /// <summary>
    /// Same as the reconciliation variant but for the project-audit fix
    /// prompt. Lives on the audit overlay's fix-prompt panel.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyAuditFixPromptWithAi))]
    private void ApplyAuditFixPromptWithAi()
    {
        if (SelectedProject is null || string.IsNullOrWhiteSpace(AuditFixPrompt)) return;
        _notebookOpener.OpenWithFixPrompt(SelectedProject, AuditFixPrompt);
        StatusMessage = "Audit fix prompt sent to Notebook. Review + click Send to apply.";
    }

    private bool CanApplyAuditFixPromptWithAi()
        => SelectedProject is not null && !string.IsNullOrWhiteSpace(AuditFixPrompt);

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

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunAuditAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        if (_scanned.Count == 0)
        {
            StatusMessage = "Scan a project first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Running project audit — synthesizing across the docs…";
        try
        {
            AuditReport = await _docService.AuditAsync(_scanned, ct);
            IsAuditOpen = true;
            StatusMessage = "Audit complete: " + AuditItems.Count
                + " roadmap item(s), "
                + AuditInconsistencies.Count + " inconsistency(ies).";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Audit failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseAudit() => IsAuditOpen = false;

    [RelayCommand]
    private void GenerateAuditFixPrompt()
    {
        if (AuditReport is null) return;
        // Separate state from the structural fix prompt so the two flows
        // don't stomp on each other and the audit-specific prompt stays
        // visible in the audit overlay itself.
        AuditFixPrompt = _docService.BuildAuditFixPrompt(FolderPath, AuditReport.Inconsistencies);
        IsAuditFixPromptVisible = true;
        StatusMessage = "Fix prompt generated — copy it from the panel inside the audit overlay.";
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
        // Push the selection through the cross-cutting context so the AI
        // service can pick up this project's Model override on its next call.
        // M4 #16: per-project model + output overrides.
        _activeProject.SetCurrent(value);
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
