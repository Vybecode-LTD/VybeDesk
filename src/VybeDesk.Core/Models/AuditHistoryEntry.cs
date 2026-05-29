namespace VybeDesk.Core.Models;

/// <summary>
/// One persisted audit run for a project. Stores the markdown report and
/// the Claude Code deep-dive prompt as text — they are NOT re-generated on
/// load, so the user can revisit an old audit verbatim months later. Also
/// keeps the verdict list so the per-statement card view can be restored.
///
/// The original Vision Audit spec marked persisted history as out-of-scope,
/// but the user opted in: keeping reports lets you compare a quick
/// structural pass against a later targeted pass on the same vision, and
/// re-read findings without re-paying for the AI call.
/// </summary>
public sealed class AuditHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public AuditMode Mode { get; set; }
    public int OffTrackCount { get; set; }
    public int AtRiskCount { get; set; }
    public int OnTrackCount { get; set; }
    public string ReportMarkdown { get; set; } = "";
    public string DeepDivePrompt { get; set; } = "";
    public IReadOnlyList<StatementVerdict> Verdicts { get; set; } = Array.Empty<StatementVerdict>();
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Short label used in the history list.</summary>
    public string DisplayLabel
        => GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm") + " · " + Mode;

    /// <summary>"3 off-track · 2 at-risk · 5 on-track" summary for the card.</summary>
    public string SummaryLabel
        => OffTrackCount + " off-track · " + AtRiskCount + " at-risk · " + OnTrackCount + " on-track";
}
