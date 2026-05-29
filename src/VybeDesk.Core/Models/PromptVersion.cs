namespace VybeDesk.Core.Models;

/// <summary>
/// A historical snapshot of a <see cref="PromptEntry"/>. Captured just before
/// an update changes the prompt's content (title / body / category / tags), so
/// the user can browse and restore prior versions. Usage-count-only updates
/// (e.g. recording a template fill) do not produce a version.
/// </summary>
public sealed class PromptVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PromptId { get; init; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "General";
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset Captured { get; init; } = DateTimeOffset.Now;
}
