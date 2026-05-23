using System.Text;
using System.Text.RegularExpressions;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Docs;

/// <summary>
/// Default implementation of <see cref="IDocReconciliationService"/>. The
/// structural pass is fully local; the semantic pass delegates to IAiService.
/// </summary>
public sealed class DocReconciliationService : IDocReconciliationService
{
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
        { "bin", "obj", ".git", "node_modules", ".vs", ".idea" };

    private static readonly string[] EntryDocNames =
        { "readme.md", "claude.md", "agents.md", "index.md" };

    private static readonly Regex LinkRx =
        new(@"\[[^\]]*\]\(\s*([^)\s]+)", RegexOptions.Compiled);
    private static readonly Regex MarkerRx =
        new(@"\b(TODO|FIXME|XXX|HACK|WIP)\b|\[DRAFT\]", RegexOptions.Compiled);
    private static readonly Regex VersionRx =
        new(@"(?i)version\s*[:=]?\s*v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private readonly IAiService _ai;

    public DocReconciliationService(IAiService ai) => _ai = ai;

    public Task<IReadOnlyList<DocFile>> ScanAsync(string folderPath, CancellationToken ct = default)
        => Task.Run<IReadOnlyList<DocFile>>(() =>
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("Folder not found: " + folderPath);

            var root = Path.GetFullPath(folderPath);
            var docs = new List<DocFile>();
            foreach (var file in EnumerateDocFiles(root))
            {
                ct.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                docs.Add(new DocFile(
                    info.FullName,
                    Path.GetRelativePath(root, info.FullName),
                    info.Name,
                    info.Length,
                    info.LastWriteTime));
            }
            return docs
                .OrderBy(d => DocSortRank(d.Name))
                .ThenBy(d => d.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, ct);

    public async Task<IReadOnlyList<Finding>> AnalyzeStructuralAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct = default)
    {
        var contents = new Dictionary<DocFile, string>();
        foreach (var d in docs)
        {
            ct.ThrowIfCancellationRequested();
            try { contents[d] = await File.ReadAllTextAsync(d.FullPath, ct); }
            catch { contents[d] = ""; }
        }

        var findings = new List<Finding>();
        CheckDeadLinks(docs, contents, findings);
        CheckMarkers(docs, contents, findings);
        CheckOrphans(docs, contents, findings);
        CheckVersionDrift(contents, findings);
        CheckMissingDocs(docs, findings);
        CheckClaudeMdStaleness(docs, contents, findings);

        return findings.OrderByDescending(f => f.Severity).ToList();
    }

    public async Task<string> AnalyzeSemanticAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct = default)
    {
        if (docs.Count == 0) return "No documents to analyze.";

        const int perDocCap = 3000;
        const int maxDocs = 12;
        var sb = new StringBuilder();
        foreach (var d in docs.Take(maxDocs))
        {
            string text;
            try { text = await File.ReadAllTextAsync(d.FullPath, ct); }
            catch { continue; }
            if (text.Length > perDocCap) text = text[..perDocCap] + "\n[...truncated...]";
            sb.AppendLine("### " + d.RelativePath);
            sb.AppendLine(text);
            sb.AppendLine();
        }

        const string system =
            "You are auditing a project's documentation for INTERNAL CONSISTENCY. " +
            "The user provides multiple documents. Identify direct contradictions or " +
            "clearly outdated information BETWEEN documents. For each issue, name the " +
            "documents involved and describe it in one line. If the docs are consistent, " +
            "say so plainly. Output a concise markdown bullet list, nothing else.";

        return await _ai.CompleteAsync(system, sb.ToString(), ct);
    }

    public string BuildFixPrompt(
        string projectPath, IReadOnlyList<Finding> structural, string semanticResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Documentation reconciliation — fix request");
        sb.AppendLine();
        sb.AppendLine("The following issues were found in the documentation under `" + projectPath + "`.");
        sb.AppendLine("Fix them so the docs are accurate and internally consistent. Make the smallest");
        sb.AppendLine("correct change for each item. If you cannot resolve an item, flag it rather");
        sb.AppendLine("than guessing.");
        sb.AppendLine();
        sb.AppendLine("## Structural issues");
        if (structural.Count == 0)
            sb.AppendLine("- None found.");
        else
            foreach (var f in structural)
                sb.AppendLine("- [" + f.Severity.ToString().ToUpperInvariant() + "] "
                    + (string.IsNullOrEmpty(f.File) ? "(project-wide)" : f.File) + ": " + f.Message);
        sb.AppendLine();
        sb.AppendLine("## AI-detected consistency issues");
        sb.AppendLine(string.IsNullOrWhiteSpace(semanticResult)
            ? "- (semantic pass not run)" : semanticResult);
        sb.AppendLine();
        sb.AppendLine("When done, update any \"last updated\" markers or handoff sections you touched.");
        return sb.ToString();
    }

    public string BuildReportMarkdown(
        string projectPath, IReadOnlyList<Finding> structural, string semanticResult)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Documentation Reconciliation Report");
        sb.AppendLine();
        sb.AppendLine("Generated: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm"));
        sb.AppendLine("Project: `" + projectPath + "`");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine("- Critical: " + structural.Count(f => f.Severity == FindingSeverity.Critical));
        sb.AppendLine("- Warning: " + structural.Count(f => f.Severity == FindingSeverity.Warning));
        sb.AppendLine("- Info: " + structural.Count(f => f.Severity == FindingSeverity.Info));
        sb.AppendLine();
        sb.AppendLine("## Structural findings");
        if (structural.Count == 0)
        {
            sb.AppendLine("No structural issues found.");
        }
        else
        {
            sb.AppendLine("| Severity | File | Issue |");
            sb.AppendLine("|---|---|---|");
            foreach (var f in structural)
                sb.AppendLine("| " + f.Severity + " | "
                    + (string.IsNullOrEmpty(f.File) ? "(project-wide)" : f.File)
                    + " | " + f.Message.Replace("|", "\\|") + " |");
        }
        sb.AppendLine();
        sb.AppendLine("## AI semantic analysis");
        sb.AppendLine(string.IsNullOrWhiteSpace(semanticResult)
            ? "_Semantic pass not run._" : semanticResult);
        return sb.ToString();
    }

    public async Task<string> SaveReportAsync(
        string folderPath, string markdown, CancellationToken ct = default)
    {
        var path = Path.Combine(folderPath, "RECONCILIATION_REPORT.md");
        await File.WriteAllTextAsync(path, markdown, ct);
        return path;
    }

    // ---- structural checks --------------------------------------------------

    private static void CheckDeadLinks(
        IReadOnlyList<DocFile> docs, Dictionary<DocFile, string> contents, List<Finding> findings)
    {
        foreach (var d in docs)
        {
            var dir = Path.GetDirectoryName(d.FullPath) ?? "";
            foreach (Match m in LinkRx.Matches(contents[d]))
            {
                var raw = m.Groups[1].Value;
                if (IsExternal(raw)) continue;
                var target = raw.Split('#')[0];
                if (string.IsNullOrWhiteSpace(target)) continue;
                string resolved;
                try { resolved = Path.GetFullPath(Path.Combine(dir, Uri.UnescapeDataString(target))); }
                catch { continue; }
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                    findings.Add(new Finding(FindingSeverity.Critical, "Dead link",
                        "Link target does not exist: " + raw, d.RelativePath));
            }
        }
    }

    private static void CheckMarkers(
        IReadOnlyList<DocFile> docs, Dictionary<DocFile, string> contents, List<Finding> findings)
    {
        foreach (var d in docs)
        {
            var count = MarkerRx.Matches(contents[d]).Count;
            if (count > 0)
                findings.Add(new Finding(FindingSeverity.Info, "Marker debt",
                    "Contains " + count + " TODO/FIXME/DRAFT marker(s).", d.RelativePath));
        }
    }

    private static void CheckOrphans(
        IReadOnlyList<DocFile> docs, Dictionary<DocFile, string> contents, List<Finding> findings)
    {
        var linked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in docs)
        {
            var dir = Path.GetDirectoryName(d.FullPath) ?? "";
            foreach (Match m in LinkRx.Matches(contents[d]))
            {
                var raw = m.Groups[1].Value;
                if (IsExternal(raw)) continue;
                var target = raw.Split('#')[0];
                if (string.IsNullOrWhiteSpace(target)) continue;
                try { linked.Add(Path.GetFullPath(Path.Combine(dir, Uri.UnescapeDataString(target)))); }
                catch { /* ignore unparseable target */ }
            }
        }
        foreach (var d in docs)
        {
            if (EntryDocNames.Contains(d.Name.ToLowerInvariant())) continue;
            if (!linked.Contains(d.FullPath))
                findings.Add(new Finding(FindingSeverity.Info, "Orphaned doc",
                    "Not linked from any other document.", d.RelativePath));
        }
    }

    private static void CheckVersionDrift(
        Dictionary<DocFile, string> contents, List<Finding> findings)
    {
        var versions = new HashSet<string>();
        foreach (var text in contents.Values)
            foreach (Match m in VersionRx.Matches(text))
                versions.Add(m.Groups[1].Value);
        if (versions.Count > 1)
            findings.Add(new Finding(FindingSeverity.Warning, "Version drift",
                "Multiple version strings found across docs: "
                + string.Join(", ", versions.OrderBy(v => v)), ""));
    }

    private static void CheckMissingDocs(IReadOnlyList<DocFile> docs, List<Finding> findings)
    {
        bool Has(params string[] names)
            => docs.Any(d => names.Contains(d.Name.ToLowerInvariant()));

        if (!Has("readme.md"))
            findings.Add(new Finding(FindingSeverity.Info, "Missing doc",
                "No README.md found in the project.", ""));
        if (!Has("claude.md", "agents.md"))
            findings.Add(new Finding(FindingSeverity.Info, "Missing doc",
                "No CLAUDE.md or AGENTS.md context file found.", ""));
    }

    private static void CheckClaudeMdStaleness(
        IReadOnlyList<DocFile> docs, Dictionary<DocFile, string> contents, List<Finding> findings)
    {
        var claude = docs.FirstOrDefault(
            d => d.Name.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase));
        if (claude is null) return;
        if (!contents[claude].Contains("Last Completed Task", StringComparison.OrdinalIgnoreCase))
            return;

        var others = docs.Where(d => d != claude).ToList();
        if (others.Count == 0) return;

        var newest = others.Max(d => d.Modified);
        if (claude.Modified < newest - TimeSpan.FromDays(1))
            findings.Add(new Finding(FindingSeverity.Warning, "Stale context file",
                "CLAUDE.md is older than other docs — its \"Last Completed Task\" may be stale.",
                claude.RelativePath));
    }

    // ---- helpers ------------------------------------------------------------

    private static bool IsExternal(string target)
        => target.StartsWith('#')
        || target.Contains("://")
        || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("//");

    private static int DocSortRank(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.StartsWith("readme")) return 0;
        if (n is "claude.md" or "agents.md") return 1;
        if (n.Contains("architecture") || n.Contains("spec")) return 2;
        return 5;
    }

    private static IEnumerable<string> EnumerateDocFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(dir); }
            catch { continue; }
            foreach (var sd in subdirs)
                if (!SkipDirs.Contains(Path.GetFileName(sd)))
                    stack.Push(sd);

            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { continue; }
            foreach (var f in files)
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".md" or ".txt")
                    yield return f;
            }
        }
    }
}
