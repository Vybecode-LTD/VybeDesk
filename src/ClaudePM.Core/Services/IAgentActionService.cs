using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>Result of validating an <see cref="AgentAction"/>.</summary>
public sealed record ActionValidation(bool IsValid, string Message);

/// <summary>
/// Performs allow-listed filesystem actions confined to scoped project roots,
/// with validation, execution, and undo (Module 4 — AI Notebook). Treats every
/// proposed action as untrusted input.
/// </summary>
public interface IAgentActionService
{
    IReadOnlyList<string> ScopedRoots { get; }
    void SetScopedRoots(IEnumerable<string> roots);

    /// <summary>A human-readable description of what the action would do.</summary>
    string Describe(AgentAction action);

    /// <summary>Checks the action is allow-listed and confined to a scoped root.</summary>
    ActionValidation Validate(AgentAction action);

    /// <summary>Executes the action and records it for undo. Throws on failure.</summary>
    Task ExecuteAsync(AgentAction action, CancellationToken ct = default);

    bool CanUndo { get; }
    IReadOnlyList<string> UndoHistory { get; }

    /// <summary>Reverses the most recently executed action.</summary>
    Task UndoLastAsync(CancellationToken ct = default);
}
