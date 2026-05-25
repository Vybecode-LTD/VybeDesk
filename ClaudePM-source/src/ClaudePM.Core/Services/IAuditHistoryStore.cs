using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Persistence for <see cref="AuditHistoryEntry"/>. Per-project list of
/// completed audit runs, newest first. Each entry is a snapshot — the
/// caller does NOT mutate stored entries; running another audit creates a
/// new entry rather than updating an old one.
/// </summary>
public interface IAuditHistoryStore
{
    /// <summary>Fires after Add / Remove / ClearProject.</summary>
    event Action? Changed;

    /// <summary>Returns the project's history newest-first.</summary>
    Task<IReadOnlyList<AuditHistoryEntry>> GetByProjectAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>Persists one audit run.</summary>
    Task AddAsync(AuditHistoryEntry entry, CancellationToken ct = default);

    /// <summary>Removes a single entry by id.</summary>
    Task RemoveAsync(Guid entryId, CancellationToken ct = default);

    /// <summary>Removes every history entry for a given project.</summary>
    Task ClearProjectAsync(Guid projectId, CancellationToken ct = default);
}
