using System.Text.Json;

namespace VybeDesk.Core.Models;

/// <summary>
/// One turn in an agent (tool-using) conversation with Claude. Distinct from
/// the simpler <see cref="ChatMessage"/>: a turn carries a list of typed
/// content blocks so we can represent text + tool_use + tool_result faithfully
/// in the API's expected shape.
/// </summary>
public sealed class AgentTurn
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public string Role { get; init; } = UserRole;
    public List<AgentContentBlock> Content { get; init; } = new();

    public static AgentTurn UserText(string text) =>
        new() { Role = UserRole, Content = { new AgentTextBlock(text) } };
}

/// <summary>Base for the typed content blocks inside an <see cref="AgentTurn"/>.</summary>
public abstract record AgentContentBlock;

/// <summary>Plain text — assistant prose or a user message.</summary>
public sealed record AgentTextBlock(string Text) : AgentContentBlock;

/// <summary>
/// A tool invocation Claude wants us to run. <see cref="Id"/> is the
/// tool_use_id we'll quote back in the matching <see cref="AgentToolResultBlock"/>.
/// <see cref="Input"/> is the tool's input JSON, reassembled from the streamed
/// input_json_delta events.
/// </summary>
public sealed record AgentToolUseBlock(string Id, string Name, JsonElement Input) : AgentContentBlock;

/// <summary>
/// The result of running a tool, sent back in the next user turn so Claude can
/// continue. <see cref="IsError"/> marks tool failures so Claude can recover.
/// </summary>
public sealed record AgentToolResultBlock(string ToolUseId, string Content, bool IsError = false) : AgentContentBlock;
