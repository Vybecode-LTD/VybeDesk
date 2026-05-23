using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Storage;

/// <summary>STUB: in-memory prompt store with seed data. Replace with SQLite + FTS5.</summary>
public sealed class InMemoryPromptStore : IPromptStore
{
    private readonly List<PromptVersion> _versions = new();
    private readonly List<PromptEntry> _items = new()
    {
        new PromptEntry
        {
            Title = "Scaffold a new module",
            Body = "Scaffold a new {{module_name}} module following the existing project conventions.",
            Category = "Claude Code",
            Tags = { "scaffold", "template" },
        },
        new PromptEntry
        {
            Title = "Reconcile documentation",
            Body = "Review all project docs and fix any inconsistencies you find.",
            Category = "Documentation",
            Tags = { "docs" },
        },
    };

    public Task<IReadOnlyList<PromptEntry>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PromptEntry>>(_items.ToList());

    public Task AddAsync(PromptEntry prompt, CancellationToken ct = default)
    {
        _items.Add(prompt);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PromptEntry prompt, CancellationToken ct = default)
    {
        var i = _items.FindIndex(p => p.Id == prompt.Id);
        if (i < 0) return Task.CompletedTask;

        var existing = _items[i];
        if (existing.Title != prompt.Title || existing.Body != prompt.Body ||
            existing.Category != prompt.Category ||
            !existing.Tags.SequenceEqual(prompt.Tags))
        {
            _versions.Add(new PromptVersion
            {
                PromptId = existing.Id,
                Title = existing.Title,
                Body = existing.Body,
                Category = existing.Category,
                Tags = existing.Tags.ToList(),
                Captured = DateTimeOffset.UtcNow,
            });
        }

        _items[i] = prompt;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _items.RemoveAll(p => p.Id == id);
        _versions.RemoveAll(v => v.PromptId == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(Guid promptId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PromptVersion>>(
            _versions
                .Where(v => v.PromptId == promptId)
                .OrderByDescending(v => v.Captured)
                .ToList());

    public Task<IReadOnlyList<PromptEntry>> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = (query ?? "").Trim();
        IEnumerable<PromptEntry> result = _items;
        if (q.Length > 0)
            result = result.Where(p =>
                p.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Body.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        return Task.FromResult<IReadOnlyList<PromptEntry>>(result.ToList());
    }
}
