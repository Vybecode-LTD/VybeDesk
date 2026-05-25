using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Persistence for <see cref="TestingPlan"/> records. Each project has at
/// most one plan, so <see cref="GetByProjectAsync"/> returns it or null and
/// <see cref="SaveAsync"/> is an upsert (insert on first save, update
/// thereafter — same TestingPlan.Id stays stable across re-runs of the
/// questionnaire for that project).
/// </summary>
public interface ITestingPlanStore
{
    /// <summary>
    /// Fires after any mutating call (Save / Remove). Subscribers re-read on
    /// this event.
    /// </summary>
    event Action? Changed;

    /// <summary>Returns the project's plan, or null if none exists yet.</summary>
    Task<TestingPlan?> GetByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Inserts or updates the plan for its project.</summary>
    Task SaveAsync(TestingPlan plan, CancellationToken ct = default);

    /// <summary>Removes the plan for a given project (if one exists).</summary>
    Task RemoveAsync(Guid projectId, CancellationToken ct = default);
}
