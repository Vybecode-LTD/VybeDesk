using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Abstraction over the Claude API. ViewModels depend on this, never on the
/// SDK directly. The real implementation reads the key from ISecureKeyStore.
/// </summary>
public interface IAiService
{
    bool IsConfigured { get; }

    /// <summary>A single-shot completion with one user prompt.</summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);

    /// <summary>A multi-turn chat completion over a conversation history.</summary>
    Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default);
}
