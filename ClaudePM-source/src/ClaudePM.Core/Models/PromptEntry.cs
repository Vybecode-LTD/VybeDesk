namespace ClaudePM.Core.Models;

/// <summary>A reusable prompt in the global prompt library (Module 2).</summary>
public sealed class PromptEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "General";
    public List<string> Tags { get; set; } = new();
    public int UsageCount { get; set; }
    public bool IsFavorite { get; set; }
    public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset Modified { get; set; } = DateTimeOffset.Now;
}
