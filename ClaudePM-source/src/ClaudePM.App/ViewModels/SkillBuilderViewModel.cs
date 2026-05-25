using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Skill Builder — the Phase 2 sub-page of the Skills section. Walks the
/// user through designing a new Claude skill from a name + rough idea + notes,
/// optionally with an AI-driven clarifying-question pass first, and emits
/// the finished skill in BOTH output forms (flat <c>.skill</c> file and a
/// <c>&lt;name&gt;/SKILL.md</c> folder). Validation and serialization are
/// shared with the Skill Manager via <see cref="ISkillBuilderService"/>'s
/// delegation to <see cref="ISkillLibraryService"/>.
///
/// Five stages: <see cref="BuilderStage.Inputs"/> →
/// (optional <see cref="BuilderStage.Questions"/>) →
/// <see cref="BuilderStage.Review"/> →
/// <see cref="BuilderStage.Emitted"/>. The Draft phase is a transient
/// busy state inside the AI call and doesn't get its own stage UI.
/// </summary>
public sealed partial class SkillBuilderViewModel : PageViewModel
{
    private readonly ISkillBuilderService _builder;
    private readonly IFilePickerService _picker;
    private readonly IClipboardService _clipboard;

    public override string Title => "Skill Builder";
    public override string Glyph => "\U0001F528"; // 🔨
    public override string Description =>
        "Design a new skill from a name + rough idea + notes, with optional AI clarifying questions.";

    // ===== Unified module header (v0.31) ======================================
    //
    // Same source-generator-naming convention as VisionAuditViewModel: the
    // [RelayCommand] methods cannot be named GoModuleHome / Reset / Restart
    // because the source generator would emit *Command auto-properties that
    // hide the PageViewModel virtuals without `override`. Use distinct method
    // names (here: GoToInputsStage / ResetCurrentStage / RestartModule) and
    // forward via expression-bodied overrides below.

    public override IReadOnlyList<string> Breadcrumbs => Stage switch
    {
        BuilderStage.Inputs    => new[] { "Step 1 — Inputs" },
        BuilderStage.Questions => new[] { "Step 2 — Clarifying questions" },
        BuilderStage.Review    => new[] { "Step 3 — Review draft" },
        BuilderStage.Emitted   => new[] { "Step 4 — Emitted" },
        _ => Array.Empty<string>(),
    };

    public override IRelayCommand? GoModuleHomeCommand => GoToInputsStageCommand;
    public override IRelayCommand? ResetCommand        => ResetCurrentStageCommand;
    public override IRelayCommand? RestartCommand      => RestartModuleCommand;

    // --- Stage 1: inputs ----------------------------------------------------

    [ObservableProperty] private string _inputName = "";
    [ObservableProperty] private string _inputRoughDescription = "";
    [ObservableProperty] private string _inputNotes = "";

    /// <summary>
    /// Drives the optional clarifying-questions stage. UI label deliberately
    /// names this "Ask me clarifying questions first" — calling it "research"
    /// would mislabel the capability since the app has no web access.
    /// </summary>
    [ObservableProperty] private bool _researchOn;

    // --- Stage 2 (optional): clarifying questions ---------------------------

    /// <summary>One bound row in the questions stage.</summary>
    public sealed partial class QuestionAnswerVm : ObservableObject
    {
        public string Question { get; }
        [ObservableProperty] private string _answer = "";

        public QuestionAnswerVm(string question) { Question = question; }
    }

    public ObservableCollection<QuestionAnswerVm> Questions { get; } = new();

    // --- Stage 3: review the drafted skill ----------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDraft))]
    [NotifyPropertyChangedFor(nameof(DraftDescription))]
    [NotifyPropertyChangedFor(nameof(DraftBody))]
    private SkillFile? _draft;

    public ObservableCollection<Finding> Findings { get; } = new();

    public bool HasDraft => Draft is not null;
    public string DraftDescription => Draft?.Description ?? "";
    public string DraftBody => Draft?.Body ?? "";

    // --- Stage 4: emit ------------------------------------------------------

    [ObservableProperty] private SkillEmitResult? _emitResult;

    // --- Stage state machine ------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputsStage))]
    [NotifyPropertyChangedFor(nameof(IsQuestionsStage))]
    [NotifyPropertyChangedFor(nameof(IsReviewStage))]
    [NotifyPropertyChangedFor(nameof(IsEmittedStage))]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]
    private BuilderStage _stage = BuilderStage.Inputs;

    public bool IsInputsStage    => Stage == BuilderStage.Inputs;
    public bool IsQuestionsStage => Stage == BuilderStage.Questions;
    public bool IsReviewStage    => Stage == BuilderStage.Review;
    public bool IsEmittedStage   => Stage == BuilderStage.Emitted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;
    public bool IsNotBusy => !IsBusy;

    [ObservableProperty] private string _statusMessage = "";

    public SkillBuilderViewModel(
        ISkillBuilderService builder,
        IFilePickerService picker,
        IClipboardService clipboard)
    {
        _builder = builder;
        _picker = picker;
        _clipboard = clipboard;
    }

    private SkillBuilderInputs CurrentInputs()
        => new(InputName.Trim(), InputRoughDescription.Trim(), InputNotes.Trim());

    /// <summary>
    /// Name regex matches the Skill Library's frontmatter convention —
    /// lowercase letters/digits with hyphens between words.
    /// </summary>
    private static readonly Regex NameRx =
        new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>
    /// Pre-flight validation of the Stage 1 inputs. Returns human-readable
    /// reasons the inputs aren't ready — empty list means OK to send to the
    /// AI. Catches the case where the user types something like "I don't
    /// know, just testing" and clicks Continue, which would cause Claude
    /// to reply conversationally and produce a confusing JSON parse error
    /// further down the pipeline.
    /// </summary>
    private static IReadOnlyList<string> ValidateInputs(SkillBuilderInputs i)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(i.Name))
            issues.Add("Name is required.");
        else if (i.Name.Length < 3)
            issues.Add("Name is too short (use at least 3 characters).");
        else if (!NameRx.IsMatch(i.Name))
            issues.Add("Name must be lowercase letters/digits with hyphens (e.g. 'my-skill').");
        else if (i.Name.Contains("claude", StringComparison.OrdinalIgnoreCase))
            issues.Add("Avoid 'claude' in the name — it's reserved.");

        if (string.IsNullOrWhiteSpace(i.RoughDescription))
            issues.Add("Rough description is required.");
        else if (i.RoughDescription.Length < 40)
            issues.Add("Rough description is too thin — say a bit more about what " +
                       "the skill does and when it should trigger (40+ characters).");

        return issues;
    }

    /// <summary>
    /// Set to true on the FIRST submission with all clarifying answers
    /// blank. The user can click Draft again to proceed anyway — a soft
    /// warning, not a hard block. Reset when answers change or the user
    /// backs out.
    /// </summary>
    private bool _emptyAnswersWarned;

    /// <summary>
    /// "Continue" from the inputs stage. Branches on the research toggle:
    /// ON → fetch clarifying questions and move to the questions stage;
    /// OFF → draft directly and move to review.
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task ContinueFromInputsAsync(CancellationToken ct)
    {
        if (IsBusy) return;
        var inputs = CurrentInputs();
        var issues = ValidateInputs(inputs);
        if (issues.Count > 0)
        {
            // Surface the list of problems before any AI call. Without this
            // pre-flight, vague inputs reach Claude which replies in prose,
            // then the JSON parser fails with an opaque message.
            StatusMessage = "Fix these first: " + string.Join(" • ", issues);
            return;
        }

        if (ResearchOn)
        {
            IsBusy = true;
            StatusMessage = "Asking Claude for clarifying questions…";
            try
            {
                var qs = await _builder.GenerateClarifyingQuestionsAsync(inputs, ct);
                Questions.Clear();
                foreach (var q in qs) Questions.Add(new QuestionAnswerVm(q));
                Stage = BuilderStage.Questions;
                StatusMessage = "Answer the questions below. Any blank answers are sent as '(no answer)'.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Couldn't generate clarifying questions: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
        else
        {
            await DraftAndReviewAsync(inputs, answers: null, ct);
        }
    }

    /// <summary>
    /// Draft from the answered questions and move to review. Includes a
    /// soft warning when ALL answers are blank — the user is asked to
    /// confirm by clicking Draft a second time, so they don't accidentally
    /// generate a thin skill from blank context.
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task DraftFromQuestionsAsync(CancellationToken ct)
    {
        var allBlank = Questions.Count > 0
                    && Questions.All(q => string.IsNullOrWhiteSpace(q.Answer));

        if (allBlank && !_emptyAnswersWarned)
        {
            _emptyAnswersWarned = true;
            StatusMessage =
                "All " + Questions.Count + " answers are blank — drafting now " +
                "would only use your original Stage 1 inputs. Add some answers " +
                "for a sharper skill, or click \"Draft the skill →\" again to " +
                "proceed anyway.";
            return;
        }

        _emptyAnswersWarned = false;
        await DraftAndReviewAsync(
            CurrentInputs(),
            Questions.Select(q => new QuestionAnswer(q.Question, q.Answer)).ToList(),
            ct);
    }

    private async Task DraftAndReviewAsync(
        SkillBuilderInputs inputs,
        IReadOnlyList<QuestionAnswer>? answers,
        CancellationToken ct)
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Drafting the skill…";
        try
        {
            var draft = await _builder.DraftAsync(inputs, answers, ct);
            Draft = draft;

            Findings.Clear();
            foreach (var f in _builder.Validate(draft)) Findings.Add(f);

            Stage = BuilderStage.Review;
            StatusMessage = Findings.Count == 0
                ? "Draft ready. No validation findings."
                : Findings.Count + " validation finding(s) — review below, then re-draft or emit.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Draft failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Re-run the draft from the same inputs (and Q&A if used).</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RedraftAsync(CancellationToken ct)
    {
        var answers = ResearchOn && Questions.Count > 0
            ? Questions.Select(q => new QuestionAnswer(q.Question, q.Answer)).ToList()
            : null;
        await DraftAndReviewAsync(CurrentInputs(), answers, ct);
    }

    /// <summary>Free-form edit applied to the draft before emission.</summary>
    [RelayCommand]
    private void ApplyEdits(object? _ = null)
    {
        if (Draft is null) return;
        Draft = new SkillFile
        {
            Name = Draft.Name,
            Description = DraftDescription,
            Body = DraftBody,
            HasFrontMatter = true,
        };
        Findings.Clear();
        foreach (var f in _builder.Validate(Draft)) Findings.Add(f);
        StatusMessage = Findings.Count == 0
            ? "Edits applied. No validation findings."
            : Findings.Count + " validation finding(s) after your edits.";
    }

    /// <summary>Pick a target folder and write both output forms.</summary>
    [RelayCommand]
    private async Task EmitAsync(CancellationToken ct)
    {
        if (Draft is null || IsBusy) return;

        var target = await _picker.PickFolderAsync(
            title: "Pick a target folder for the new skill (a .skill file AND a folder will be written here)",
            startLocation: "");
        if (target is null) return;

        IsBusy = true;
        StatusMessage = "Writing skill files…";
        try
        {
            EmitResult = await _builder.EmitAsync(Draft, target, ct);
            Stage = BuilderStage.Emitted;
            StatusMessage = "Wrote " + EmitResult.FlatFilePath +
                            " and " + EmitResult.FolderPath + ".";
        }
        catch (Exception ex)
        {
            StatusMessage = "Emit failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToInputs()
    {
        Stage = BuilderStage.Inputs;
        _emptyAnswersWarned = false;
        StatusMessage = "Adjust inputs and continue again.";
    }

    [RelayCommand]
    private void BackToQuestions()
    {
        if (Questions.Count == 0) { BackToInputs(); return; }
        Stage = BuilderStage.Questions;
        _emptyAnswersWarned = false;
        StatusMessage = "Adjust your answers and re-draft.";
    }

    /// <summary>Start a fresh build, clearing all state.</summary>
    [RelayCommand]
    private void StartOver()
    {
        InputName = InputRoughDescription = InputNotes = "";
        ResearchOn = false;
        Questions.Clear();
        Draft = null;
        Findings.Clear();
        EmitResult = null;
        _emptyAnswersWarned = false;
        Stage = BuilderStage.Inputs;
        StatusMessage = "Cleared. Start a new skill.";
    }

    // ===== Unified-header commands (v0.31) ====================================

    /// <summary>
    /// Jump back to the Inputs stage WITHOUT clearing any data the user has
    /// already entered. Use to re-edit the original inputs while keeping the
    /// research toggle, questions, draft, and emit result intact. Matches the
    /// GoModuleHome semantic on PageViewModel.
    /// </summary>
    [RelayCommand]
    private void GoToInputsStage()
    {
        Stage = BuilderStage.Inputs;
        StatusMessage = "Back to inputs — your data is preserved.";
    }

    /// <summary>
    /// Clear the inputs unique to the CURRENT stage only. Does NOT change
    /// which stage is active. Per-stage:
    ///   Inputs    — clear InputName / InputRoughDescription / InputNotes.
    ///               Leaves ResearchOn alone (it's a behaviour toggle).
    ///   Questions — clear every answer in Questions (the question list is kept).
    ///   Review    — clear Draft (which empties DraftDescription / DraftBody via
    ///               their computed-property change notifications) + Findings.
    ///   Emitted   — clear EmitResult (no-op if null).
    /// </summary>
    [RelayCommand]
    private void ResetCurrentStage()
    {
        if (IsBusy) return;
        switch (Stage)
        {
            case BuilderStage.Inputs:
                InputName = "";
                InputRoughDescription = "";
                InputNotes = "";
                StatusMessage = "Inputs cleared.";
                break;
            case BuilderStage.Questions:
                foreach (var q in Questions) q.Answer = "";
                _emptyAnswersWarned = false;
                StatusMessage = "Answers cleared — re-answer or back out.";
                break;
            case BuilderStage.Review:
                Draft = null;
                Findings.Clear();
                StatusMessage = "Draft cleared — re-draft or back out.";
                break;
            case BuilderStage.Emitted:
                EmitResult = null;
                StatusMessage = "Emit result cleared.";
                break;
        }
    }

    /// <summary>
    /// Clear ALL builder state and return to the Inputs stage. The hard
    /// reset. Wipes inputs, the research toggle, every question and answer,
    /// the draft, findings, and the emit result.
    /// </summary>
    [RelayCommand]
    private void RestartModule()
    {
        if (IsBusy) return;
        InputName = "";
        InputRoughDescription = "";
        InputNotes = "";
        ResearchOn = false;
        Questions.Clear();
        Draft = null;
        Findings.Clear();
        EmitResult = null;
        _emptyAnswersWarned = false;
        Stage = BuilderStage.Inputs;
        StatusMessage = "Reset — fill in name + description to begin.";
    }

    [RelayCommand]
    private async Task CopyAsync(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (await _clipboard.SetTextAsync(text))
            StatusMessage = "Copied to clipboard.";
    }
}

/// <summary>Wizard stages for <see cref="SkillBuilderViewModel"/>.</summary>
public enum BuilderStage
{
    Inputs,
    Questions,
    Review,
    Emitted,
}
