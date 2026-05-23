namespace ClaudePM.Core.Models;

public enum AgentActionKind { CreateFile, CreateFolder, Move }

/// <summary>
/// An allow-listed filesystem action proposed in the AI Notebook (Module 4).
/// </summary>
public sealed class AgentAction
{
    public AgentActionKind Kind { get; init; }

    /// <summary>Target path for create actions; source path for a move.</summary>
    public string Path { get; init; } = "";

    /// <summary>Destination path — used by Move only.</summary>
    public string DestinationPath { get; init; } = "";

    /// <summary>File contents — used by CreateFile only.</summary>
    public string Content { get; init; } = "";
}
