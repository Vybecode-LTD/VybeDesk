namespace ClaudePM.Core.Models;

/// <summary>
/// A supporting file that lives alongside a skill — e.g. a reference document
/// inside a skill folder. The skill manager lists these and shows the contents
/// of whichever one the user selects.
/// </summary>
public sealed class SkillResource
{
    /// <summary>Absolute path to the resource file on disk.</summary>
    public string FullPath { get; set; } = "";

    /// <summary>File name shown in the resource list (e.g. "reference.md").</summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// Path of the resource relative to the skill's own folder, used when the
    /// resource is nested (e.g. "references/api.md"). Falls back to the file
    /// name when the resource sits directly beside the skill.
    /// </summary>
    public string RelativePath { get; set; } = "";

    /// <summary>Size of the file in bytes, shown as a hint in the list.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Best label for the list: the relative path when known.</summary>
    public string DisplayName =>
        string.IsNullOrWhiteSpace(RelativePath) ? FileName : RelativePath;
}
