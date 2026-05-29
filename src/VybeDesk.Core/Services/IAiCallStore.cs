using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>Persistent store for AI API call records (M3 #12 telemetry).</summary>
public interface IAiCallStore
{
    Task AddAsync(AiCallRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<AiCallRecord>> GetRecentAsync(int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<AiCallRecord>> GetByProjectAsync(Guid projectId, int limit = 50, CancellationToken ct = default);
    Task<AiCallSummary> GetSummaryAsync(CancellationToken ct = default);
    event Action? Changed;
}

public sealed record AiCallSummary(
    int TotalCalls,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalCacheCreationTokens,
    long TotalCacheReadTokens,
    double TotalCost);
