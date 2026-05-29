namespace VybeDesk.Core.Models;

/// <summary>
/// One row in the persistent agent action log. Captures everything needed
/// to undo the action cross-session — for EditFile that means the
/// pre-edit content of the target file (stored verbatim so a future-app-
/// version that loads the entry can still restore it byte-for-byte).
///
/// Ordered newest-first when queried by project (matches AuditHistoryEntry).
/// </summary>
public sealed record AgentActionLogEntry
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public AgentActionKind Kind { get; init; }

    /// <summary>Target path for create / source path for move / file path for edit.</summary>
    public string Path { get; init; } = "";
    /// <summary>Destination path — used by Move only. Empty otherwise.</summary>
    public string DestinationPath { get; init; } = "";

    /// <summary>
    /// The file's pre-action content, captured at execute time. Used by
    /// EditFile undo to restore the original verbatim. Null for non-Edit
    /// actions (CreateFile / CreateFolder / Move don't need this — their
    /// undo is just delete or move-back).
    /// </summary>
    public string? OriginalContent { get; init; }

    /// <summary>Status of the action — Done is the common case; Undone is set after a successful UndoLast.</summary>
    public AgentActionLogStatus Status { get; init; }

    public DateTimeOffset ExecutedAt { get; init; }

    /// <summary>Human-readable description for the side-panel list. Built from Kind + Path at execute time.</summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Post-action content captured at execute time, for redo. CreateFile
    /// stores the proposed file content here; EditFile stores the
    /// post-edit content. CreateFolder / Move leave it null (no content to
    /// restore on redo). Used by <c>IAgentActionService.RedoLastAsync</c>
    /// to re-apply the action after an undo.
    /// </summary>
    public string? NewContent { get; init; }
}

public enum AgentActionLogStatus { Done, Undone }
