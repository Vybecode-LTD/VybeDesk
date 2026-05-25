namespace ClaudePM.Core.Models;

public sealed record AiUsage(
    int InputTokens,
    int OutputTokens,
    int CacheCreationInputTokens = 0,
    int CacheReadInputTokens = 0)
{
    public static readonly AiUsage Empty = new(0, 0);

    public int TotalTokens => InputTokens + OutputTokens;
}
