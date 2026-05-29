using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>
/// Ingests an existing folder on disk as a new Project, picking up any
/// surrounding context that would otherwise have to be entered by hand:
/// CLAUDE.md as the project description, the project's last git commit
/// time as LastActivity, and any .claude/commands/*.md files as
/// PromptEntries tagged with the project name. The .claude/skills/
/// half of this import is deliberately deferred to the Module 5 rewrite
/// (M6).
///
/// Pure side-effects-and-go: writes to <see cref="IProjectStore"/> and
/// <see cref="IPromptStore"/> directly. Returns a summary the caller
/// can surface to the user.
/// </summary>
public interface IProjectImportService
{
    /// <summary>
    /// Import the folder at <paramref name="folderPath"/> as a new Project.
    /// Errors-out (Success=false) if the folder doesn't exist or isn't
    /// readable. Otherwise creates a Project and (optionally) one or more
    /// PromptEntries; both saved through their respective stores.
    /// </summary>
    Task<ProjectImportResult> ImportFromFolderAsync(
        string folderPath, CancellationToken ct = default);
}

/// <summary>Outcome of a single import attempt.</summary>
public sealed record ProjectImportResult(
    bool Success,
    Project? Project,
    int PromptsImported,
    int PromptsSkippedDuplicate,
    bool HadGitTimestamp,
    bool HadClaudeMd,
    string Message);
