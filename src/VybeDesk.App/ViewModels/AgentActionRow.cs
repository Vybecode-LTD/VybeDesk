using VybeDesk.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VybeDesk.App.ViewModels;

/// <summary>Display wrapper for a proposed agent action in the Notebook.</summary>
public sealed partial class AgentActionRow : ObservableObject
{
    public AgentActionRow(AgentAction action, string description, bool isValid, string status)
    {
        Action = action;
        Description = description;
        IsValid = isValid;
        _status = status;
    }

    public AgentAction Action { get; }
    public string Description { get; }
    public bool IsValid { get; }

    /// <summary>
    /// Anthropic tool_use_id that produced this action. Quoted back in the
    /// matching tool_result when the action runs (or is cancelled) so Claude
    /// can correlate.
    /// </summary>
    public string ToolUseId { get; init; } = "";

    [ObservableProperty]
    private string _status;
}
