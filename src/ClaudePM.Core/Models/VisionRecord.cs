namespace ClaudePM.Core.Models;

/// <summary>
/// A project's intended vision — a list of concrete, testable statements
/// that say what the project must do or be. Modelled as a list (not one
/// block of prose) on purpose: the audit operates statement-by-statement,
/// so the data model matches the grain of the analysis.
///
/// Exactly one VisionRecord exists per project. <see cref="ApprovedAt"/>
/// is null until the user explicitly approves the drafted vision — and
/// <see cref="ClaudePM.Core.Services.IVisionAuditService"/> MUST refuse
/// to audit against an unapproved record. The approval gate guarantees
/// the measuring stick is correct before anything is measured against it.
/// </summary>
public sealed class VisionRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public IReadOnlyList<VisionStatement> Statements { get; set; } = Array.Empty<VisionStatement>();
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.Now;

    /// <summary>True iff <see cref="ApprovedAt"/> is set.</summary>
    public bool IsApproved => ApprovedAt is not null;
}

/// <summary>
/// One concrete, testable claim about what the project must do or be
/// (e.g. "users can create an account", "data persists between sessions").
/// Vague aspirations ("it should be good") are NOT vision statements —
/// the AI extractor and any human editing should push toward the concrete.
/// </summary>
public sealed class VisionStatement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; set; } = "";
}

/// <summary>
/// One audit run's verdict on one vision statement. The verdict pool is
/// transient (not persisted) — audit reports are exportable as markdown
/// from <see cref="ClaudePM.Core.Services.IVisionAuditService.BuildReportMarkdown"/>
/// but no in-app history is stored, per the spec's out-of-scope clause.
/// </summary>
/// <param name="StatementId">Which statement this verdict is about.</param>
/// <param name="StatementText">The statement text, copied in for the report.</param>
/// <param name="Rank">OnTrack / AtRisk / OffTrack.</param>
/// <param name="Evidence">Short factual evidence the rank is based on.</param>
/// <param name="Recommendation">Concrete next step to close the gap.</param>
public sealed record StatementVerdict(
    Guid StatementId,
    string StatementText,
    AlignmentRank Rank,
    string Evidence,
    string Recommendation);

/// <summary>
/// Alignment of a single vision statement against the project as audited.
/// Visualised via <c>SeverityToBrushConverter</c> using the same red /
/// amber / blue palette as <see cref="FindingSeverity"/> and
/// <see cref="BugSeverity"/>.
/// </summary>
public enum AlignmentRank
{
    /// <summary>No structural evidence the capability exists at all.</summary>
    OffTrack,
    /// <summary>Partial or ambiguous evidence; may be stubbed or incomplete.</summary>
    AtRisk,
    /// <summary>Clear evidence the capability exists or is underway.</summary>
    OnTrack,
}

/// <summary>
/// How wide the audit reaches.
///
/// <list type="bullet">
/// <item><see cref="Structural"/> — gathers only the SHAPE of the project
/// (folder/file names, dependency manifests, the docs) and ranks each
/// statement against that shape. Fast, cheap, size-independent. Catches
/// big drift; cannot catch subtle logic drift inside correctly-named files.</item>
/// <item><see cref="Targeted"/> — two-phase: first the AI picks a bounded
/// set of the most relevant source files, then ranks each statement
/// against shape PLUS the contents of those files. Slower, more API
/// budget, deeper coverage.</item>
/// </list>
/// </summary>
public enum AuditMode
{
    Structural,
    Targeted,
}
