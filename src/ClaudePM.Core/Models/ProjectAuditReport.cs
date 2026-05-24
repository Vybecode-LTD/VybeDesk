namespace ClaudePM.Core.Models;

/// <summary>
/// The synthesis pass over a project's docs (Module 1, M2.5). Distinct from
/// <see cref="Finding"/> — findings are local-deterministic + AI doc-vs-doc
/// inconsistency checks; this is "read the project and tell me what's there,
/// what's done, what's not, where do the docs disagree?"
/// </summary>
public sealed record ProjectAuditReport(
    string Design,
    IReadOnlyList<AuditRoadmapItem> RoadmapItems,
    IReadOnlyList<AuditInconsistency> Inconsistencies)
{
    public IReadOnlyList<AuditRoadmapItem> Complete =>
        RoadmapItems.Where(i => i.Status == AuditItemStatus.Complete).ToList();

    public IReadOnlyList<AuditRoadmapItem> Incomplete =>
        RoadmapItems.Where(i => i.Status != AuditItemStatus.Complete).ToList();

    public static ProjectAuditReport Empty { get; } =
        new("", Array.Empty<AuditRoadmapItem>(), Array.Empty<AuditInconsistency>());
}

/// <summary>One roadmap entry the audit extracted from the docs.</summary>
public sealed record AuditRoadmapItem(
    string Title,
    AuditItemStatus Status,
    string Category,
    string Source,
    string Evidence);

public enum AuditItemStatus { Complete, Incomplete, Unknown }

/// <summary>One inconsistency the audit found across docs.</summary>
public sealed record AuditInconsistency(
    FindingSeverity Severity,
    IReadOnlyList<string> Docs,
    string Issue);
