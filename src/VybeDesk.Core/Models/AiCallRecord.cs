namespace VybeDesk.Core.Models;

/// <summary>One row in the AI call log (M3 #12). Captures tokens, cost, and provenance for every Anthropic API call.</summary>
public sealed class AiCallRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public string Module { get; set; } = "";
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheCreationInputTokens { get; set; }
    public int CacheReadInputTokens { get; set; }
    public double CostEstimate { get; set; }
    public int DurationMs { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
