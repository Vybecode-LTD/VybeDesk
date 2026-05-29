using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>
/// Abstraction over the Claude API. ViewModels depend on this, never on the
/// SDK directly. The real implementation reads the key from ISecureKeyStore.
/// </summary>
public interface IAiService
{
    bool IsConfigured { get; }

    /// <summary>A single-shot completion with one user prompt.</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);

    /// <summary>
    /// Streaming, tool-using agent chat. Streams assistant text deltas through
    /// <paramref name="onTextDelta"/> as they arrive, accumulates the full
    /// content blocks (text + tool_use), and returns when the model stops.
    /// When <see cref="AgentChatResponse.WantsToolResults"/> is true the
    /// caller must run the requested tools and continue the conversation by
    /// appending an assistant turn (from the response blocks) and a user turn
    /// of <see cref="AgentToolResultBlock"/>s.
    /// </summary>
    Task<AgentChatResponse> AgentChatAsync(
        string systemPrompt,
        IReadOnlyList<AgentTurn> history,
        IReadOnlyList<AgentTool> tools,
        Action<string>? onTextDelta = null,
        Action<AiUsage>? onUsageDelta = null,
        CancellationToken ct = default);
}
