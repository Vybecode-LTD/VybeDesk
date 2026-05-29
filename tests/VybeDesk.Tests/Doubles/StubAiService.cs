using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Tests.Doubles;

/// <summary>
/// Lightweight test double for <see cref="IAiService"/>. Returns placeholder
/// text; never calls a real model. Moved here from the Services project
/// because production code should not ship test stubs.
/// </summary>
internal sealed class StubAiService : IAiService
{
    private const string Placeholder = "[Stub AI service — not wired to a real model.]";

    public bool IsConfigured => false;

    public Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
        => Task.FromResult(Placeholder);

    public Task<AgentChatResponse> AgentChatAsync(
        string systemPrompt,
        IReadOnlyList<AgentTurn> history,
        IReadOnlyList<AgentTool> tools,
        Action<string>? onTextDelta = null,
        Action<AiUsage>? onUsageDelta = null,
        CancellationToken ct = default)
    {
        onTextDelta?.Invoke(Placeholder);
        return Task.FromResult(new AgentChatResponse(
            StopReason: "end_turn",
            Blocks: new AgentContentBlock[] { new AgentTextBlock(Placeholder) },
            Usage: AiUsage.Empty));
    }
}
