using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Persistence for <see cref="Bug"/> records. Bugs are project-scoped — every
/// bug belongs to exactly one <see cref="Project"/>, and there is no global
/// bug list. Mirrors <see cref="IProjectStore"/>'s shape with one key
/// difference: <see cref="GetByProjectAsync"/> filters by project id.
/// </summary>
public interface IBugStore
{
    /// <summary>
    /// Fires after any mutating call (Add / Update / Remove). Subscribers
    /// (e.g. the Bug Tracker ViewModel) re-read on this event.
    /// </summary>
    event Action? Changed;

    /// <summary>Returns all bugs belonging to the given project.</summary>
    Task<IReadOnlyList<Bug>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Bug bug, CancellationToken ct = default);
    Task UpdateAsync(Bug bug, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
