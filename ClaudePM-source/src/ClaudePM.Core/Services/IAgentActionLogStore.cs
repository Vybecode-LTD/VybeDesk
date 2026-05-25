using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Per-project, append-only log of executed agent actions, with status
/// updates and single-delete (for clearing an undone action's record).
/// Backs the Notebook's cross-session undo (`IAgentActionService.UndoLast`)
/// and the side-panel "Action history" list.
///
/// Shape mirrors <see cref="IAuditHistoryStore"/> deliberately — same
/// per-project newest-first ordering, same Add / Remove / ClearProject
/// surface.
/// </summary>
public interface IAgentActionLogStore
{
    Task AddAsync(AgentActionLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Entries for the project, newest-first. Limit caps the result;
    /// 50 is a sensible default for the side-panel list.
    /// </summary>
    Task<IReadOnlyList<AgentActionLogEntry>> GetByProjectAsync(
        Guid projectId, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// The most recent action for the project that's still in
    /// <see cref="AgentActionLogStatus.Done"/> (i.e. eligible to undo).
    /// Null if there's nothing to undo for this project.
    /// </summary>
    Task<AgentActionLogEntry?> GetMostRecentUndoableAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// The most recent action for the project that's currently in
    /// <see cref="AgentActionLogStatus.Undone"/> (i.e. eligible to redo).
    /// Null if there's nothing to redo for this project.
    /// </summary>
    Task<AgentActionLogEntry?> GetMostRecentUndoneAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>Mark an entry's status (typically Done → Undone after a successful UndoLast).</summary>
    Task UpdateStatusAsync(Guid id, AgentActionLogStatus status, CancellationToken ct = default);

    Task RemoveAsync(Guid id, CancellationToken ct = default);
    Task ClearProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Fires after any mutation so VMs can refresh their list.</summary>
    event Action? Changed;
}
