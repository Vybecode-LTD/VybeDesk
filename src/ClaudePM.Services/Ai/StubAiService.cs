using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Ai;

/// <summary>
/// STUB AI service. Returns a placeholder. Retained as a lightweight test
/// double; the app wires <see cref="AnthropicChatService"/> in production.
/// </summary>
public sealed class StubAiService : IAiService
{
    public bool IsConfigured => false;

    public Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, CancellationToken ct = default)
        => Task.FromResult("[Stub AI service — not wired to a real model.]");

    public Task<string> ChatAsync(
        string systemPrompt, IReadOnlyList<ChatMessage> history, CancellationToken ct = default)
        => Task.FromResult("[Stub AI service — not wired to a real model.]");
}
