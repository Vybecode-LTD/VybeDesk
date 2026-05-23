using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>Persistence for AI Notebook notes.</summary>
public interface INoteStore
{
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Note note, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
