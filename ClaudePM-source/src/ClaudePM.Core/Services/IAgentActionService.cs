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
///
/// Undo is now backed by <see cref="IAgentActionLogStore"/> (SQLite) rather
/// than an in-memory stack, so undo works across app restarts: an action
/// executed yesterday is still reversible today, as long as the on-disk
/// reverse operation is still applicable (target file/folder still in the
/// expected state). This is M3 #10 Phase B.
/// </summary>
public interface IAgentActionService
{
    IReadOnlyList<string> ScopedRoots { get; }
    void SetScopedRoots(IEnumerable<string> roots);

    /// <summary>
    /// Which project the active session is scoped to. Set this whenever the
    /// Notebook's ActiveProject changes — every executed action is tagged
    /// with this project id in the persistent log, and
    /// <see cref="UndoLastAsync"/> pulls the most recent undoable action
    /// for THIS project. Null = no project picked; <see cref="ExecuteAsync"/>
    /// will still run validation (which blocks because there are no scoped
    /// roots in that case), but no log entry is written because there is
    /// nothing to scope it to.
    /// </summary>
    Guid? ActiveProjectId { get; }

    /// <summary>
    /// Set the active project. Refreshes the cached
    /// <see cref="RecentActions"/> snapshot for the new project (fire-and-
    /// forget) and raises <see cref="RecentActionsChanged"/> when done so
    /// VMs can re-bind their action-history list.
    /// </summary>
    void SetActiveProject(Guid? projectId);

    /// <summary>A human-readable description of what the action would do.</summary>
    string Describe(AgentAction action);

    /// <summary>Checks the action is allow-listed and confined to a scoped root.</summary>
    ActionValidation Validate(AgentAction action);

    /// <summary>
    /// Executes the action and records it in the persistent log (if
    /// <see cref="ActiveProjectId"/> is set). Throws on failure.
    /// </summary>
    Task ExecuteAsync(AgentAction action, CancellationToken ct = default);

    /// <summary>
    /// True iff the persistent log has at least one Done entry for the
    /// active project. Backed by an in-memory cache that refreshes on
    /// log mutations and on active-project changes (the store would be
    /// hit on every binding evaluation otherwise).
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Snapshot of recent actions for the active project, newest-first.
    /// Empty when no project is selected. Backed by the same cache as
    /// <see cref="CanUndo"/>; refresh notifications come via
    /// <see cref="RecentActionsChanged"/>.
    /// </summary>
    IReadOnlyList<AgentActionLogEntry> RecentActions { get; }

    /// <summary>
    /// Fires after <see cref="RecentActions"/> / <see cref="CanUndo"/> have
    /// been refreshed (after an execute, an undo, an active-project switch,
    /// or any external mutation of the underlying log store). VMs should
    /// re-read both properties when this fires.
    /// </summary>
    event Action? RecentActionsChanged;

    /// <summary>Reverses the most recently executed undoable action for the active project.</summary>
    Task UndoLastAsync(CancellationToken ct = default);

    /// <summary>
    /// True iff the persistent log has at least one Undone entry for the
    /// active project (i.e. an action that was undone and can be redone).
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Re-applies the most recently undone action for the active project,
    /// flipping its status back to Done. Mirror of UndoLastAsync.
    /// </summary>
    Task RedoLastAsync(CancellationToken ct = default);

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
