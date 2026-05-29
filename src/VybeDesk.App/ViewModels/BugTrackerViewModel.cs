using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VybeDesk.App.ViewModels;

/// <summary>
/// Module 5 — Bug Tracker. Project-scoped defect log: every bug belongs to
/// exactly one project, and the tracker only ever shows bugs for the
/// currently-selected project. Bugs are NOT a flat notepad: the list sorts
/// by severity (Critical → Major → Minor), and the editor's three reproduction
/// fields stay distinct to teach reproducible reporting.
/// </summary>
public sealed partial class BugTrackerViewModel : PageViewModel
{
    private static readonly BugSeverity[] SeverityValues =
        (BugSeverity[])Enum.GetValues(typeof(BugSeverity));
    private static readonly BugStatus[] StatusValues =
        (BugStatus[])Enum.GetValues(typeof(BugStatus));

    private readonly IBugStore _bugs;
    private readonly IProjectStore _projects;
    private readonly IClipboardService _clipboard;
    private readonly IBugFixedNotifier _bugFixedNotifier;
    private readonly IActiveProjectContext _activeProjectContext;
    private bool _reloadingProjects;

    /// <summary>
    /// Module-local project memory. Survives the null pulse from
    /// ContentPresenter detachment and is NOT overwritten by other modules'
    /// project selections. <see cref="OnActivated"/> restores from this
    /// field — not from <see cref="IActiveProjectContext.Current"/> — so
    /// each module keeps its own independent selection (project isolation).
    /// </summary>
    private Guid? _lastSelectedProjectId;

    public override string Title => "Bug Tracker";
    public override string Glyph => "\U0001F41E"; // 🐞
    public override string Description =>
        "Track project-scoped bugs by severity and reproduce-ability.";

    public ObservableCollection<Project> Projects { get; } = new();
    public ObservableCollection<Bug> Bugs { get; } = new();
    public ObservableCollection<Bug> SelectedBugs { get; } = new();

    public IReadOnlyList<BugSeverity> Severities => SeverityValues;
    public IReadOnlyList<BugStatus> Statuses => StatusValues;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    private Project? _selectedProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Bug? _selectedBug;

    [ObservableProperty] private string _editTitle = "";
    [ObservableProperty] private BugSeverity _editSeverity = BugSeverity.Major;
    [ObservableProperty] private BugStatus _editStatus = BugStatus.Open;
    [ObservableProperty] private string _editStepsToReproduce = "";
    [ObservableProperty] private string _editExpectedResult = "";
    [ObservableProperty] private string _editActualResult = "";
    [ObservableProperty] private string _editArea = "";

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _fixPromptOutput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _majorCount;
    [ObservableProperty] private int _minorCount;

    public bool IsNotBusy => !IsBusy;
    public bool HasProject => SelectedProject is not null;
    public bool HasSelection => SelectedBug is not null;

    public BugTrackerViewModel(
        IBugStore bugs, IProjectStore projects, IClipboardService clipboard,
        IBugFixedNotifier bugFixedNotifier, IActiveProjectContext activeProjectContext)
    {
        _bugs = bugs;
        _projects = projects;
        _clipboard = clipboard;
        _bugFixedNotifier = bugFixedNotifier;
        _activeProjectContext = activeProjectContext;
        _projects.Changed += OnProjectsChanged;
        _ = LoadProjectsAsync();
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await LoadProjectsAsync());

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

        // Only reset the bug list on genuine project switch, not same-ID refresh.
        if (oldValue?.Id == newValue?.Id) return;

        SelectedBug = null;
        FixPromptOutput = "";
        _ = ReloadBugsAsync();
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

    private async Task ReloadBugsAsync()
    {
        Bugs.Clear();
        CriticalCount = MajorCount = MinorCount = 0;

        if (SelectedProject is null) return;

        var rows = await _bugs.GetByProjectAsync(SelectedProject.Id);
        foreach (var b in SortForDisplay(rows)) Bugs.Add(b);

        CriticalCount = rows.Count(b => b.Severity == BugSeverity.Critical);
        MajorCount = rows.Count(b => b.Severity == BugSeverity.Major);
        MinorCount = rows.Count(b => b.Severity == BugSeverity.Minor);
    }

    /// <summary>
    /// Severity first (Critical → Major → Minor); within a severity, Open and
    /// Fixing rise above Fixed and WontFix; ties broken by newest-first
    /// creation. Answers "what should I fix next?" by reading top-down.
    /// </summary>
    private static IEnumerable<Bug> SortForDisplay(IEnumerable<Bug> source)
        => source
            .OrderBy(b => (int)b.Severity)
            .ThenBy(b => IsClosed(b.Status) ? 1 : 0)
            .ThenByDescending(b => b.Created);

    private static bool IsClosed(BugStatus s) => s is BugStatus.Fixed or BugStatus.WontFix;

    partial void OnSelectedBugChanged(Bug? value)
    {
        if (value is null)
        {
            EditTitle = "";
            EditSeverity = BugSeverity.Major;
            EditStatus = BugStatus.Open;
            EditStepsToReproduce = EditExpectedResult = EditActualResult = EditArea = "";
            return;
        }

        EditTitle = value.Title;
        EditSeverity = value.Severity;
        EditStatus = value.Status;
        EditStepsToReproduce = value.StepsToReproduce;
        EditExpectedResult = value.ExpectedResult;
        EditActualResult = value.ActualResult;
        EditArea = value.Area;
    }

    [RelayCommand]
    private async Task NewBugAsync()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Pick a project first — bugs live inside a project.";
            return;
        }

        var bug = new Bug
        {
            ProjectId = SelectedProject.Id,
            Title = "Untitled bug",
            Severity = BugSeverity.Major,
            Status = BugStatus.Open,
        };
        await _bugs.AddAsync(bug);
        await ReloadBugsAsync();
        SelectedBug = Bugs.FirstOrDefault(b => b.Id == bug.Id);
        StatusMessage = "New bug created — fill in the reproduction fields.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedBug is null) return;
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            var previousStatus = SelectedBug.Status;
            SelectedBug.Title = string.IsNullOrWhiteSpace(EditTitle)
                ? "Untitled bug"
                : EditTitle.Trim();
            SelectedBug.Severity = EditSeverity;
            SelectedBug.Status = EditStatus;
            SelectedBug.StepsToReproduce = EditStepsToReproduce;
            SelectedBug.ExpectedResult = EditExpectedResult;
            SelectedBug.ActualResult = EditActualResult;
            SelectedBug.Area = EditArea.Trim();

            await _bugs.UpdateAsync(SelectedBug);

            // Detect the transition BEFORE clearing the selection so we can
            // both nudge the user and ping the Testing Manager via the
            // cross-module IBugFixedNotifier. The notifier is the only thing
            // the Bug Tracker shares with the Testing Manager — deliberately
            // tiny so the two modules stay loosely coupled.
            var transitionedToFixed = previousStatus != BugStatus.Fixed
                                    && EditStatus == BugStatus.Fixed;
            var firedBugCopy = SelectedBug;  // capture before clearing

            // Clear the form so the user can immediately log the next bug.
            // Bug entry is typically a batch workflow (QA pass, screenshot
            // review), so keep-the-current-bug-selected feels wrong here —
            // unlike Prompts/Projects which are individually-edited objects.
            SelectedBug = null;
            await ReloadBugsAsync();

            if (transitionedToFixed)
            {
                _bugFixedNotifier.Notify(new BugFixedEvent(
                    firedBugCopy.ProjectId, firedBugCopy.Id, firedBugCopy.Title));
            }

            StatusMessage = transitionedToFixed
                ? "Saved. Is there a test that would catch this bug if it returned? " +
                  "(The Testing Manager has been notified.)"
                : "Saved — form cleared. Click 'New bug' to log the next one.";
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
    private async Task DeleteAsync()
    {
        if (SelectedBug is null) return;
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            await _bugs.RemoveAsync(SelectedBug.Id);
            SelectedBug = null;
            await ReloadBugsAsync();
            StatusMessage = "Bug deleted.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Delete failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (await _clipboard.SetTextAsync(text))
            StatusMessage = "Copied to clipboard.";
    }

    /// <summary>
    /// Build a Claude Code fix prompt from the currently selected bugs (or all
    /// open bugs if nothing is multi-selected). Bugs are ordered by severity
    /// and rendered with their full reproduction trio, so the agent has
    /// enough to act safely. The instruction block tells the agent to make
    /// the smallest correct change per bug and to flag rather than guess.
    /// </summary>
    [RelayCommand]
    private void GenerateFixPrompt()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Pick a project first.";
            return;
        }

        var pool = SelectedBugs.Count > 0
            ? SelectedBugs.ToList()
            : Bugs.Where(b => !IsClosed(b.Status)).ToList();

        if (pool.Count == 0)
        {
            StatusMessage = "No bugs to include in a fix prompt.";
            return;
        }

        var ordered = SortForDisplay(pool).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("# Bug fix task — " + SelectedProject.Name);
        sb.AppendLine();
        sb.AppendLine("Fix the bugs below. Order them by severity " +
                      "(Critical → Major → Minor) and within a severity by the order listed.");
        sb.AppendLine();
        sb.AppendLine("For each bug:");
        sb.AppendLine("- Reproduce locally using the listed steps before changing code.");
        sb.AppendLine("- Make the smallest correct change. Do not refactor unrelated code.");
        sb.AppendLine("- If you cannot reproduce the bug, do NOT guess — stop and report " +
                      "what you tried and what you observed.");
        sb.AppendLine("- After fixing, note whether a regression test exists; if not, " +
                      "say so explicitly so the user can decide whether to add one.");
        sb.AppendLine();

        for (int i = 0; i < ordered.Count; i++)
        {
            var b = ordered[i];
            sb.AppendLine("## " + (i + 1) + ". [" + b.Severity + "] " + b.Title);
            if (!string.IsNullOrWhiteSpace(b.Area))
                sb.AppendLine("**Area:** " + b.Area);
            sb.AppendLine("**Status:** " + b.Status);
            sb.AppendLine();
            sb.AppendLine("**Steps to reproduce:**");
            sb.AppendLine(Indent(b.StepsToReproduce));
            sb.AppendLine();
            sb.AppendLine("**Expected:**");
            sb.AppendLine(Indent(b.ExpectedResult));
            sb.AppendLine();
            sb.AppendLine("**Actual:**");
            sb.AppendLine(Indent(b.ActualResult));
            sb.AppendLine();
        }

        FixPromptOutput = sb.ToString().TrimEnd();
        StatusMessage = ordered.Count + " bug(s) packed into a fix prompt — copy it into Claude Code.";
    }

    private static string Indent(string body)
        => string.IsNullOrWhiteSpace(body)
            ? "    (not provided)"
            : string.Join(Environment.NewLine,
                body.Replace("\r\n", "\n").Split('\n').Select(l => "    " + l));
}
