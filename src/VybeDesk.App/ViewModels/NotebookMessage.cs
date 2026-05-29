using System.Collections.ObjectModel;
using VybeDesk.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VybeDesk.App.ViewModels;

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
    /// True only while we're actively streaming an assistant reply that
    /// hasn't produced its first text delta yet. Drives a "thinking…"
    /// skeleton so the empty space between Send and the first character
    /// doesn't feel dead. Gated on <see cref="IsStreaming"/> so the
    /// placeholder vanishes cleanly when a turn ends without producing
    /// text (instead of sticking forever on an empty bubble).
    /// </summary>
    public bool ShowThinkingPlaceholder => IsAssistant && IsStreaming && !HasText;

    /// <summary>
    /// True while the assistant turn is still actively streaming. Drives the
    /// view to render <see cref="Text"/> as plaintext during the stream
    /// (so a partially-formed markdown code block doesn't make
    /// Markdown.Avalonia bail and blank the bubble), then swap to a
    /// markdown render once the turn settles.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettled))]
    [NotifyPropertyChangedFor(nameof(ShowThinkingPlaceholder))]
    [NotifyPropertyChangedFor(nameof(HasStreamingUsage))]
    private bool _isStreaming;

    public bool IsSettled => !IsStreaming;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUsage))]
    [NotifyPropertyChangedFor(nameof(UsageSummary))]
    private AiUsage? _usage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StreamingUsageSummary))]
    [NotifyPropertyChangedFor(nameof(HasStreamingUsage))]
    private AiUsage? _streamingUsage;

    [ObservableProperty]
    private double _costEstimate;

    public bool HasUsage => Usage is not null && Usage.TotalTokens > 0;

    public bool HasStreamingUsage => IsStreaming && StreamingUsage is not null;

    public string UsageSummary
    {
        get
        {
            if (Usage is null) return "";
            var s = $"{Usage.InputTokens:N0} in · {Usage.OutputTokens:N0} out";
            if (CostEstimate > 0) s += $" · ${CostEstimate:F4}";
            return s;
        }
    }

    public string StreamingUsageSummary
    {
        get
        {
            if (StreamingUsage is null) return "";
            return $"{StreamingUsage.InputTokens:N0} in · ~{StreamingUsage.OutputTokens:N0} out";
        }
    }

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
