using System.Collections.ObjectModel;
using Avalonia.Threading;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VybeDesk.App.ViewModels;

/// <summary>
/// Module 8 — Vision Audit. Project-scoped four-stage workflow:
/// Extract → Approve → ChooseMode → RunReview. The Approve gate is
/// mandatory: nothing audits against an unapproved vision because an
/// audit against the wrong measuring stick is worse than none.
///
/// Mode picker uses plain-language phrasing per spec — the target user
/// shouldn't have to know what "Structural" vs "Targeted" means. The
/// rendered choice talks about cost / depth / size-independence instead.
/// </summary>
public sealed partial class VisionAuditViewModel : PageViewModel
{
    private readonly IVisionStore _store;
    private readonly IVisionAuditService _audit;
    private readonly IAuditHistoryStore _history;
    private readonly IProjectStore _projects;
    private readonly IClipboardService _clipboard;
    private readonly IActiveProjectContext _activeProjectContext;

    public override string Title => "Vision Audit";
    public override string Glyph => "\U0001F9ED"; // 🧭
    public override string Description =>
        "Catch project drift: extract a vision, approve it, audit against it.";

    public ObservableCollection<Project> Projects { get; } = new();

    /// <summary>
    /// Editable list of statements shown on the Approve stage. Wrapped in
    /// `StatementEditViewModel` so each row has its own bindable Text and
    /// a per-row Remove command.
    /// </summary>
    public ObservableCollection<StatementEditViewModel> DraftStatements { get; } = new();

    /// <summary>
    /// Verdicts from the latest audit run. Bound to the report ItemsControl
    /// on the RunReview stage.
    /// </summary>
    public ObservableCollection<StatementVerdict> Verdicts { get; } = new();

    /// <summary>
    /// Persisted audit history for the currently selected project,
    /// newest-first. Each entry stores its own report markdown and deep-dive
    /// prompt verbatim so the user can revisit an old audit without
    /// re-paying for the AI call.
    /// </summary>
    public ObservableCollection<AuditHistoryEntry> History { get; } = new();

    public bool HasHistory => History.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    private Project? _selectedProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExtractStage))]
    [NotifyPropertyChangedFor(nameof(IsApproveStage))]
    [NotifyPropertyChangedFor(nameof(IsChooseModeStage))]
    [NotifyPropertyChangedFor(nameof(IsRunReviewStage))]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]
    private VisionAuditStage _stage = VisionAuditStage.Extract;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    [NotifyPropertyChangedFor(nameof(OffTrackCount))]
    [NotifyPropertyChangedFor(nameof(AtRiskCount))]
    [NotifyPropertyChangedFor(nameof(OnTrackCount))]
    private AuditReport? _latestReport;

    [ObservableProperty] private string _reportMarkdown = "";
    [ObservableProperty] private string _deepDivePrompt = "";

    /// <summary>The chosen audit mode for the next run.</summary>
    [ObservableProperty] private AuditMode _selectedMode = AuditMode.Structural;

    /// <summary>Tracks the loaded record (null = no vision drafted yet for this project).</summary>
    private VisionRecord? _loadedRecord;
    private bool _reloadingProjects;

    /// <summary>
    /// Module-local project memory. Survives the null pulse from
    /// ContentPresenter detachment and is NOT overwritten by other modules'
    /// project selections. <see cref="OnActivated"/> restores from this
    /// field — not from <see cref="IActiveProjectContext.Current"/> — so
    /// each module keeps its own independent selection (project isolation).
    /// </summary>
    private Guid? _lastSelectedProjectId;

    public bool IsNotBusy => !IsBusy;
    public bool HasProject => SelectedProject is not null;
    public bool IsExtractStage => Stage == VisionAuditStage.Extract;
    public bool IsApproveStage => Stage == VisionAuditStage.Approve;
    public bool IsChooseModeStage => Stage == VisionAuditStage.ChooseMode;
    public bool IsRunReviewStage => Stage == VisionAuditStage.RunReview;

    public bool HasReport => LatestReport is not null;
    public int OffTrackCount => LatestReport?.Verdicts.Count(v => v.Rank == AlignmentRank.OffTrack) ?? 0;
    public int AtRiskCount => LatestReport?.Verdicts.Count(v => v.Rank == AlignmentRank.AtRisk) ?? 0;
    public int OnTrackCount => LatestReport?.Verdicts.Count(v => v.Rank == AlignmentRank.OnTrack) ?? 0;

    // ===== Unified module header (v0.31) ======================================
    //
    // NAMING CONVENTION — IMPORTANT for any future VM that opts in to the
    // unified header. The base class declares `public virtual IRelayCommand?
    // GoModuleHomeCommand` (and `ResetCommand` / `RestartCommand`). A concrete
    // VM CANNOT declare `[RelayCommand] private void GoModuleHome()` to fill
    // those — the source generator would emit a `public IRelayCommand
    // GoModuleHomeCommand { get; }` auto-property that hides the virtual
    // without `override`, and the compiler errors out (CS0506 / CS0108).
    //
    // The pattern that works: name the [RelayCommand] methods DIFFERENTLY
    // from the base virtuals (here: GoToFirstStage / ResetCurrentStage /
    // RestartModule below — note `StartOver` already existed and is reused),
    // then forward via an expression-bodied override. The generated *Command
    // property is non-nullable IRelayCommand; the implicit conversion to the
    // base's nullable IRelayCommand? is fine.

    public override IReadOnlyList<string> Breadcrumbs => Stage switch
    {
        VisionAuditStage.Extract    => new[] { "Step 1 — Extract" },
        VisionAuditStage.Approve    => new[] { "Step 2 — Approve" },
        VisionAuditStage.ChooseMode => new[] { "Step 3 — Choose mode" },
        VisionAuditStage.RunReview  => new[] { "Step 4 — Review report" },
        _ => Array.Empty<string>(),
    };

    // Repurpose existing StartOver — it already navigates to Extract WITHOUT
    // clearing data, which is exactly the GoModuleHome semantic. Kept under
    // its original name so the existing XAML binding to StartOverCommand on
    // the Stage 4 "Re-extract from docs" button (now removed in v0.31 but
    // worth preserving the rename-free diff) doesn't churn.
    public override IRelayCommand? GoModuleHomeCommand => StartOverCommand;
    public override IRelayCommand? ResetCommand => ResetCurrentStageCommand;
    public override IRelayCommand? RestartCommand => RestartModuleCommand;

    public VisionAuditViewModel(
        IVisionStore store,
        IVisionAuditService audit,
        IAuditHistoryStore history,
        IProjectStore projects,
        IClipboardService clipboard,
        IActiveProjectContext activeProjectContext)
    {
        _store = store;
        _audit = audit;
        _history = history;
        _projects = projects;
        _clipboard = clipboard;
        _activeProjectContext = activeProjectContext;

        _projects.Changed += OnProjectsChanged;
        _ = LoadProjectsAsync();
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await LoadProjectsAsync());

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
        if (newValue?.Id != _activeProjectContext.Current?.Id)
            _activeProjectContext.SetCurrent(newValue);

        // Only clear transient state when genuinely switching to a DIFFERENT
        // project. A same-ID reload (triggered by _projects.Changed creating
        // new object references) must NOT wipe an in-progress audit run.
        if (oldValue?.Id == newValue?.Id) return;

        DraftStatements.Clear();
        Verdicts.Clear();
        History.Clear();
        OnPropertyChanged(nameof(HasHistory));
        LatestReport = null;
        ReportMarkdown = "";
        DeepDivePrompt = "";
        StatusMessage = "";
        _loadedRecord = null;

        if (newValue is null)
        {
            Stage = VisionAuditStage.Extract;
            return;
        }

        _ = LoadExistingVisionAsync();
        _ = LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        if (SelectedProject is null) return;
        var entries = await _history.GetByProjectAsync(SelectedProject.Id);
        History.Clear();
        foreach (var e in entries) History.Add(e);
        OnPropertyChanged(nameof(HasHistory));
    }

    private async Task LoadExistingVisionAsync()
    {
        if (SelectedProject is null) return;
        _loadedRecord = await _store.GetByProjectAsync(SelectedProject.Id);
        if (_loadedRecord is null)
        {
            Stage = VisionAuditStage.Extract;
            StatusMessage = "No vision yet for this project. Extract one from the docs to begin.";
            return;
        }

        // Hydrate the editable draft from the stored record so a Re-approve
        // round-trip is non-destructive.
        DraftStatements.Clear();
        foreach (var s in _loadedRecord.Statements)
            DraftStatements.Add(new StatementEditViewModel(s.Id, s.Text));

        Stage = _loadedRecord.IsApproved
            ? VisionAuditStage.ChooseMode
            : VisionAuditStage.Approve;

        StatusMessage = _loadedRecord.IsApproved
            ? "Vision approved. Choose how deep the audit should go."
            : "Draft vision loaded — review, edit, and Approve to enable auditing.";
    }

    // ===== Stage 1: Extract ===================================================

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ExtractAsync(CancellationToken ct)
    {
        if (SelectedProject is null) { StatusMessage = "Pick a project first."; return; }
        if (IsBusy) return;

        IsBusy = true;
        StatusMessage = "Reading project docs and distilling a draft vision…";
        try
        {
            var statements = await _audit.ExtractVisionAsync(SelectedProject.FolderPath, ct);
            DraftStatements.Clear();
            foreach (var s in statements)
                DraftStatements.Add(new StatementEditViewModel(s.Id, s.Text));
            Stage = VisionAuditStage.Approve;
            StatusMessage = statements.Count == 0
                ? "No statements drafted — add some by hand before approving."
                : statements.Count + " draft statement(s). Review, edit, then Approve.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Vision extraction failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===== Stage 2: Approve ===================================================

    [RelayCommand]
    private void AddStatement()
    {
        DraftStatements.Add(new StatementEditViewModel(Guid.NewGuid(), ""));
        StatusMessage = "Added a blank statement — fill it in then Approve.";
    }

    [RelayCommand]
    private void RemoveStatement(StatementEditViewModel? statement)
    {
        if (statement is null) return;
        DraftStatements.Remove(statement);
    }

    [RelayCommand]
    private async Task ApproveAsync(CancellationToken ct)
    {
        if (SelectedProject is null) return;
        if (IsBusy) return;

        var nonEmpty = DraftStatements
            .Where(s => !string.IsNullOrWhiteSpace(s.Text))
            .ToList();
        if (nonEmpty.Count == 0)
        {
            StatusMessage = "Add at least one statement before approving.";
            return;
        }

        IsBusy = true;
        try
        {
            var record = _loadedRecord ?? new VisionRecord
            {
                ProjectId = SelectedProject.Id,
                Created = DateTimeOffset.Now,
            };
            record.Statements = nonEmpty
                .Select(s => new VisionStatement { Id = s.Id, Text = s.Text.Trim() })
                .ToList();
            record.ApprovedAt = DateTimeOffset.Now;
            record.Modified = DateTimeOffset.Now;

            await _store.SaveAsync(record, ct);
            _loadedRecord = record;
            Stage = VisionAuditStage.ChooseMode;
            StatusMessage = "Vision approved (" + nonEmpty.Count + " statement(s)). " +
                            "Pick an audit mode below.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToExtract()
    {
        Stage = VisionAuditStage.Extract;
        StatusMessage = "";
    }

    // ===== Stage 3: ChooseMode ================================================

    [RelayCommand] private void PickStructural() => SelectedMode = AuditMode.Structural;
    [RelayCommand] private void PickTargeted() => SelectedMode = AuditMode.Targeted;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunAuditAsync(CancellationToken ct)
    {
        if (SelectedProject is null || _loadedRecord is null) return;
        if (!_loadedRecord.IsApproved)
        {
            StatusMessage = "Vision isn't approved — go back and Approve first.";
            return;
        }
        if (IsBusy) return;

        IsBusy = true;
        StatusMessage = SelectedMode == AuditMode.Structural
            ? "Running quick structural audit…"
            : "Running deeper targeted audit (will read a bounded set of source files)…";
        try
        {
            var report = await _audit.AuditAsync(
                _loadedRecord, SelectedProject.FolderPath, SelectedMode, ct);

            LatestReport = report;
            Verdicts.Clear();
            foreach (var v in OrderForDisplay(report.Verdicts)) Verdicts.Add(v);

            ReportMarkdown = _audit.BuildReportMarkdown(report, SelectedProject.Name);
            DeepDivePrompt = _audit.BuildDeepDivePrompt(report, SelectedProject.Name);

            // Persist this run to the audit history so the user can revisit
            // it later. Each Add is a NEW entry — re-running never overwrites
            // an older report.
            var entry = new AuditHistoryEntry
            {
                ProjectId = SelectedProject.Id,
                Mode = report.Mode,
                OffTrackCount = OffTrackCount,
                AtRiskCount = AtRiskCount,
                OnTrackCount = OnTrackCount,
                ReportMarkdown = ReportMarkdown,
                DeepDivePrompt = DeepDivePrompt,
                Verdicts = report.Verdicts,
                GeneratedAt = report.GeneratedAt,
            };
            await _history.AddAsync(entry, ct);
            History.Insert(0, entry);
            OnPropertyChanged(nameof(HasHistory));

            Stage = VisionAuditStage.RunReview;
            StatusMessage = OffTrackCount + " off-track · " + AtRiskCount +
                            " at-risk · " + OnTrackCount + " on-track.";
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

    /// <summary>Off-track first (the real drift), then at-risk, then on-track.</summary>
    private static IEnumerable<StatementVerdict> OrderForDisplay(IEnumerable<StatementVerdict> verdicts)
        => verdicts.OrderBy(v => v.Rank switch
        {
            AlignmentRank.OffTrack => 0,
            AlignmentRank.AtRisk => 1,
            _ => 2,
        });

    [RelayCommand]
    private void BackToApprove()
    {
        Stage = VisionAuditStage.Approve;
        StatusMessage = "Re-edit the vision, then Approve again to update.";
    }

    // ===== Stage 4: RunReview =================================================

    [RelayCommand]
    private void RunAgain()
    {
        // Lets the user re-run with a different mode without leaving the
        // saved vision behind.
        Stage = VisionAuditStage.ChooseMode;
        StatusMessage = "Pick a mode and run the audit again.";
    }

    [RelayCommand]
    private void StartOver()
    {
        // Re-extract the vision from docs (project may have evolved).
        Stage = VisionAuditStage.Extract;
        StatusMessage = "Re-extracting the vision will draft new statements from " +
                        "the current docs — you'll review and approve again.";
    }

    // ===== Audit history ======================================================

    /// <summary>
    /// Load a stored history entry back into the report / deep-dive panels.
    /// The user stays on the RunReview stage; the displayed report just
    /// reflects the historical entry instead of the latest run. We replace
    /// LatestReport with a synthesised AuditReport so the summary chips
    /// reflect the loaded entry's counts.
    /// </summary>
    [RelayCommand]
    private void LoadFromHistory(AuditHistoryEntry? entry)
    {
        if (entry is null) return;

        LatestReport = new AuditReport(entry.Mode, entry.Verdicts, entry.GeneratedAt);
        Verdicts.Clear();
        foreach (var v in OrderForDisplay(entry.Verdicts)) Verdicts.Add(v);
        ReportMarkdown = entry.ReportMarkdown;
        DeepDivePrompt = entry.DeepDivePrompt;
        Stage = VisionAuditStage.RunReview;
        StatusMessage = "Loaded audit from " + entry.DisplayLabel + ".";
    }

    [RelayCommand]
    private async Task DeleteHistoryEntryAsync(AuditHistoryEntry? entry)
    {
        if (entry is null) return;
        try
        {
            await _history.RemoveAsync(entry.Id);
            History.Remove(entry);
            OnPropertyChanged(nameof(HasHistory));
            StatusMessage = "Deleted audit entry from " + entry.DisplayLabel + ".";
        }
        catch (Exception ex)
        {
            StatusMessage = "Couldn't delete that entry: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (SelectedProject is null || History.Count == 0) return;
        try
        {
            await _history.ClearProjectAsync(SelectedProject.Id);
            History.Clear();
            OnPropertyChanged(nameof(HasHistory));
            StatusMessage = "Audit history cleared for this project.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Couldn't clear history: " + ex.Message;
        }
    }

    // ===== Unified-header commands (v0.31) ====================================

    /// <summary>
    /// Clear the inputs unique to the CURRENT stage only. Does NOT change
    /// which stage is active, does NOT touch saved DB state, does NOT
    /// touch persisted history. Per-stage:
    ///   Extract     — no-op (no inputs to clear on this stage)
    ///   Approve     — clear all DraftStatements
    ///   ChooseMode  — reset SelectedMode to Structural (the default)
    ///   RunReview   — clear Verdicts + ReportMarkdown + DeepDivePrompt
    /// </summary>
    [RelayCommand]
    private void ResetCurrentStage()
    {
        if (IsBusy) return;
        switch (Stage)
        {
            case VisionAuditStage.Approve:
                DraftStatements.Clear();
                StatusMessage = "Statement list cleared. Add some or re-extract.";
                break;
            case VisionAuditStage.ChooseMode:
                SelectedMode = AuditMode.Structural;
                StatusMessage = "Audit mode reset to Structural.";
                break;
            case VisionAuditStage.RunReview:
                Verdicts.Clear();
                LatestReport = null;
                ReportMarkdown = "";
                DeepDivePrompt = "";
                StatusMessage = "Report panels cleared. Saved history is intact.";
                break;
        }
    }

    /// <summary>
    /// Discard ALL in-memory module state — drafts, verdicts, generated
    /// reports, current mode — and return to the Extract stage. The DB-
    /// persisted VisionRecord and the persisted AuditHistory survive
    /// (re-selecting the project re-hydrates the saved vision).
    /// </summary>
    [RelayCommand]
    private void RestartModule()
    {
        if (IsBusy) return;
        DraftStatements.Clear();
        Verdicts.Clear();
        LatestReport = null;
        ReportMarkdown = "";
        DeepDivePrompt = "";
        SelectedMode = AuditMode.Structural;
        _loadedRecord = null;
        Stage = VisionAuditStage.Extract;
        StatusMessage = "Reset — extract a fresh vision from the docs to begin.";
    }

    // ===== Shared ==============================================================

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (await _clipboard.SetTextAsync(text))
            StatusMessage = "Copied to clipboard.";
    }
}

/// <summary>One row in the Approve stage's editable statement list.</summary>
public sealed partial class StatementEditViewModel : ObservableObject
{
    public Guid Id { get; }
    [ObservableProperty] private string _text;

    public StatementEditViewModel(Guid id, string text)
    {
        Id = id;
        _text = text;
    }
}

/// <summary>Wizard stages for the Vision Audit.</summary>
public enum VisionAuditStage
{
    Extract,
    Approve,
    ChooseMode,
    RunReview,
}
