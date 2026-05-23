namespace ClaudePM.Core.Models;

/// <summary>A parsed .skill file from the skill library (Module 5).</summary>
public sealed class SkillFile
{
    public string FullPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Body { get; set; } = "";
    public bool HasFrontMatter { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(no name)" : Name;
}
