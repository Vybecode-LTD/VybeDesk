using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>Persistence for registered projects. Async to be SQLite-ready.</summary>
public interface IProjectStore
{
    /// <summary>
    /// Fires after any mutating call (Add / Update / Remove). Subscribers that
    /// cache project state (Documentation's project picker, Notebook's scoped
    /// roots) re-read on this event.
    /// </summary>
    event Action? Changed;

    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
