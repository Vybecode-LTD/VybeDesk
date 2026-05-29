using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Docs;

namespace VybeDesk.Services.ProjectHealth;

/// <summary>
/// Default implementation of <see cref="IProjectHealthService"/>. Calls each
/// dependent surface independently and tolerates failures per-metric so the
/// Home dashboard always renders SOMETHING for every card (e.g. a project
/// folder that no longer exists shouldn't make the card error out — it
/// should just show zeros / em-dash and the user can fix the path).
/// </summary>
public sealed class ProjectHealthService(
    IDocReconciliationService docs,
    IAgentActionLogStore agentLog)
    : IProjectHealthService
{
    public async Task<ProjectHealthMetrics> ComputeAsync(
        Project project, CancellationToken ct = default)
    {
        int stale = 0;
        if (!string.IsNullOrWhiteSpace(project.FolderPath) &&
            Directory.Exists(project.FolderPath))
        {
            try
            {
                var files = await docs.ScanAsync(project.FolderPath, ct);
                var findings = await docs.AnalyzeStructuralAsync(project.FolderPath, files, ct);
                stale = findings.Count;
            }
            catch
            {
                // Stale count is best-effort; failures here leave it at 0.
            }
        }

        int? commits = null;
        if (!string.IsNullOrWhiteSpace(project.FolderPath) &&
            Directory.Exists(project.FolderPath))
        {
            commits = await GitInfo.GetCommitCountSinceAsync(
                project.FolderPath,
                DateTimeOffset.UtcNow.AddDays(-7),
                ct);
        }

        int pending = 0;
        try
        {
            var entries = await agentLog.GetByProjectAsync(project.Id, limit: 1000, ct);
            pending = entries.Count(e => e.Status == AgentActionLogStatus.Done);
        }
        catch
        {
            // Same — best-effort.
        }

        return new ProjectHealthMetrics(stale, commits, pending, project.LastActivity);
    }
}
