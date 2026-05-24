using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Scans a project folder for documentation and reconciles it — a deterministic
/// structural pass plus an AI-driven doc-vs-doc semantic pass (Module 1).
/// </summary>
public interface IDocReconciliationService
{
    /// <summary>Finds documentation files (.md, .txt) under a folder.</summary>
    Task<IReadOnlyList<DocFile>> ScanAsync(string folderPath, CancellationToken ct = default);

    /// <summary>
    /// Local, deterministic checks — no AI, no token cost.
    /// <paramref name="projectRoot"/> is used to surface git-aware staleness
    /// signals; pass the same folder you handed to <see cref="ScanAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Finding>> AnalyzeStructuralAsync(
        string projectRoot, IReadOnlyList<DocFile> docs, CancellationToken ct = default);

    /// <summary>AI-driven doc-vs-doc consistency check. Returns a markdown summary.</summary>
    Task<string> AnalyzeSemanticAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct = default);

    /// <summary>
    /// AI-driven synthesis pass: reads a signal-weighted bundle of docs and
    /// returns a structured <see cref="ProjectAuditReport"/> with a design
    /// summary, a flat roadmap-item list (each tagged complete / incomplete /
    /// unknown), and an inconsistencies list. The audit is "what's the state
    /// of this project?" — distinct from AnalyzeSemanticAsync which only
    /// flags contradictions.
    /// </summary>
    Task<ProjectAuditReport> AuditAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct = default);

    /// <summary>Builds a ready-to-paste Claude Code fix prompt from a list of audit inconsistencies.</summary>
    string BuildAuditFixPrompt(
        string projectPath, IReadOnlyList<AuditInconsistency> inconsistencies);

    /// <summary>Builds a ready-to-paste Claude Code fix prompt from the findings.</summary>
    string BuildFixPrompt(
        string projectPath, IReadOnlyList<Finding> structural, string semanticResult);

    /// <summary>Builds a full markdown reconciliation report.</summary>
    string BuildReportMarkdown(
        string projectPath, IReadOnlyList<Finding> structural, string semanticResult);

    /// <summary>Writes the report into the scanned folder; returns the file path.</summary>
    Task<string> SaveReportAsync(string folderPath, string markdown, CancellationToken ct = default);
}
