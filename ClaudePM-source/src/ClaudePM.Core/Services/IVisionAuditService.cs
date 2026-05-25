using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Module 8 — Vision Audit. Externalises drift detection: distil a project's
/// intended vision into testable statements, get the user to approve it,
/// then audit the project against that vision.
///
/// Two audit strategies, both deliberately size-independent (a real codebase
/// is too large to send wholesale): <see cref="AuditMode.Structural"/> reads
/// only the SHAPE of the project; <see cref="AuditMode.Targeted"/> also reads
/// a bounded set of the most relevant files. The deep line-by-line dive is
/// HANDED OFF to Claude Code via <see cref="BuildDeepDivePrompt"/> — that's
/// the agent's job, not this module's.
/// </summary>
public interface IVisionAuditService
{
    /// <summary>
    /// Stage 1 — read the project's documentation and distil a draft list of
    /// concrete vision statements. The user MUST review and approve the
    /// result before any audit runs. Reuses the Documentation module's
    /// doc-scanning capability so we don't keep two scanners in sync.
    /// </summary>
    Task<IReadOnlyList<VisionStatement>> ExtractVisionAsync(
        string projectFolder, CancellationToken ct = default);

    /// <summary>
    /// Stages 3 and 4 — given an approved vision, audit the project against
    /// it in either <see cref="AuditMode.Structural"/> or
    /// <see cref="AuditMode.Targeted"/> mode. Throws
    /// <see cref="InvalidOperationException"/> if
    /// <see cref="VisionRecord.IsApproved"/> is false: the approval gate is
    /// mandatory because an audit against the wrong measuring stick is
    /// worse than no audit at all.
    /// </summary>
    Task<AuditReport> AuditAsync(
        VisionRecord approvedVision,
        string projectFolder,
        AuditMode mode,
        CancellationToken ct = default);

    /// <summary>
    /// Renders the audit report as markdown — the format the user can copy,
    /// paste into a colleague's chat, or save alongside the project. Off-track
    /// items lead, per the skill's "Lead the report with Off track items"
    /// guidance.
    /// </summary>
    string BuildReportMarkdown(AuditReport report, string projectName);

    /// <summary>
    /// Builds a Claude Code prompt asking the agent to investigate the
    /// flagged areas in the actual code — the structural audit cannot catch
    /// behavioral drift inside correctly-named files, so the deep dive is
    /// deliberately handed off. The prompt names the approved vision and
    /// the At-risk / Off-track statements so the agent knows where to look.
    /// </summary>
    string BuildDeepDivePrompt(AuditReport report, string projectName);
}

/// <summary>
/// The output of one audit run. Transient — not persisted. Convert to
/// markdown via <see cref="IVisionAuditService.BuildReportMarkdown"/> for
/// export.
/// </summary>
/// <param name="Mode">Which mode produced this report.</param>
/// <param name="Verdicts">One verdict per statement on the audited vision.</param>
/// <param name="GeneratedAt">When the audit completed.</param>
public sealed record AuditReport(
    AuditMode Mode,
    IReadOnlyList<StatementVerdict> Verdicts,
    DateTimeOffset GeneratedAt);
