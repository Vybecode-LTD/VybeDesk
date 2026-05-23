using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Storage;

/// <summary>STUB: in-memory note store. Replace with SQLite.</summary>
public sealed class InMemoryNoteStore : INoteStore
{
    private readonly List<Note> _items = new();

    public Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Note>>(_items.ToList());

    public Task AddAsync(Note note, CancellationToken ct = default)
    {
        _items.Add(note);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _items.RemoveAll(n => n.Id == id);
        return Task.CompletedTask;
    }
}
