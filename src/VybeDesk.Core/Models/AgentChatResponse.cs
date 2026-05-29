namespace VybeDesk.Core.Models;

/// <summary>
/// The assistant turn returned by an agent chat call: the ordered list of
/// content blocks the model produced plus the API's stop_reason. The caller
/// usually appends this as a new assistant <see cref="AgentTurn"/> in the
/// conversation history before sending the next user turn.
/// </summary>
public sealed record AgentChatResponse(
    string StopReason,
    IReadOnlyList<AgentContentBlock> Blocks,
    AiUsage Usage,
    string Model = "")
{
    /// <summary>All text the assistant produced, concatenated.</summary>
    public string TextOutput =>
        string.Concat(Blocks.OfType<AgentTextBlock>().Select(b => b.Text));

    /// <summary>Tool calls the assistant wants the host to run.</summary>
    public IReadOnlyList<AgentToolUseBlock> ToolUses =>
        Blocks.OfType<AgentToolUseBlock>().ToList();

    /// <summary>
    /// True when the model stopped because it wants tool results — the caller
    /// must run the tools and continue the conversation.
    /// </summary>
    public bool WantsToolResults => StopReason == "tool_use";
}
