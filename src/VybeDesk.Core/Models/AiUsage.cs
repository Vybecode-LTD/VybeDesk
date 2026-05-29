namespace VybeDesk.Core.Models;

/// <summary>Token usage from a single Anthropic API call, including prompt-cache counters.</summary>
public sealed record AiUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens = 0,
    int CacheReadInputTokens = 0)
{
    public static readonly AiUsage Empty = new(0, 0);

    public int TotalTokens => InputTokens + OutputTokens;
}
