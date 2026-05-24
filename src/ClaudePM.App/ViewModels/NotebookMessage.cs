using System.Collections.ObjectModel;
using ClaudePM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// Chat row in the Notebook UI. Distinct from the Core <see cref="ChatMessage"/>
/// record because we need an observable <see cref="Text"/> for streamed deltas
/// and an <see cref="Activities"/> collection for the per-turn tool-use chips.
/// </summary>
public sealed partial class NotebookMessage : ObservableObject
{
    public string Role { get; }
    public bool IsAssistant => Role == ChatMessage.AssistantRole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText))]
    [NotifyPropertyChangedFor(nameof(ShowThinkingPlaceholder))]
    private string _text;

    public bool HasText => !string.IsNullOrEmpty(Text);

    /// <summary>
    /// True while we're still waiting on the first text delta for an assistant
    /// reply. Drives a "thinking…" skeleton in the bubble so the empty space
    /// between Send and the first streamed character doesn't feel dead.
    /// </summary>
    public bool ShowThinkingPlaceholder => IsAssistant && !HasText;

    /// <summary>
    /// True while the assistant turn is still actively streaming. Drives the
    /// view to render <see cref="Text"/> as plaintext during the stream
    /// (so a partially-formed markdown code block doesn't make
    /// Markdown.Avalonia bail and blank the bubble), then swap to a
    /// markdown render once the turn settles.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettled))]
    private bool _isStreaming;

    public bool IsSettled => !IsStreaming;

    /// <summary>
    /// Tool invocations Claude made (or proposed) in this turn — auto-executed
    /// read tools, queued write tools, blocked attempts. Rendered as italic
    /// colored chips beneath the prose.
    /// </summary>
    public ObservableCollection<ToolActivity> Activities { get; } = new();

    public NotebookMessage(string role, string text)
    {
        Role = role;
        _text = text;
    }
}
