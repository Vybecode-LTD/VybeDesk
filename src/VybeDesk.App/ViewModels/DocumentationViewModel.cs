using System.Collections.ObjectModel;
using Avalonia.Threading;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VybeDesk.App.ViewModels;

/// <summary>
/// Module 1 — Documentation Manager. Scans a project's docs, runs a structural
/// and an AI semantic pass, and produces a report + Claude Code fix prompt.
/// </summary>
public sealed partial class DocumentationViewModel : PageViewModel, IDisposable
{
    private readonly IDocReconciliationService _docService;
    private readonly IProjectStore _projects;
    private readonly IFilePickerService _picker;
    private readonly IClipboardService _clipboard;
    private readonly INotebookOpener _notebookOpener;
    private readonly IActiveProjectContext _activeProject;
    private IReadOnlyList<DocFile> _scanned = Array.Empty<DocFile>();
    private IReadOnlyList<Finding> _structural = Array.Empty<Finding>();
    private bool _reloadingProjects;

    /// <summary>
    /// Module-local project memory. Survives the null pulse from
    /// ContentPresenter detachment and is NOT overwritten by other modules'
    /// project selections. <see cref="OnActivated"/> restores from this
    /// field — not from <see cref="IActiveProjectContext.Current"/> — so
    /// each module keeps its own independent selection (project isolation).
    /// </summary>
    private Guid? _lastSelectedProjectId;

    public override string Title => "Documentation";
    public override string Glyph => "\U0001F4C4";
    public override string Description =>
        "Scan, list, and reconcile project documentation.";

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<DocFile> Docs { get; } = new();
    public ObservableCollection<Finding> Findings { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    private Project? _selectedProject;

    public bool HasProject => SelectedProject is not null;

    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSemanticResult))]
    private string _semanticResult = "";

    [ObservableProperty]
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
    private string _auditFixPrompt = "";

    [ObservableProperty] private bool _isAuditFixPromptVisible;

    // ── watch mode (M2.7) ────────────────────────────────────────────

    /// <summary>Debounce window before a file-change rescan fires.</summary>
    private static readonly TimeSpan WatchDebounce = TimeSpan.FromMilliseconds(750);

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();

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
    /// Opens the reconciliation fix-prompt in the Notebook, scoped to
    /// the current project. The user reviews the prompt and clicks Send
    /// themselves — all AI-initiated writes flow through the Notebook's
    /// preview/execute/undo gate. No unattended auto-execution.
    /// </summary>
    [RelayCommand]
    private void ApplyReconciliationFixPromptWithAi()
    {
        if (SelectedProject is null || string.IsNullOrWhiteSpace(FixPrompt)) return;
        _notebookOpener.OpenWithFixPrompt(SelectedProject, FixPrompt);
        StatusMessage = "Fix prompt opened in Notebook — review and click Send.";
    }

    /// <summary>
    /// Opens the audit fix-prompt in the Notebook, scoped to the current
    /// project. Same review-first safety contract as the reconciliation
    /// variant above.
    /// </summary>
    [RelayCommand]
    private void ApplyAuditFixPromptWithAi()
    {
        if (SelectedProject is null || string.IsNullOrWhiteSpace(AuditFixPrompt)) return;
        _notebookOpener.OpenWithFixPrompt(SelectedProject, AuditFixPrompt);
        StatusMessage = "Audit fix prompt opened in Notebook — review and click Send.";
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
        // Synchronized: this handler fires on a FileSystemWatcher thread-pool
        // thread; DisposeWatcher() runs on the UI thread. The lock prevents
        // a race where Cancel() hits an already-disposed CTS or the new CTS
        // is immediately nulled by a concurrent DisposeWatcher() call.
        CancellationTokenSource cts;
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            cts = new CancellationTokenSource();
            _debounceCts = cts;
        }

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
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
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

        // Capture keepId AFTER the async gap: use our current selection first,
        // then fall back to our own module-local memory (NOT the cross-module
        // context, which may reflect a different module's selection).
        var keepId = SelectedProject?.Id ?? _lastSelectedProjectId;

        // Guard: Projects.Clear() causes the ComboBox TwoWay binding to fire
        // SelectedProject = null synchronously while the collection is empty.
        // _reloadingProjects tells OnSelectedProjectChanged to skip
        // SetCurrent() for that transient null so it never propagates.
        _reloadingProjects = true;
        try
        {
            Projects.Clear();
            foreach (var p in all) Projects.Add(p);
            // Only restore a previous selection — do NOT auto-select the
            // first project when none was ever chosen. The view shows a
            // "Choose a project" landing until the user picks one.
            SelectedProject = keepId is not null
                ? Projects.FirstOrDefault(p => p.Id == keepId)
                : null;

            // Explicitly save to module-local memory because the
            // _reloadingProjects guard suppresses OnSelectedProjectChanged
            // during this window, so the normal save path doesn't run.
            _lastSelectedProjectId = SelectedProject?.Id;
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _reloadingProjects = false);
        }
    }

    partial void OnSelectedProjectChanged(Project? oldValue, Project? newValue)
    {
        // Suppress the transient null that arrives from the ComboBox TwoWay
        // binding when Projects.Clear() runs inside LoadProjectsAsync.
        if (_reloadingProjects) return;

        // Ignore null writes from view detachment. When Avalonia's
        // ContentPresenter detaches this view on navigation, the ComboBox
        // TwoWay binding fires null back. We must NOT clear state for this
        // — OnActivated will restore the selection when we come back.
        if (newValue is null && oldValue is not null) return;

        // Persist the user's selection for this module. Survives null
        // pulses from ContentPresenter detachment and is NOT overwritten
        // by other modules' project selections (project isolation).
        _lastSelectedProjectId = newValue?.Id;

        // Keep the cross-module context in sync for AI model resolution
        // (AnthropicChatService reads IActiveProjectContext.Current).
        if (newValue?.Id != _activeProject.Current?.Id)
            _activeProject.SetCurrent(newValue);

        // Only update folder / trigger scan on genuine project switch,
        // not on same-ID refreshes from LoadProjectsAsync re-creating refs.
        if (oldValue?.Id == newValue?.Id) return;

        if (newValue is not null && !string.IsNullOrWhiteSpace(newValue.FolderPath))
        {
            FolderPath = newValue.FolderPath;
            // Auto-scan when a project is selected so the findings panel
            // immediately shows doc state without requiring a manual click.
            _ = ScanAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Re-sync SelectedProject from this module's own local memory on
    /// every navigation back to this page. Necessary because Avalonia's
    /// ContentPresenter detaches the old view on navigate-away, which
    /// causes the ModuleHeader ComboBox's TwoWay binding to null out
    /// the backing field. Uses <see cref="_lastSelectedProjectId"/>
    /// instead of <see cref="IActiveProjectContext.Current"/> so each
    /// module keeps its own independent project selection.
    /// </summary>
    public override void OnActivated()
    {
        if (_lastSelectedProjectId is null) return;
        if (SelectedProject?.Id == _lastSelectedProjectId) return;
        var found = Projects.FirstOrDefault(p => p.Id == _lastSelectedProjectId);
        if (found is not null)
            SelectedProject = found;
    }

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
            IsAuditOpen = false;
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
            IsAuditOpen = false;
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

    public void Dispose()
    {
        DisposeWatcher();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
        _projects.Changed -= OnProjectsChanged;
    }
}
