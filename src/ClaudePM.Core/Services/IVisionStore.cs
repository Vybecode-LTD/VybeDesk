using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Persistence for <see cref="VisionRecord"/>. One vision per project,
/// upserted by project id. Audit reports themselves are NOT persisted —
/// they're transient and exportable as markdown via
/// <see cref="IVisionAuditService.BuildReportMarkdown"/>.
/// </summary>
public interface IVisionStore
{
    /// <summary>
    /// Fires after any mutating call (Save / Remove). Subscribers re-read
    /// on this event.
    /// </summary>
    event Action? Changed;

    /// <summary>Returns the project's vision record, or null if none has been drafted yet.</summary>
    Task<VisionRecord?> GetByProjectAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>Inserts or updates the record for its project (UNIQUE on project_id).</summary>
    Task SaveAsync(VisionRecord record, CancellationToken ct = default);

    /// <summary>Removes the vision record for a given project (if any).</summary>
    Task RemoveAsync(Guid projectId, CancellationToken ct = default);
}
