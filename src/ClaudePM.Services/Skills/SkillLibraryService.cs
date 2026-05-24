using System.Text;
using System.Text.RegularExpressions;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Skills;

/// <summary>
/// Default <see cref="ISkillLibraryService"/>. Hand-parses the narrow YAML
/// frontmatter (name + description) used by .skill files — no YAML dependency.
/// </summary>
public sealed class SkillLibraryService : ISkillLibraryService
{
    private const int DescriptionLimit = 1024;
    private static readonly Regex NameRx = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public Task<IReadOnlyList<SkillFile>> ScanAsync(string folderPath, CancellationToken ct = default)
        => Task.Run<IReadOnlyList<SkillFile>>(() =>
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("Folder not found: " + folderPath);

            // Folder format ONLY (<name>/SKILL.md). Enumerate every .md file
            // recursively and keep only those literally named SKILL.md
            // (case-insensitive). The flat *.skill format is deliberately
            // unsupported — modern Claude skills are folders, and standalone
            // .skill files in the wild are usually ZIP archives that this
            // parser would render as garbage "PK..." bodies + "(no name)".
            var skills = new List<SkillFile>();
            foreach (var file in Directory.EnumerateFiles(
                         folderPath, "*.md", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (!Path.GetFileName(file).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
                    continue;
                try { skills.Add(Parse(file, File.ReadAllText(file))); }
                catch { /* skip unreadable file */ }
            }
            return skills.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }, ct);

    public IReadOnlyList<Finding> Validate(SkillFile s)
    {
        var f = new List<Finding>();
        var file = s.FileName;

        if (!s.HasFrontMatter)
            f.Add(new Finding(FindingSeverity.Critical, "Frontmatter",
                "No YAML frontmatter (--- block) found.", file));

        if (string.IsNullOrWhiteSpace(s.Name))
        {
            f.Add(new Finding(FindingSeverity.Critical, "Name", "Skill has no name.", file));
        }
        else
        {
            if (!NameRx.IsMatch(s.Name))
                f.Add(new Finding(FindingSeverity.Warning, "Name",
                    "Name should be lowercase words separated by hyphens.", file));
            if (s.Name.Contains("claude", StringComparison.OrdinalIgnoreCase))
                f.Add(new Finding(FindingSeverity.Warning, "Name",
                    "Avoid 'claude' in a skill name — it is reserved.", file));
        }

        if (string.IsNullOrWhiteSpace(s.Description))
        {
            f.Add(new Finding(FindingSeverity.Critical, "Description",
                "Skill has no description.", file));
        }
        else
        {
            var len = s.Description.Length;
            if (len >= DescriptionLimit)
                f.Add(new Finding(FindingSeverity.Critical, "Description",
                    "Description is " + len + " chars — must be under " + DescriptionLimit + ".", file));
            else if (len < 40)
                f.Add(new Finding(FindingSeverity.Warning, "Description",
                    "Description is very short — add trigger phrases.", file));

            var lower = s.Description.ToLowerInvariant();
            if (!lower.Contains("use when") && !lower.Contains("trigger")
                && !lower.Contains("when the user") && !lower.Contains("use this"))
                f.Add(new Finding(FindingSeverity.Info, "Description",
                    "Description may lack explicit 'use when' trigger guidance.", file));
        }

        if (string.IsNullOrWhiteSpace(s.Body))
            f.Add(new Finding(FindingSeverity.Warning, "Body", "Skill body is empty.", file));

        return f.OrderByDescending(x => x.Severity).ToList();
    }

    public IReadOnlyList<Finding> FindDuplicates(IReadOnlyList<SkillFile> skills)
    {
        var findings = new List<Finding>();
        foreach (var grp in skills
                     .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                     .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            findings.Add(new Finding(FindingSeverity.Critical, "Duplicate",
                "Skill name '" + grp.Key + "' is used by " + grp.Count() + " files.", grp.Key));
        }
        return findings;
    }

    public string Serialize(SkillFile s)
    {
        var sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append("name: ").Append(s.Name).Append('\n');
        sb.Append("description: >-\n");
        foreach (var line in WrapText(s.Description, 76))
            sb.Append("  ").Append(line).Append('\n');
        sb.Append("---\n\n");
        sb.Append(s.Body.TrimStart('\n'));
        if (!sb.ToString().EndsWith('\n')) sb.Append('\n');
        return sb.ToString();
    }

    public Task SaveAsync(SkillFile skill, CancellationToken ct = default)
        => File.WriteAllTextAsync(skill.FullPath, Serialize(skill), ct);

    public async Task<string> ExportAsync(
        SkillFile skill, string targetFolder, CancellationToken ct = default)
    {
        var sourceDir = SkillDirectoryOrThrow(skill);
        if (!Directory.Exists(targetFolder))
            Directory.CreateDirectory(targetFolder);

        var folderName = Path.GetFileName(sourceDir);
        var exportPath = Path.Combine(targetFolder, folderName);
        if (Directory.Exists(exportPath))
            throw new InvalidOperationException(
                "A folder named '" + folderName + "' already exists in the target. " +
                "Pick a different target or move/rename the existing folder first.");

        await Task.Run(() => CopyDirectory(sourceDir, exportPath), ct);
        return exportPath;
    }

    public async Task<string> BackupAsync(
        SkillFile skill, string targetFolder, CancellationToken ct = default)
    {
        var sourceDir = SkillDirectoryOrThrow(skill);
        if (!Directory.Exists(targetFolder))
            Directory.CreateDirectory(targetFolder);

        // Timestamp suffix guarantees uniqueness — backups never overwrite
        // each other even on rapid successive calls.
        var folderName = Path.GetFileName(sourceDir);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(targetFolder, folderName + "-backup-" + stamp);

        await Task.Run(() => CopyDirectory(sourceDir, backupPath), ct);
        return backupPath;
    }

    public async Task RenameAsync(SkillFile skill, string newName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New name cannot be empty.", nameof(newName));
        if (!NameRx.IsMatch(newName))
            throw new ArgumentException(
                "New name must be lowercase words separated by hyphens (e.g. my-skill).",
                nameof(newName));
        if (newName.Contains("claude", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "'claude' is reserved and cannot appear in a skill name.", nameof(newName));

        var sourceDir = SkillDirectoryOrThrow(skill);
        var parentDir = Path.GetDirectoryName(sourceDir);
        if (string.IsNullOrEmpty(parentDir))
            throw new InvalidOperationException(
                "Cannot determine the parent folder of the skill.");

        var newDir = Path.Combine(parentDir, newName);
        if (Directory.Exists(newDir))
            throw new InvalidOperationException(
                "A folder named '" + newName + "' already exists alongside the skill.");

        // Two-step rename: move the folder, then rewrite SKILL.md frontmatter
        // so the on-disk name and the in-frontmatter name stay in sync.
        await Task.Run(() => Directory.Move(sourceDir, newDir), ct);

        var skillFileName = Path.GetFileName(skill.FullPath);
        skill.FullPath = Path.Combine(newDir, skillFileName);
        skill.Name = newName;

        await File.WriteAllTextAsync(skill.FullPath, Serialize(skill), ct);

        // Resources still live under the new folder — refresh their paths.
        PopulateResources(skill);
    }

    /// <summary>
    /// Returns the directory containing the skill's SKILL.md file, or throws
    /// with a clear message if the skill has no path or the path no longer
    /// exists on disk.
    /// </summary>
    private static string SkillDirectoryOrThrow(SkillFile skill)
    {
        if (string.IsNullOrWhiteSpace(skill.FullPath))
            throw new InvalidOperationException("Skill has no file path.");
        var dir = Path.GetDirectoryName(skill.FullPath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            throw new DirectoryNotFoundException(
                "Skill folder not found on disk: " + (dir ?? "(null)"));
        return dir;
    }

    /// <summary>Recursive folder copy. Used by Export and Backup.</summary>
    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    // ---- parsing ------------------------------------------------------------

    private static SkillFile Parse(string path, string text)
    {
        var skill = new SkillFile { FullPath = path, FileName = Path.GetFileName(path) };
        var norm = text.Replace("\r\n", "\n");

        if (!norm.StartsWith("---\n"))
        {
            skill.HasFrontMatter = false;
            skill.Body = text;
            return skill;
        }

        var lines = norm.Split('\n');
        var end = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == "---") { end = i; break; }
        }
        if (end < 0)
        {
            skill.HasFrontMatter = false;
            skill.Body = text;
            return skill;
        }

        skill.HasFrontMatter = true;
        ParseFrontMatter(lines.Skip(1).Take(end - 1).ToArray(), skill);
        skill.Body = string.Join("\n", lines.Skip(end + 1)).TrimStart('\n');
        return skill;
    }

    private static void ParseFrontMatter(string[] fm, SkillFile skill)
    {
        for (var i = 0; i < fm.Length; i++)
        {
            var line = fm[i];

            var nameM = Regex.Match(line, @"^name:\s*(.*)$");
            if (nameM.Success)
            {
                skill.Name = Unquote(nameM.Groups[1].Value.Trim());
                continue;
            }

            var descM = Regex.Match(line, @"^description:\s*(.*)$");
            if (!descM.Success) continue;

            var rest = descM.Groups[1].Value.Trim();
            if (rest.StartsWith('>') || rest.StartsWith('|') || rest.Length == 0)
            {
                var literal = rest.StartsWith('|');
                var collected = new List<string>();
                var j = i + 1;
                for (; j < fm.Length; j++)
                {
                    var l = fm[j];
                    if (l.Length == 0) { collected.Add(""); continue; }
                    if (l[0] == ' ' || l[0] == '\t') collected.Add(l.Trim());
                    else break;
                }
                skill.Description = literal
                    ? string.Join("\n", collected).Trim()
                    : string.Join(" ", collected.Where(c => c.Length > 0)).Trim();
                i = j - 1;
            }
            else
            {
                skill.Description = Unquote(rest);
            }
        }
    }

    public void PopulateResources(SkillFile skill)
    {
        // A skill's resources are the other files that live in the same folder
        // as the skill/SKILL.md file. We deliberately walk the skill's own
        // directory only (and its subdirectories), never the whole library, so
        // one skill's resources never bleed into another's.
        skill.Resources.Clear();

        if (string.IsNullOrWhiteSpace(skill.FullPath))
            return;

        var skillDir = Path.GetDirectoryName(skill.FullPath);
        if (string.IsNullOrEmpty(skillDir) || !Directory.Exists(skillDir))
            return;

        foreach (var file in Directory.EnumerateFiles(
                     skillDir, "*", SearchOption.AllDirectories))
        {
            // Skip the skill file itself — it is shown in the main viewer,
            // not listed as one of its own resources.
            if (string.Equals(file, skill.FullPath, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip nested .skill files — those belong to other skills.
            if (Path.GetExtension(file).Equals(".skill", StringComparison.OrdinalIgnoreCase))
                continue;

            FileInfo info;
            try { info = new FileInfo(file); }
            catch { continue; }

            skill.Resources.Add(new SkillResource
            {
                FullPath = info.FullName,
                FileName = info.Name,
                RelativePath = Path.GetRelativePath(skillDir, info.FullName),
                SizeBytes = info.Length,
            });
        }

        // Stable, predictable ordering for the resource list.
        skill.Resources.Sort((a, b) =>
            string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> ReadResourceAsync(
        SkillResource resource, CancellationToken ct = default)
    {
        if (resource is null || string.IsNullOrWhiteSpace(resource.FullPath))
            return "";
        if (!File.Exists(resource.FullPath))
            return "[Resource file not found: " + resource.FullPath + "]";

        try
        {
            return await File.ReadAllTextAsync(resource.FullPath, ct);
        }
        catch (Exception ex)
        {
            // Binary files or unreadable files should not crash the viewer —
            // report the problem in the viewer instead.
            return "[Could not read this resource as text: " + ex.Message + "]";
        }
    }

    private static string Unquote(string s)
        => s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\''))
            ? s[1..^1]
            : s;

    private static IEnumerable<string> WrapText(string text, int width)
    {
        var words = text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var w in words)
        {
            if (line.Length > 0 && line.Length + 1 + w.Length > width)
            {
                yield return line.ToString();
                line.Clear();
            }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }
        if (line.Length > 0) yield return line.ToString();
    }
}
