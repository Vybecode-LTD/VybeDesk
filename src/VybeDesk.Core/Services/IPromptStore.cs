using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>Persistence for the global prompt library.</summary>
public interface IPromptStore
{
    /// <summary>Fires after any mutation (Add, Update, Remove).</summary>
    event Action? Changed;
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
