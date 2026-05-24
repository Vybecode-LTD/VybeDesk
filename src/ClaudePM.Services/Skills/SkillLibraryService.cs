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

            var skills = new List<SkillFile>();

            // Legacy flat format: <folder>/<name>.skill
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.skill", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { skills.Add(Parse(file, File.ReadAllText(file), folderFormat: false)); }
                catch { /* skip unreadable file */ }
            }

            // Modern Claude Code format: <folder>/<name>/SKILL.md (pattern is
            // case-insensitive on Windows so SKILL.md / Skill.md / skill.md
            // all match).
            foreach (var file in Directory.EnumerateFiles(folderPath, "SKILL.md", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try { skills.Add(Parse(file, File.ReadAllText(file), folderFormat: true)); }
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

    public IReadOnlyList<SkillResource> GetResources(SkillFile skill)
    {
        // Only folder-format skills (foo/SKILL.md) have a resource folder;
        // flat *.skill files live alone with nothing alongside.
        var isFolderFormat = skill.FileName.EndsWith(
            "/SKILL.md", StringComparison.OrdinalIgnoreCase);
        if (!isFolderFormat) return Array.Empty<SkillResource>();

        var folder = Path.GetDirectoryName(skill.FullPath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            return Array.Empty<SkillResource>();

        const int maxEntries = 200;
        var list = new List<SkillResource>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(
                folder, "*", SearchOption.AllDirectories))
            {
                // Exclude the SKILL.md itself — that's the skill, not a
                // resource.
                if (Path.GetFileName(file).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
                    continue;

                var rel = Path.GetRelativePath(folder, file).Replace('\\', '/');
                long size;
                try { size = new FileInfo(file).Length; } catch { size = 0; }
                list.Add(new SkillResource(rel, file, size));

                if (list.Count >= maxEntries) break;
            }
        }
        catch
        {
            // Unreadable folder — return whatever we've collected.
        }

        list.Sort((a, b) => StringComparer.OrdinalIgnoreCase
            .Compare(a.RelativePath, b.RelativePath));
        return list;
    }

    public async Task<string> ExportAsync(
        SkillFile skill, string folderPath, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(skill.Name) ? "skill" : skill.Name;
        var serialized = Serialize(skill);

        // Dual-format export: a flat *.skill file (Claude web) and a
        // folder/SKILL.md (Claude Code). Lets the same skill be loaded by
        // either runtime without manual conversion.
        var flatPath = Path.Combine(folderPath, name + ".skill");
        await File.WriteAllTextAsync(flatPath, serialized, ct);

        var skillDir = Path.Combine(folderPath, name);
        Directory.CreateDirectory(skillDir);
        var skillMdPath = Path.Combine(skillDir, "SKILL.md");
        await File.WriteAllTextAsync(skillMdPath, serialized, ct);

        return flatPath + "  +  " + skillMdPath;
    }

    // ---- parsing ------------------------------------------------------------

    private static SkillFile Parse(string path, string text, bool folderFormat)
    {
        // For folder-format skills (foo/SKILL.md), the file is always literally
        // named "SKILL.md" so we use "<folder>/SKILL.md" as the display
        // FileName — otherwise the secondary line in the list would be the
        // same string for every modern skill.
        var fileName = folderFormat
            ? Path.GetFileName(Path.GetDirectoryName(path) ?? "") + "/SKILL.md"
            : Path.GetFileName(path);
        var skill = new SkillFile { FullPath = path, FileName = fileName };
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
