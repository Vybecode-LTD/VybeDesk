using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Avalonia.Threading;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using ClaudePM.Services.Testing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Module 6 — Testing Manager. Project-scoped strategy chooser and
/// Claude-Code-prompt generator.
///
/// Wizard layout follows Pattern C from
/// <c>docs/design-patterns/testing-manager-wizard-options.md</c> — a
/// stepped wizard backed by a <see cref="Steps"/> collection of
/// <see cref="QuestionViewModel"/>. The View renders ONE step at a time
/// via a <see cref="Avalonia.Controls.ContentControl"/> bound to
/// <see cref="CurrentQuestion"/>. Each step fits on screen so no
/// ScrollViewer is needed for the questionnaire (which sidesteps the
/// Avalonia ScrollViewer-in-Grid-column height-constraint bug that
/// surfaced in v0.27's first iteration — see the design-patterns doc for
/// the full analysis and alternative patterns).
///
/// Three view states:
/// <list type="bullet">
/// <item>Stepped questionnaire (no plan yet, or user clicked Re-run).</item>
/// <item>Recommendation review (user clicked "See recommendation" on the
/// last step) — shows the draft strategy with Accept/Re-answer.</item>
/// <item>Saved plan view — strategy summary, generate setup-prompt,
/// regression-prompt, bug-fixed nudge.</item>
/// </list>
/// The Bug Tracker and Testing Manager share ONLY the
/// <see cref="IBugFixedNotifier"/> event — nothing else of either module's
/// internals.
/// </summary>
public sealed partial class TestingManagerViewModel : PageViewModel
{
    private readonly ITestingPlanStore _plans;
    private readonly IProjectStore _projects;
    private readonly ITestingFrameworkCatalog _catalog;
    private readonly IBugFixedNotifier _bugFixed;
    private readonly IClipboardService _clipboard;
    private readonly IActiveProjectContext _activeProjectContext;

    public override string Title => "Testing Manager";
    public override string Glyph => "\U0001F9EA"; // 🧪
    public override string Description =>
        "Pick a testing strategy and generate Claude Code setup + regression prompts.";

    // ===== Unified module header (v0.31) ======================================
    //
    // Source-generator naming convention (see VisionAuditViewModel for the
    // detailed explanation): the [RelayCommand] methods are named differently
    // from the base virtuals (GoToFirstQuestion / ResetCurrentStage /
    // RestartModule) and forwarded via expression-bodied overrides below.

    public override IReadOnlyList<string> Breadcrumbs
    {
        get
        {
            if (!HasProject) return Array.Empty<string>();
            if (IsShowingRecommendation) return new[] { "Recommendation review" };
            if (HasPlanViewVisible) return new[] { "Saved plan" };
            // Questionnaire (initial OR re-run): use the current question's
            // title if present, else a generic step label.
            var q = CurrentQuestion;
            if (q is not null) return new[] { q.Title };
            return new[] { "Question " + (CurrentStepIndex + 1) };
        }
    }

    public override IRelayCommand? GoModuleHomeCommand => GoToFirstQuestionCommand;
    public override IRelayCommand? ResetCommand        => ResetCurrentStageCommand;
    public override IRelayCommand? RestartCommand      => RestartModuleCommand;

    public ObservableCollection<Project> Projects { get; } = new();

    /// <summary>The 5 wizard steps. Populated once in the ctor — not rebuilt on project change.</summary>
    public ObservableCollection<QuestionViewModel> Steps { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]
    private Project? _selectedProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlan))]
    [NotifyPropertyChangedFor(nameof(IsQuestionnaireVisible))]
    [NotifyPropertyChangedFor(nameof(HasPlanViewVisible))]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]
    private TestingPlan? _currentPlan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQuestionnaireVisible))]
    [NotifyPropertyChangedFor(nameof(HasPlanViewVisible))]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]
    private bool _isReRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQuestionnaireVisible))]
    [NotifyPropertyChangedFor(nameof(IsRecommendationReviewVisible))]
    [NotifyPropertyChangedFor(nameof(HasPlanViewVisible))]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]
    private bool _isShowingRecommendation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentQuestion))]
    [NotifyPropertyChangedFor(nameof(IsFirstStep))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepLabel))]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]
    private int _currentStepIndex;

    [ObservableProperty] private StrategyRecommendation? _draftRecommendation;
    [ObservableProperty] private string _setupPromptOutput = "";
    [ObservableProperty] private string _regressionPromptOutput = "";
    [ObservableProperty] private string _bugFixedNudge = "";
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;
    public bool HasProject => SelectedProject is not null;
    public bool HasPlan => CurrentPlan is not null;

    public QuestionViewModel? CurrentQuestion
        => CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count
            ? Steps[CurrentStepIndex]
            : null;

    public bool IsFirstStep => CurrentStepIndex <= 0;
    public bool IsLastStep => Steps.Count > 0 && CurrentStepIndex >= Steps.Count - 1;
    public string StepLabel => Steps.Count > 0
        ? "Step " + (CurrentStepIndex + 1) + " of " + Steps.Count
        : "";

    /// <summary>All five questions answered.</summary>
    public bool IsAnswersComplete => Steps.Count > 0 && Steps.All(s => s.IsAnswered);

    public bool IsQuestionnaireVisible
        => HasProject && (CurrentPlan is null || IsReRunning) && !IsShowingRecommendation;

    public bool IsRecommendationReviewVisible
        => HasProject && IsShowingRecommendation;

    public bool HasPlanViewVisible
        => HasProject && CurrentPlan is not null && !IsReRunning && !IsShowingRecommendation;

    /// <summary>
    /// Set when <see cref="IBugFixedNotifier"/> fires for the currently
    /// selected project. Lets the regression-prompt command name the
    /// specific bug. Transient — overwritten on the next fix event,
    /// cleared on Dismiss.
    /// </summary>
    private BugFixedEvent? _pendingFixedBug;

    public TestingManagerViewModel(
        ITestingPlanStore plans,
        IProjectStore projects,
        ITestingFrameworkCatalog catalog,
        IBugFixedNotifier bugFixed,
        IClipboardService clipboard,
        IActiveProjectContext activeProjectContext)
    {
        _plans = plans;
        _projects = projects;
        _catalog = catalog;
        _bugFixed = bugFixed;
        _clipboard = clipboard;
        _activeProjectContext = activeProjectContext;

        BuildSteps();

        _projects.Changed += OnProjectsChanged;
        _bugFixed.BugFixed += OnBugFixed;

        _ = LoadProjectsAsync();
    }

    // The five wizard questions, populated once. Tokens here MUST match the
    // strings the StrategySelector and FriendlyXxx mappers expect — adding
    // a token without updating the selector silently breaks the
    // recommendation prose.
    private void BuildSteps()
    {
        Steps.Add(new QuestionViewModel(
            "1. What are you building?",
            new[]
            {
                new QuestionOption("Library",     "Library or API"),
                new QuestionOption("Desktop",     "Desktop application"),
                new QuestionOption("WebFrontend", "Web frontend"),
                new QuestionOption("CLI",         "Command-line tool"),
                new QuestionOption("Mixed",       "Mixed / not sure"),
            }));

        Steps.Add(new QuestionViewModel(
            "2. What language / ecosystem?",
            new[]
            {
                new QuestionOption("DotNet",     ".NET (C#)"),
                new QuestionOption("Python",     "Python"),
                new QuestionOption("JavaScript", "JavaScript / TypeScript"),
                new QuestionOption("Cpp",        "C++"),
                new QuestionOption("Other",      "Something else / mixed"),
            }));

        Steps.Add(new QuestionViewModel(
            "3. How important is correctness?",
            new[]
            {
                new QuestionOption("Critical",  "Critical — people or money depend on it"),
                new QuestionOption("Important", "Important — mistakes cost time"),
                new QuestionOption("Personal",  "Personal — fine if it breaks"),
            }));

        Steps.Add(new QuestionViewModel(
            "4. Who works on the code?",
            new[]
            {
                new QuestionOption("Solo",       "Just me"),
                new QuestionOption("SmallTeam",  "A small team (2 – 5)"),
                new QuestionOption("LargerTeam", "A larger team"),
            }));

        Steps.Add(new QuestionViewModel(
            "5. Does the code touch external systems (databases, APIs, file system, network)?",
            new[]
            {
                new QuestionOption("Heavy", "Heavily — databases, APIs, file system, network everywhere"),
                new QuestionOption("Some",  "In some places"),
                new QuestionOption("None",  "No — it's pure in-process logic"),
            }));

        // Listen for IsAnswered changes on every step so IsAnswersComplete
        // and the Next button enable-state stay in sync.
        foreach (var step in Steps)
            step.PropertyChanged += OnStepChanged;
    }

    private void OnStepChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuestionViewModel.IsAnswered)
         || e.PropertyName == nameof(QuestionViewModel.SelectedToken))
        {
            OnPropertyChanged(nameof(IsAnswersComplete));
            // The Next button binds to CurrentQuestion.IsAnswered through
            // the data template; CurrentQuestion is the source object so
            // its own PropertyChanged is what the View sees. Nothing else
            // needed here for that — just IsAnswersComplete for the
            // last-step "See recommendation" enable state.
        }
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await LoadProjectsAsync());

    private void OnBugFixed(BugFixedEvent evt)
        => Dispatcher.UIThread.Post(() =>
        {
            if (SelectedProject is null || evt.ProjectId != SelectedProject.Id) return;
            _pendingFixedBug = evt;
            BugFixedNudge =
                "A bug was just marked Fixed in this project: \"" + evt.Title + "\". " +
                "Click \"Generate regression-test prompt\" to draft a test that would " +
                "catch it if it returned.";
        });

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
        // M4 #16: push the selection through the cross-cutting context so
        // the AI service picks up this project's Model override on its next
        // call.
        _activeProjectContext.SetCurrent(value);

        // Project switch: clear all transient wizard / output state so each
        // project starts from a clean slate.
        ResetWizard();
        SetupPromptOutput = "";
        RegressionPromptOutput = "";
        BugFixedNudge = "";
        DraftRecommendation = null;
        _pendingFixedBug = null;
        IsReRunning = false;
        IsShowingRecommendation = false;

        _ = LoadPlanAsync();
    }

    private async Task LoadPlanAsync()
    {
        if (SelectedProject is null) { CurrentPlan = null; return; }

        CurrentPlan = await _plans.GetByProjectAsync(SelectedProject.Id);

        // If a plan already exists, pre-fill the wizard with its stored
        // answers so a Re-run starts from the user's previous choices
        // rather than blank.
        if (CurrentPlan is not null)
        {
            var a = CurrentPlan.Answers;
            SetStepToken(0, a.ProjectKind);
            SetStepToken(1, a.Language);
            SetStepToken(2, a.Criticality);
            SetStepToken(3, a.TeamSize);
            SetStepToken(4, a.ExternalSystems);
        }
    }

    private void SetStepToken(int index, string token)
    {
        if (index < 0 || index >= Steps.Count) return;
        Steps[index].PickCommand.Execute(token);
    }

    private void ResetWizard()
    {
        CurrentStepIndex = 0;
        foreach (var step in Steps)
            step.PickCommand.Execute("");
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStepIndex > 0) CurrentStepIndex--;
    }

    [RelayCommand]
    private void GoNext()
    {
        if (CurrentStepIndex < Steps.Count - 1) CurrentStepIndex++;
    }

    /// <summary>
    /// Last step's "See recommendation" command. Builds the draft strategy
    /// from current answers and flips into recommendation-review state.
    /// </summary>
    [RelayCommand]
    private void SeeRecommendation()
    {
        if (!IsAnswersComplete) return;
        DraftRecommendation = StrategySelector.Recommend(BuildAnswers(), _catalog);
        IsShowingRecommendation = true;
    }

    [RelayCommand]
    private void ReAnswer()
    {
        // Go back to the questionnaire without losing the answers — the
        // user can tweak before re-submitting.
        IsShowingRecommendation = false;
    }

    private QuestionnaireAnswers BuildAnswers() => new()
    {
        ProjectKind = Steps[0].SelectedToken,
        Language = Steps[1].SelectedToken,
        Criticality = Steps[2].SelectedToken,
        TeamSize = Steps[3].SelectedToken,
        ExternalSystems = Steps[4].SelectedToken,
    };

    [RelayCommand]
    private async Task AcceptRecommendationAsync()
    {
        if (SelectedProject is null || DraftRecommendation is null) return;
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            // Preserve Id and Created across re-runs so the user's
            // strategy history (when telemetry lands in M3) stays linked.
            var existing = CurrentPlan;
            var plan = new TestingPlan
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                ProjectId = SelectedProject.Id,
                StrategySummary = DraftRecommendation.Summary,
                Frameworks = DraftRecommendation.Frameworks,
                Kinds = DraftRecommendation.Kinds,
                Answers = BuildAnswers(),
                Created = existing?.Created ?? DateTimeOffset.Now,
                Modified = DateTimeOffset.Now,
            };

            await _plans.SaveAsync(plan);
            CurrentPlan = plan;
            IsReRunning = false;
            IsShowingRecommendation = false;
            StatusMessage = existing is null
                ? "Strategy saved."
                : "Strategy updated.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void StartReRun()
    {
        if (CurrentPlan is null) return;
        IsReRunning = true;
        IsShowingRecommendation = false;
        CurrentStepIndex = 0;
        StatusMessage =
            "Re-running the questionnaire — your previous answers are pre-filled. " +
            "Adjust what's changed and Accept to update the strategy.";
    }

    [RelayCommand]
    private void CancelReRun()
    {
        if (!IsReRunning && !IsShowingRecommendation) return;
        IsReRunning = false;
        IsShowingRecommendation = false;
        _ = LoadPlanAsync();
        StatusMessage = "Re-run cancelled.";
    }

    [RelayCommand]
    private void GenerateSetupPrompt()
    {
        if (CurrentPlan is null || SelectedProject is null) return;

        var sb = new StringBuilder();
        sb.AppendLine("# Testing setup — " + SelectedProject.Name);
        sb.AppendLine();
        sb.AppendLine("Strategy summary:");
        sb.AppendLine();
        sb.AppendLine(IndentLines(CurrentPlan.StrategySummary));
        sb.AppendLine();
        sb.AppendLine("Set up the frameworks below in order. Each section is a " +
                      "self-contained prompt you can paste into a fresh Claude Code " +
                      "session against this project — work through them one at a time " +
                      "and confirm each is green before moving on.");
        sb.AppendLine();

        var anyFound = false;
        for (int i = 0; i < CurrentPlan.Frameworks.Count; i++)
        {
            var name = CurrentPlan.Frameworks[i];
            var fw = _catalog.ByName(name);
            if (fw is null) continue;
            anyFound = true;

            sb.AppendLine("## " + (i + 1) + ". " + fw.Name);
            if (!string.IsNullOrWhiteSpace(fw.Note))
            {
                sb.AppendLine();
                sb.AppendLine("> " + fw.Note);
            }
            sb.AppendLine();
            sb.AppendLine(SubstituteTemplate(fw.SetupPromptTemplate, SelectedProject));
            sb.AppendLine();
        }

        if (!anyFound)
        {
            sb.AppendLine("(No frameworks in the strategy matched the catalog. The " +
                          "kinds of testing recommended above still apply — pick the " +
                          "standard tools for your language and stack.)");
        }

        SetupPromptOutput = sb.ToString().TrimEnd();
        StatusMessage = "Setup prompt generated — copy it into Claude Code.";
    }

    [RelayCommand]
    private void GenerateRegressionPrompt()
    {
        if (SelectedProject is null) return;

        var sb = new StringBuilder();
        sb.AppendLine("# Regression test — " + SelectedProject.Name);
        sb.AppendLine();

        if (_pendingFixedBug is not null)
        {
            sb.AppendLine("Write a regression test for the bug below. The test MUST " +
                          "fail on the broken code (pre-fix behaviour) and PASS on the " +
                          "current code (post-fix behaviour). Place it alongside the " +
                          "project's existing tests, following the layout the test " +
                          "framework already uses.");
            sb.AppendLine();
            sb.AppendLine("Bug: **" + _pendingFixedBug.Title + "**");
            sb.AppendLine();
            sb.AppendLine("Reproduction steps, expected behaviour, and actual " +
                          "behaviour are recorded in the ClaudePM Bug Tracker for " +
                          "this project. Open the Bug Tracker tab, find this bug, " +
                          "and use its reproduction trio as the basis for the test " +
                          "case — do not guess. If the reproduction steps are not " +
                          "concrete enough to write a deterministic test, stop and " +
                          "report what's missing.");
        }
        else
        {
            sb.AppendLine("Write a regression test for the bug(s) most recently " +
                          "fixed in this project. The tests MUST fail on the " +
                          "pre-fix code and PASS on the current code.");
            sb.AppendLine();
            sb.AppendLine("Open the ClaudePM Bug Tracker for this project, find " +
                          "the bug(s) marked Fixed most recently, and use each one's " +
                          "Steps to Reproduce / Expected / Actual fields as the basis " +
                          "for the regression test. Place tests alongside the project's " +
                          "existing test layout, following the framework already in use.");
        }

        sb.AppendLine();
        sb.AppendLine("Do NOT add coverage-number checks or refactor unrelated code. " +
                      "The goal is one tight test per bug that would catch a regression " +
                      "the next time someone changes nearby code.");

        RegressionPromptOutput = sb.ToString().TrimEnd();
        StatusMessage = "Regression prompt generated — copy it into Claude Code.";
    }

    [RelayCommand]
    private void DismissBugFixedNudge()
    {
        BugFixedNudge = "";
        _pendingFixedBug = null;
    }

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (await _clipboard.SetTextAsync(text))
            StatusMessage = "Copied to clipboard.";
    }

    private static string SubstituteTemplate(string template, Project p)
        => template
            .Replace("{{ProjectName}}", p.Name)
            .Replace("{{ProjectPath}}",
                string.IsNullOrWhiteSpace(p.FolderPath) ? "<your project folder>" : p.FolderPath);

    private static string IndentLines(string body)
        => string.IsNullOrWhiteSpace(body)
            ? "    (no summary)"
            : string.Join(Environment.NewLine,
                body.Replace("\r\n", "\n").Split('\n').Select(l => "    " + l));

    // ===== Unified-header commands (v0.31) ====================================

    /// <summary>
    /// Jump back to the first question of the questionnaire WITHOUT clearing
    /// any answers. If currently on the recommendation review or the saved
    /// plan view, drops back into the questionnaire at question 1 with the
    /// existing answers intact. Matches the GoModuleHome semantic.
    /// </summary>
    [RelayCommand]
    private void GoToFirstQuestion()
    {
        if (!HasProject) return;
        IsShowingRecommendation = false;
        // If a plan is saved, treat this as a re-run so the questionnaire
        // is the visible state (HasPlanViewVisible would otherwise hide it).
        if (CurrentPlan is not null) IsReRunning = true;
        CurrentStepIndex = 0;
        StatusMessage = "Back to question 1 — your answers are preserved.";
    }

    /// <summary>
    /// Clear the inputs unique to the CURRENT view. Does NOT change which
    /// top-level state is active. Per-state:
    ///   Questionnaire             — clear the CURRENT question's selected token only.
    ///   Recommendation review     — no-op (nothing to reset; the draft is recomputed on re-entry).
    ///   Saved plan view           — clear SetupPromptOutput / RegressionPromptOutput.
    /// </summary>
    [RelayCommand]
    private void ResetCurrentStage()
    {
        if (IsBusy) return;
        if (IsQuestionnaireVisible)
        {
            var q = CurrentQuestion;
            if (q is not null)
            {
                q.PickCommand.Execute("");
                StatusMessage = "Current answer cleared.";
            }
        }
        else if (HasPlanViewVisible)
        {
            SetupPromptOutput = "";
            RegressionPromptOutput = "";
            StatusMessage = "Generated prompts cleared. The saved plan is intact.";
        }
        // Recommendation review: deliberate no-op.
    }

    /// <summary>
    /// Clear ALL answers across every question, dismiss the recommendation /
    /// plan view, and return to question 1. Does NOT delete the persisted
    /// TestingPlan from the DB — re-selecting the project reloads it (which
    /// matches the pattern in the other modules: persisted history survives
    /// in-memory resets). The bug-fixed nudge is dismissed too.
    /// </summary>
    [RelayCommand]
    private void RestartModule()
    {
        if (IsBusy) return;
        ResetWizard();
        IsReRunning = HasPlan;       // If a plan exists, show the questionnaire as a re-run.
        IsShowingRecommendation = false;
        SetupPromptOutput = "";
        RegressionPromptOutput = "";
        DraftRecommendation = null;
        BugFixedNudge = "";
        _pendingFixedBug = null;
        StatusMessage = "Reset — answer the questions again to pick a strategy.";
    }
}
