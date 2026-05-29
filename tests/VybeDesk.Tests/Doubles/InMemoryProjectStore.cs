using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Tests.Doubles;

/// <summary>
/// In-memory project store with seed data for tests. Moved here from the
/// Services project because production code should not ship test stubs
/// (the real app uses <c>SqliteProjectStore</c>).
/// </summary>
internal sealed class InMemoryProjectStore : IProjectStore
{
    private readonly List<Project> _items = new()
    {
        new Project { Name = "TestProject", Description = "A test project.", FolderPath = Path.GetTempPath() },
        new Project { Name = "Sample Project", Description = "Placeholder.", FolderPath = Path.GetTempPath() },
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
