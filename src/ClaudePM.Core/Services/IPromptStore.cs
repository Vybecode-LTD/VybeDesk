using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>Persistence for the global prompt library.</summary>
public interface IPromptStore
{
    Task<IReadOnlyList<PromptEntry>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(PromptEntry prompt, CancellationToken ct = default);
    Task UpdateAsync(PromptEntry prompt, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
