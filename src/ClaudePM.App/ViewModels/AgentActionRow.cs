using ClaudePM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudePM.App.ViewModels;

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

    [ObservableProperty]
    private string _status;
}
