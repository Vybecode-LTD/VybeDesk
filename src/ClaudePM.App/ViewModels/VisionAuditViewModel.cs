using System.Collections.ObjectModel;
using Avalonia.Threading;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

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

    public VisionAuditViewModel(
        IVisionStore store,
        IVisionAuditService audit,
        IAuditHistoryStore history,
        IProjectStore projects,
        IClipboardService clipboard)
    {
        _store = store;
        _audit = audit;
        _history = history;
        _projects = projects;
        _clipboard = clipboard;

        _projects.Changed += OnProjectsChanged;
        _ = LoadProjectsAsync();
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await LoadProjectsAsync());

    private async Task LoadProjectsAsync()
    {
        var keepId = SelectedProject?.Id;
        var all = await _projects.GetAllAsync();
        Projects.Clear();
        foreach (var p in all) Projects.Add(p);

        SelectedProject = keepId is not null
            ? Projects.FirstOrDefault(p => p.Id == keepId) ?? Projects.FirstOrDefault()
            : Projects.FirstOrDefault();
    }

    partial void OnSelectedProjectChanged(Project? value)
    {
        // Clear all transient state when switching projects so each project
        // starts from a clean slate. Then load any existing vision record
        // and the project's audit history.
        DraftStatements.Clear();
        Verdicts.Clear();
        History.Clear();
        OnPropertyChanged(nameof(HasHistory));
        LatestReport = null;
        ReportMarkdown = "";
        DeepDivePrompt = "";
        StatusMessage = "";
        _loadedRecord = null;

        if (value is null)
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
