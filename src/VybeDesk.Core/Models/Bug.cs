namespace VybeDesk.Core.Models;

/// <summary>
/// A reported defect scoped to a single <see cref="Project"/>. The three
/// reproduction fields (<see cref="StepsToReproduce"/>,
/// <see cref="ExpectedResult"/>, <see cref="ActualResult"/>) are intentionally
/// separate — the structure teaches the user to think reproducibly. The Bug
/// Tracker UI surfaces them as three distinct labeled fields, NOT one merged
/// description.
/// </summary>
public sealed class Bug
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public BugSeverity Severity { get; set; } = BugSeverity.Major;
    public BugStatus Status { get; set; } = BugStatus.Open;
    public string StepsToReproduce { get; set; } = "";
    public string ExpectedResult { get; set; } = "";
    public string ActualResult { get; set; } = "";
    public string Area { get; set; } = "";
    public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;
}

/// <summary>
/// Severity is the triage axis — it answers "what gets fixed next."
/// Three levels keep triage decisions cheap. Matches the bug-triage skill.
/// </summary>
public enum BugSeverity
{
    Critical,
    Major,
    Minor,
}

/// <summary>Lifecycle state of a bug from report to resolution.</summary>
public enum BugStatus
{
    Open,
    Fixing,
    Fixed,
    WontFix,
}
