using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Module 5b — Skill Builder. Orchestrates the workflow of designing a new
/// Claude skill from a name + rough description + notes. Process-oriented:
/// the output is files on disk (a flat <c>.skill</c> and a folder
/// <c>&lt;name&gt;/SKILL.md</c>), NOT database rows. Validation and
/// serialization MUST be delegated to <see cref="ISkillLibraryService"/> so
/// the Builder and the Manager share one source of truth for what a valid
/// skill is and how a skill is rendered to text — a second copy of either
/// would silently diverge.
/// </summary>
public interface ISkillBuilderService
{
    /// <summary>
    /// Step 2 when the research toggle is ON. Asks the AI for a focused list
    /// of clarifying questions (intended triggers, scope boundaries, concrete
    /// use cases, what the skill should NOT do). Returns the questions; the
    /// caller collects answers and feeds them into <see cref="DraftAsync"/>.
    /// The "research" label is a UI convention only — the app has no web
    /// access, so this is interactive clarification, not internet research.
    /// </summary>
    Task<IReadOnlyList<string>> GenerateClarifyingQuestionsAsync(
        SkillBuilderInputs inputs, CancellationToken ct = default);

    /// <summary>
    /// Step 3 — drafts a complete <see cref="SkillFile"/> from the inputs
    /// (and optionally the clarifying Q&amp;A pairs). The
    /// <see cref="SkillFile.Name"/> is set from <see cref="SkillBuilderInputs.Name"/>
    /// directly; the AI produces a polished routing-style description and an
    /// imperative body that leads with the core principle and ends with
    /// anti-patterns.
    /// </summary>
    Task<SkillFile> DraftAsync(
        SkillBuilderInputs inputs,
        IReadOnlyList<QuestionAnswer>? answers,
        CancellationToken ct = default);

    /// <summary>
    /// Step 4 — validates the draft. Delegates to
    /// <see cref="ISkillLibraryService.Validate"/> so the Builder's
    /// validation is identical to the Manager's by construction.
    /// </summary>
    IReadOnlyList<Finding> Validate(SkillFile skill);

    /// <summary>
    /// Step 5 — writes the skill in BOTH output forms beneath
    /// <paramref name="targetFolder"/>: a flat
    /// <c>&lt;targetFolder&gt;/&lt;name&gt;.skill</c> file, and a
    /// <c>&lt;targetFolder&gt;/&lt;name&gt;/SKILL.md</c> folder. Each form
    /// serves a different user need (one-click add vs. growing the skill
    /// with bundled resources later). Returns both written paths.
    /// </summary>
    Task<SkillEmitResult> EmitAsync(
        SkillFile skill, string targetFolder, CancellationToken ct = default);
}

/// <summary>Raw user inputs at Stage 1 of the builder wizard.</summary>
/// <param name="Name">Lowercase-hyphen identifier the user chose.</param>
/// <param name="RoughDescription">User's rough sense of the skill's purpose.</param>
/// <param name="Notes">Free-form extra context — examples, edge cases, etc.</param>
public sealed record SkillBuilderInputs(
    string Name,
    string RoughDescription,
    string Notes);

/// <summary>One Q&amp;A pair from the research/clarifying step.</summary>
public sealed record QuestionAnswer(string Question, string Answer);

/// <summary>Paths produced by <see cref="ISkillBuilderService.EmitAsync"/>.</summary>
/// <param name="FlatFilePath">The single <c>&lt;name&gt;.skill</c> file.</param>
/// <param name="FolderPath">The <c>&lt;name&gt;/</c> folder containing <c>SKILL.md</c>.</param>
public sealed record SkillEmitResult(string FlatFilePath, string FolderPath);
