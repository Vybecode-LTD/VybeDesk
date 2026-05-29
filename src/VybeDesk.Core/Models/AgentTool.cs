using System.Text.Json;

namespace VybeDesk.Core.Models;

/// <summary>
/// A tool exposed to Claude in an agent chat call. <see cref="InputSchema"/> is
/// a JSON Schema describing the tool's input shape — Anthropic relays it to
/// Claude so the model knows how to construct a valid tool_use block.
/// </summary>
public sealed record AgentTool(string Name, string Description, JsonElement InputSchema);
