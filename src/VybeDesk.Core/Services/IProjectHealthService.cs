using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>
/// Computes the per-project health metrics shown on the Home dashboard:
/// stale-doc count from a fresh structural scan, recent commit count via
/// git, and pending-action count from the agent log. Backed by the existing
/// IDocReconciliationService + IAgentActionLogStore + GitInfo helpers —
/// this interface just packages them as a single per-project call.
/// </summary>
public interface IProjectHealthService
{
    Task<ProjectHealthMetrics> ComputeAsync(Project project, CancellationToken ct = default);
}

/// <summary>Output of one project's health computation.</summary>
public sealed record ProjectHealthMetrics(
    int StaleDocCount,
    int? RecentCommitCount, // null if git absent / not a repo
    int PendingActionCount,
    DateTimeOffset LastActivity);
