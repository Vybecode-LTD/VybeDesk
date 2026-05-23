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

    /// <summary>Local, deterministic checks — no AI, no token cost.</summary>
    Task<IReadOnlyList<Finding>> AnalyzeStructuralAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct = default);

    /// <summary>AI-driven doc-vs-doc consistency check. Returns a markdown summary.</summary>
    Task<string> AnalyzeSemanticAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct = default);

    /// <summary>Builds a ready-to-paste Claude Code fix prompt from the findings.</summary>
    string BuildFixPrompt(
        string projectPath, IReadOnlyList<Finding> structural, string semanticResult);

    /// <summary>Builds a full markdown reconciliation report.</summary>
    string BuildReportMarkdown(
        string projectPath, IReadOnlyList<Finding> structural, string semanticResult);

    /// <summary>Writes the report into the scanned folder; returns the file path.</summary>
    Task<string> SaveReportAsync(string folderPath, string markdown, CancellationToken ct = default);
}
