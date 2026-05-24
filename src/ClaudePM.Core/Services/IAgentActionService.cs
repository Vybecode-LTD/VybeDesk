using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>Result of validating an <see cref="AgentAction"/>.</summary>
public sealed record ActionValidation(bool IsValid, string Message);

/// <summary>Result of a scoped read_file call.</summary>
public sealed record ReadFileResult(bool Success, string Content, string ErrorMessage);

/// <summary>Result of a scoped list_directory call.</summary>
public sealed record ListDirectoryResult(
    bool Success, IReadOnlyList<string> Entries, string ErrorMessage);

/// <summary>
/// Performs allow-listed filesystem actions confined to scoped project roots,
/// with validation, execution, and undo (Module 4 — AI Notebook). Treats every
/// proposed action as untrusted input.
/// </summary>
public interface IAgentActionService
{
    IReadOnlyList<string> ScopedRoots { get; }
    void SetScopedRoots(IEnumerable<string> roots);

    /// <summary>A human-readable description of what the action would do.</summary>
    string Describe(AgentAction action);

    /// <summary>Checks the action is allow-listed and confined to a scoped root.</summary>
    ActionValidation Validate(AgentAction action);

    /// <summary>Executes the action and records it for undo. Throws on failure.</summary>
    Task ExecuteAsync(AgentAction action, CancellationToken ct = default);

    bool CanUndo { get; }
    IReadOnlyList<string> UndoHistory { get; }

    /// <summary>Reverses the most recently executed action.</summary>
    Task UndoLastAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads a file's UTF-8 text contents, confined to scoped roots. Returns
    /// truncated content (with a marker) past <paramref name="maxBytes"/>.
    /// </summary>
    ReadFileResult ReadFile(string path, int maxBytes = 50_000);

    /// <summary>
    /// Lists a directory's immediate entries, confined to scoped roots.
    /// Folders are suffixed with '/'. Truncated past <paramref name="maxEntries"/>.
    /// </summary>
    ListDirectoryResult ListDirectory(string path, int maxEntries = 200);
}
