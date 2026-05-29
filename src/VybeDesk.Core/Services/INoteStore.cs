using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>Persistence for AI Notebook notes.</summary>
public interface INoteStore
{
    event Action? Changed;
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Note note, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
