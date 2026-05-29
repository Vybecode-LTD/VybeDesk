namespace VybeDesk.Core.Models;

public enum AgentActionKind { CreateFile, CreateFolder, Move, EditFile }

/// <summary>
/// An allow-listed filesystem action proposed in the AI Notebook (Module 4).
/// </summary>
public sealed class AgentAction
{
    public AgentActionKind Kind { get; init; }

    /// <summary>Target path for create actions; source path for a move; file path for an edit.</summary>
    public string Path { get; init; } = "";

    /// <summary>Destination path — used by Move only.</summary>
    public string DestinationPath { get; init; } = "";

    /// <summary>File contents — used by CreateFile only.</summary>
    public string Content { get; init; } = "";

    /// <summary>Exact text the EditFile action will find. Must be non-empty.</summary>
    public string OldString { get; init; } = "";

    /// <summary>Replacement text — may be empty (= deletion). Used by EditFile only.</summary>
    public string NewString { get; init; } = "";

    /// <summary>When true, EditFile replaces every match of OldString rather than requiring exactly one.</summary>
    public bool ReplaceAll { get; init; }
}
