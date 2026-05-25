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

    /// <summary>
    /// Supporting files bundled with this skill (the contents of its skill
    /// folder, excluding the skill/SKILL.md file itself). Empty for a bare,
    /// single-file skill that has no folder of resources.
    /// </summary>
    public List<SkillResource> Resources { get; set; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(no name)" : Name;

    /// <summary>True when this skill has at least one supporting resource file.</summary>
    public bool HasResources => Resources.Count > 0;
}
