using ClaudePM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Chat row in the Notebook UI. Distinct from the Core <see cref="ChatMessage"/>
/// record because we need an observable <see cref="Text"/> so the live UI
/// updates as the assistant's streamed text deltas arrive.
/// </summary>
public sealed partial class NotebookMessage : ObservableObject
{
    public string Role { get; }
    public bool IsAssistant => Role == ChatMessage.AssistantRole;

    [ObservableProperty]
    private string _text;

    public NotebookMessage(string role, string text)
    {
        Role = role;
        _text = text;
    }
}
