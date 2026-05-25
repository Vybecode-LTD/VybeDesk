using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>Persistence for the global prompt library.</summary>
public interface IPromptStore
{
    Task<IReadOnlyList<PromptEntry>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(PromptEntry prompt, CancellationToken ct = default);
    Task UpdateAsync(PromptEntry prompt, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Full-text search across title, body, and tags. Empty / whitespace queries
    /// return all prompts (so the UI can share a single code path). Tokens are
    /// matched as prefixes, ANDed together; ordering is by relevance.
    /// </summary>
    Task<IReadOnlyList<PromptEntry>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Historical snapshots of a prompt, captured by <see cref="UpdateAsync"/>
    /// just before each content-changing update. Newest first.
    /// </summary>
    Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(Guid promptId, CancellationToken ct = default);
}
