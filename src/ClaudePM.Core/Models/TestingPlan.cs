namespace ClaudePM.Core.Models;

/// <summary>
/// A project's testing strategy — chosen via a guided questionnaire, persisted
/// so the reasoning can be revisited. Each project has at most one
/// <see cref="TestingPlan"/>; absence means the questionnaire hasn't been run
/// yet for that project.
///
/// The full <see cref="Answers"/> are stored alongside the conclusion on
/// purpose: a project's needs change as it grows, and the user must be able
/// to see why a strategy was chosen and re-run the questionnaire later. Do
/// NOT throw the answers away after computing the strategy.
/// </summary>
public sealed class TestingPlan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string StrategySummary { get; set; } = "";
    public IReadOnlyList<string> Frameworks { get; set; } = Array.Empty<string>();
    public IReadOnlyList<TestKind> Kinds { get; set; } = Array.Empty<TestKind>();
    public QuestionnaireAnswers Answers { get; set; } = new();
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// The kinds of tests a strategy can call for. Database testing is NOT a
/// separate kind — it's <see cref="Integration"/> within whatever framework
/// the project's language is using (xUnit, pytest, etc.). The framework
/// catalog and the strategy selector both enforce this convention.
/// </summary>
public enum TestKind
{
    Unit,
    Integration,
    Component,
    EndToEnd,
    ManualChecklist,
}

/// <summary>
/// The questionnaire answers in their stored form. String-valued (not enums)
/// so old DB rows survive a renumbering, and so the values can be displayed
/// directly to the user when revisiting the strategy.
/// Empty strings mean "not yet answered" — the View enforces that all five
/// are non-empty before the recommendation is generated.
/// </summary>
public sealed record QuestionnaireAnswers
{
    /// <summary>"Library", "Desktop", "WebFrontend", "CLI", "Mixed".</summary>
    public string ProjectKind { get; init; } = "";

    /// <summary>"DotNet", "Python", "JavaScript", "Cpp", "Other".</summary>
    public string Language { get; init; } = "";

    /// <summary>"Critical", "Important", "Personal".</summary>
    public string Criticality { get; init; } = "";

    /// <summary>"Solo", "SmallTeam", "LargerTeam".</summary>
    public string TeamSize { get; init; } = "";

    /// <summary>"Heavy", "Some", "None".</summary>
    public string ExternalSystems { get; init; } = "";

    public bool IsComplete()
        => !string.IsNullOrEmpty(ProjectKind)
        && !string.IsNullOrEmpty(Language)
        && !string.IsNullOrEmpty(Criticality)
        && !string.IsNullOrEmpty(TeamSize)
        && !string.IsNullOrEmpty(ExternalSystems);
}
