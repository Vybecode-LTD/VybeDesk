using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Storage;

/// <summary>
/// STUB: in-memory project store with seed data. Replace with a SQLite-backed
/// implementation (see SPEC.md, section 2 — persistence).
/// </summary>
public sealed class InMemoryProjectStore : IProjectStore
{
    private readonly List<Project> _items = new()
    {
        new Project { Name = "ClaudePM", Description = "This very app.", FolderPath = @"C:\dev\ClaudePM" },
        new Project { Name = "Sample Project", Description = "Placeholder — register your own.", FolderPath = @"C:\dev\sample" },
    };

    public event Action? Changed;

    public Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Project>>(_items.ToList());

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _items.Add(project);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        var i = _items.FindIndex(p => p.Id == project.Id);
        if (i >= 0) _items[i] = project;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _items.RemoveAll(p => p.Id == id);
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
