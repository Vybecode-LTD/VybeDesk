using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// Browses, validates, edits, and manages a library of folder-format skills
/// (<c>&lt;name&gt;/SKILL.md</c>). The flat <c>.skill</c> file format is NOT
/// supported by design — modern Claude skills ship as folders, and standalone
/// <c>.skill</c> files in the wild are usually ZIP archives that this service
/// would mis-parse as text.
/// </summary>
public interface ISkillLibraryService
{
    /// <summary>
    /// Finds every <c>SKILL.md</c> file under a folder (case-insensitive,
    /// recursive) and parses each into a <see cref="SkillFile"/>. Each
    /// <see cref="SkillFile.FullPath"/> points to the SKILL.md file itself;
    /// the parent folder is the skill's canonical home for resources.
    /// </summary>
    Task<IReadOnlyList<SkillFile>> ScanAsync(string folderPath, CancellationToken ct = default);

    /// <summary>Validates a single skill (frontmatter, name, description, body).</summary>
    IReadOnlyList<Finding> Validate(SkillFile skill);

    /// <summary>Finds skills that share a name.</summary>
    IReadOnlyList<Finding> FindDuplicates(IReadOnlyList<SkillFile> skills);

    /// <summary>Renders a skill back to <c>SKILL.md</c> file text.</summary>
    string Serialize(SkillFile skill);

    /// <summary>Writes the skill back to its existing file path.</summary>
    Task SaveAsync(SkillFile skill, CancellationToken ct = default);

    /// <summary>
    /// Exports the skill by duplicating its entire folder (including all
    /// resources) into <paramref name="targetFolder"/>. The destination is
    /// <c>&lt;targetFolder&gt;/&lt;skillFolderName&gt;</c>; fails if that
    /// already exists rather than silently overwriting. Returns the path of
    /// the exported folder.
    /// </summary>
    Task<string> ExportAsync(SkillFile skill, string targetFolder, CancellationToken ct = default);

    /// <summary>
    /// Backs up the skill by recursively copying its folder into
    /// <paramref name="targetFolder"/> as
    /// <c>&lt;skillFolderName&gt;-backup-&lt;yyyyMMdd-HHmmss&gt;/</c>.
    /// Always creates a new folder — never overwrites. Returns the backup
    /// folder path.
    /// </summary>
    Task<string> BackupAsync(SkillFile skill, string targetFolder, CancellationToken ct = default);

    /// <summary>
    /// Renames the skill's containing folder and updates its frontmatter
    /// <c>name:</c> to match. The <see cref="SkillFile.FullPath"/> and
    /// <see cref="SkillFile.Name"/> are updated in place. Fails on name-
    /// format violation or folder collision rather than guessing.
    /// </summary>
    Task RenameAsync(SkillFile skill, string newName, CancellationToken ct = default);

    /// <summary>
    /// Discovers the supporting files that live in the skill's own folder
    /// (everything except the skill's SKILL.md file itself) and populates the
    /// skill's <see cref="SkillFile.Resources"/> list. A bare skill folder
    /// with no extras simply ends up with an empty resource list.
    /// </summary>
    void PopulateResources(SkillFile skill);

    /// <summary>Reads the text contents of a single supporting resource file.</summary>
    Task<string> ReadResourceAsync(SkillResource resource, CancellationToken ct = default);
}
