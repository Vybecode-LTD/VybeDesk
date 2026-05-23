using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Browses, validates, and edits a library of .skill files (Module 5).
/// </summary>
public interface ISkillLibraryService
{
    /// <summary>Finds and parses every .skill file under a folder.</summary>
    Task<IReadOnlyList<SkillFile>> ScanAsync(string folderPath, CancellationToken ct = default);

    /// <summary>Validates a single skill (frontmatter, name, description, body).</summary>
    IReadOnlyList<Finding> Validate(SkillFile skill);

    /// <summary>Finds skills that share a name.</summary>
    IReadOnlyList<Finding> FindDuplicates(IReadOnlyList<SkillFile> skills);

    /// <summary>Renders a skill back to .skill file text.</summary>
    string Serialize(SkillFile skill);

    /// <summary>Writes the skill back to its existing file path.</summary>
    Task SaveAsync(SkillFile skill, CancellationToken ct = default);

    /// <summary>Writes the skill as &lt;name&gt;.skill into a folder; returns the path.</summary>
    Task<string> ExportAsync(SkillFile skill, string folderPath, CancellationToken ct = default);
}
