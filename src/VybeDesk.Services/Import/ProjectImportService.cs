using System.Globalization;
using System.Text;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Docs;

namespace VybeDesk.Services.Import;

/// <summary>
/// Default <see cref="IProjectImportService"/>. Ingests a folder as a
/// Project, harvesting any context already present on disk:
/// <list type="bullet">
/// <item><c>CLAUDE.md</c> at the folder root → <see cref="Project.Description"/>
/// (truncated to <see cref="MaxDescriptionChars"/>).</item>
/// <item>Last <c>git</c> commit time on the folder → <see cref="Project.LastActivity"/>;
/// falls back to <see cref="Directory.GetLastWriteTime(string)"/> when git
/// is missing or the folder isn't a repo.</item>
/// <item>Every <c>.claude/commands/**/*.md</c> file → a new
/// <see cref="PromptEntry"/> tagged with the project name, skipping
/// duplicates that already exist under the same title + tag.</item>
/// </list>
/// The <c>.claude/skills/</c> half of the import is deliberately deferred to
/// the Module 5 rewrite (M6); this service does not touch it.
/// </summary>
public sealed class ProjectImportService : IProjectImportService
{
    /// <summary>
    /// Hard cap for the imported CLAUDE.md content stored as
    /// <see cref="Project.Description"/>. Large CLAUDE.md files (this very
    /// app's own is ~9 KB) would otherwise dominate the projects table.
    /// </summary>
    public const int MaxDescriptionChars = 4000;

    /// <summary>
    /// Per-prompt body cap. Commands directories occasionally contain very
    /// long playbooks; we keep what we can without bloating the prompts table.
    /// </summary>
    public const int MaxPromptBodyChars = 16 * 1024;

    private const string TruncationMarker = "\n\n[...truncated]";

    private readonly IProjectStore _projects;
    private readonly IPromptStore _prompts;

    public ProjectImportService(IProjectStore projects, IPromptStore prompts)
    {
        _projects = projects;
        _prompts = prompts;
    }

    public async Task<ProjectImportResult> ImportFromFolderAsync(
        string folderPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return new ProjectImportResult(
                Success: false, Project: null,
                PromptsImported: 0, PromptsSkippedDuplicate: 0,
                HadGitTimestamp: false, HadClaudeMd: false,
                Message: "No folder path supplied.");
        }

        var trimmed = folderPath.Trim();
        if (!Directory.Exists(trimmed))
        {
            return new ProjectImportResult(
                Success: false, Project: null,
                PromptsImported: 0, PromptsSkippedDuplicate: 0,
                HadGitTimestamp: false, HadClaudeMd: false,
                Message: "Folder doesn't exist: " + trimmed);
        }

        string absoluteFolder;
        try
        {
            absoluteFolder = Path.GetFullPath(trimmed);
        }
        catch (Exception ex)
        {
            return new ProjectImportResult(
                Success: false, Project: null,
                PromptsImported: 0, PromptsSkippedDuplicate: 0,
                HadGitTimestamp: false, HadClaudeMd: false,
                Message: "Could not resolve folder path: " + ex.Message);
        }

        // 1. Derive a project name from the last path segment. Falls back
        //    to a generic label if the user picked a drive root or similar.
        var projectName = DeriveProjectName(absoluteFolder);

        // 2. Read CLAUDE.md if present. Truncated; BOM-stripped; UTF-8.
        var (description, hadClaudeMd) =
            await ReadClaudeMdAsync(absoluteFolder, ct).ConfigureAwait(false);

        // 3. Last git commit time for the folder, or directory mtime as
        //    fallback when git is missing / not a repo / untracked.
        var (lastActivity, hadGit) =
            await ResolveLastActivityAsync(absoluteFolder, ct).ConfigureAwait(false);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = projectName,
            Description = description,
            FolderPath = absoluteFolder,
            Status = ProjectStatus.Active,
            LastActivity = lastActivity,
            LogoPath = TryDetectLogo(absoluteFolder),
        };

        await _projects.AddAsync(project, ct).ConfigureAwait(false);

        // 4. Walk .claude/commands/**/*.md for promptable command files.
        var (imported, skipped) = await ImportCommandPromptsAsync(
            absoluteFolder, projectName, ct).ConfigureAwait(false);

        var summary = BuildSummary(projectName, imported, skipped, hadClaudeMd, hadGit);

        return new ProjectImportResult(
            Success: true,
            Project: project,
            PromptsImported: imported,
            PromptsSkippedDuplicate: skipped,
            HadGitTimestamp: hadGit,
            HadClaudeMd: hadClaudeMd,
            Message: summary);
    }

    // --- name --------------------------------------------------------------

    private static string DeriveProjectName(string absoluteFolder)
    {
        // Path.GetFileName on a trailing-slash path returns "". Trim those
        // off so users picking "C:\dev\foo\" don't end up with a blank name.
        var trimmed = absoluteFolder.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var last = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(last) ? "Imported project" : last;
    }

    // --- CLAUDE.md ---------------------------------------------------------

    private static async Task<(string description, bool hadClaudeMd)>
        ReadClaudeMdAsync(string folder, CancellationToken ct)
    {
        var path = Path.Combine(folder, "CLAUDE.md");
        if (!File.Exists(path)) return ("", false);

        try
        {
            var text = await File.ReadAllTextAsync(path, Encoding.UTF8, ct)
                .ConfigureAwait(false);
            // Strip a UTF-8 BOM if File.ReadAllTextAsync handed one back.
            if (text.Length > 0 && text[0] == '﻿')
                text = text.Substring(1);

            if (text.Length > MaxDescriptionChars)
                text = text.Substring(0, MaxDescriptionChars) + TruncationMarker;

            return (text, true);
        }
        catch
        {
            // Unreadable CLAUDE.md isn't a hard failure — we still create the
            // project with an empty description.
            return ("", false);
        }
    }

    // --- LastActivity ------------------------------------------------------

    private static async Task<(DateTimeOffset lastActivity, bool hadGit)>
        ResolveLastActivityAsync(string folder, CancellationToken ct)
    {
        // GitInfo gracefully returns null when git isn't on PATH, the folder
        // isn't a repo, or the call times out — perfect for the soft signal
        // we want here.
        var gitTime = await GitInfo.GetLastCommitTimeAsync(folder, null, ct)
            .ConfigureAwait(false);
        if (gitTime is not null)
            return (gitTime.Value, true);

        try
        {
            var mtime = Directory.GetLastWriteTime(folder);
            return (new DateTimeOffset(mtime), false);
        }
        catch
        {
            // Permissions / weird filesystem — fall back to "now" so the row
            // is still valid.
            return (DateTimeOffset.Now, false);
        }
    }

    // --- logo auto-detect -------------------------------------------------

    /// <summary>
    /// Best-effort logo discovery: walks a short priority list of well-known
    /// filenames at the project root, then falls back to a single
    /// <c>*logo*.png</c> glob at the top level. Returns the absolute path of
    /// the first hit, or null when nothing matches. Permissions errors during
    /// the glob fall back to null so the import keeps succeeding.
    /// </summary>
    private static string? TryDetectLogo(string folderPath)
    {
        // Priority order: more specific first.
        var candidates = new[]
        {
            Path.Combine(folderPath, "favicon.ico"),
            Path.Combine(folderPath, "logo.png"),
            Path.Combine(folderPath, "logo.svg"),
            Path.Combine(folderPath, "logo.jpg"),
            Path.Combine(folderPath, "icon.png"),
            Path.Combine(folderPath, "icon.svg"),
            Path.Combine(folderPath, ".claude", "icon.png"),
            Path.Combine(folderPath, ".claude", "logo.png"),
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;

        // Glob fallback: anything matching *logo*.png at the root.
        try
        {
            var glob = Directory
                .EnumerateFiles(folderPath, "*logo*.png", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (glob is not null) return glob;
        }
        catch { /* permissions — skip */ }
        return null;
    }

    // --- .claude/commands/*.md --------------------------------------------

    private async Task<(int imported, int skipped)> ImportCommandPromptsAsync(
        string folder, string projectName, CancellationToken ct)
    {
        var commandsDir = Path.Combine(folder, ".claude", "commands");
        if (!Directory.Exists(commandsDir)) return (0, 0);

        // Lower-cased tag — matches the convention used by every other tag in
        // the prompt library (search is case-insensitive but the canonical form
        // is lower-case for consistency).
        var projectTag = projectName.ToLowerInvariant();

        // Pre-load the existing prompts once and build a fast lookup keyed on
        // (Title-lowercased, hasProjectTag). One DB read is cheaper than N
        // searches.
        var existing = await _prompts.GetAllAsync(ct).ConfigureAwait(false);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existing)
        {
            if (p.Tags.Any(t => string.Equals(t, projectTag, StringComparison.OrdinalIgnoreCase)))
                seen.Add(p.Title);
        }

        var imported = 0;
        var skipped = 0;

        IEnumerable<string> mdFiles;
        try
        {
            mdFiles = Directory.EnumerateFiles(
                commandsDir, "*.md", SearchOption.AllDirectories);
        }
        catch
        {
            // Unreadable subtree — nothing to import.
            return (0, 0);
        }

        foreach (var file in mdFiles)
        {
            ct.ThrowIfCancellationRequested();

            var title = TitleFromFileName(file);
            if (string.IsNullOrWhiteSpace(title)) continue;

            if (seen.Contains(title))
            {
                skipped++;
                continue;
            }

            string body;
            try
            {
                body = await File.ReadAllTextAsync(file, Encoding.UTF8, ct)
                    .ConfigureAwait(false);
            }
            catch
            {
                continue; // skip silently — one bad file shouldn't fail the import
            }

            if (body.Length > 0 && body[0] == '﻿')
                body = body.Substring(1);
            if (body.Length > MaxPromptBodyChars)
                body = body.Substring(0, MaxPromptBodyChars) + TruncationMarker;

            var entry = new PromptEntry
            {
                Title = title,
                Body = body,
                Category = "Imported",
                Tags = new List<string> { projectTag },
            };
            await _prompts.AddAsync(entry, ct).ConfigureAwait(false);

            seen.Add(title); // protect against duplicate file stems within the same import
            imported++;
        }

        return (imported, skipped);
    }

    /// <summary>
    /// Turns <c>review-pr.md</c> into <c>Review Pr</c>, <c>fix_bug.md</c>
    /// into <c>Fix Bug</c>, and so on. Hyphens and underscores become
    /// spaces; each word's first character is upper-cased. Deliberately
    /// minimal — no library to load, no surprising rules to debug.
    /// </summary>
    internal static string TitleFromFileName(string filePath)
    {
        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(stem)) return "";

        // Replace separators with spaces, collapse repeats.
        var raw = stem.Replace('-', ' ').Replace('_', ' ').Trim();
        if (raw.Length == 0) return "";

        var sb = new StringBuilder(raw.Length);
        var capitalize = true;
        foreach (var ch in raw)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                capitalize = true;
                continue;
            }
            sb.Append(capitalize ? char.ToUpper(ch, CultureInfo.InvariantCulture) : ch);
            capitalize = false;
        }
        return sb.ToString();
    }

    // --- diagnostics -------------------------------------------------------

    private static string BuildSummary(
        string projectName, int imported, int skipped,
        bool hadClaudeMd, bool hadGit)
    {
        var bits = new List<string>();
        bits.Add(hadClaudeMd ? "CLAUDE.md found" : "no CLAUDE.md");
        bits.Add(hadGit ? "git timestamp" : "directory mtime");
        if (imported > 0) bits.Add(imported + " prompt(s) imported");
        if (skipped > 0) bits.Add(skipped + " duplicate prompt(s) skipped");
        return "Imported '" + projectName + "' (" + string.Join(", ", bits) + ").";
    }
}
