namespace ClaudePM.Core.Models;

/// <summary>A note saved from the AI Notebook (Module 4).</summary>
public sealed class Note
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public Guid? ProjectId { get; set; }
    public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;
}
