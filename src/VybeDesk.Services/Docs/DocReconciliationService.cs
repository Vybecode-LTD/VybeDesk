using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Utilities;

namespace VybeDesk.Services.Docs;

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
        string projectRoot, IReadOnlyList<DocFile> docs, CancellationToken ct = default)
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
        await CheckGitStalenessAsync(projectRoot, docs, findings, ct);

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

    public async Task<ProjectAuditReport> AuditAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct = default)
    {
        if (docs.Count == 0) return ProjectAuditReport.Empty;

        const int perDocCap = 4000;
        const int maxDocs = 12;

        // Signal-weighted: read order matters because token budget caps the
        // bundle. CLAUDE.md (current state) and CHANGELOG.md (what shipped)
        // come first; ROADMAP / SPEC give intent; README / KICKOFF give
        // overview; docs/ and the rest backfill.
        var prioritized = docs
            .OrderBy(DocPriority)
            .ThenBy(d => d.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maxDocs)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Documents to audit");
        sb.AppendLine();
        foreach (var d in prioritized)
        {
            string text;
            try { text = await File.ReadAllTextAsync(d.FullPath, ct); }
            catch { continue; }
            if (text.Length > perDocCap)
                text = text[..perDocCap] + "\n[...truncated...]";
            sb.AppendLine("## " + d.RelativePath);
            sb.AppendLine("```");
            sb.AppendLine(text);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        const string system =
            "You are auditing a software project by reading its documentation. " +
            "Return ONLY a JSON object (no prose, no markdown fences) with this " +
            "exact shape:\n" +
            "{\n" +
            "  \"design\": \"<2-5 sentence prose summary of what the project is " +
            "and how it's architected, synthesized from the docs>\",\n" +
            "  \"roadmapItems\": [\n" +
            "    {\n" +
            "      \"title\": \"<short item name>\",\n" +
            "      \"status\": \"complete\" | \"incomplete\" | \"unknown\",\n" +
            "      \"category\": \"feature\" | \"gate\" | \"phase\" | \"fix\",\n" +
            "      \"source\": \"<file name where the item is declared, e.g. ROADMAP.md>\",\n" +
            "      \"evidence\": \"<one-line quote or paraphrase justifying the status>\"\n" +
            "    }\n" +
            "  ],\n" +
            "  \"inconsistencies\": [\n" +
            "    {\n" +
            "      \"severity\": \"critical\" | \"warning\" | \"info\",\n" +
            "      \"docs\": [\"A.md\", \"B.md\"],\n" +
            "      \"issue\": \"<one-line description of the disagreement>\"\n" +
            "    }\n" +
            "  ]\n" +
            "}\n" +
            "\n" +
            "Rules:\n" +
            "- List every roadmap-style item you can identify across all docs " +
            "  (features, gates, phases, fixes, milestones). Don't invent items.\n" +
            "- A checkbox checked, a CHANGELOG entry, or 'shipped'/'done' language " +
            "  counts as complete. A roadmap entry without those signals is incomplete.\n" +
            "- Inconsistencies = real disagreements (version mismatch, feature claimed " +
            "  in one doc but missing in another, contradictory status). Don't list " +
            "  stylistic differences.\n" +
            "- If there's no inconsistency, return an empty array.";

        var raw = await _ai.CompleteAsync(system, sb.ToString(), ct);
        var report = ParseAuditPayload(raw);
        if (report == ProjectAuditReport.Empty && !string.IsNullOrWhiteSpace(raw))
        {
            // The AI returned something but it didn't parse as the expected JSON.
            // Surface it as the design field so the user can see what happened.
            var preview = raw.Length > 500 ? raw[..500] + "…" : raw;
            throw new InvalidOperationException(
                "Audit response could not be parsed as structured JSON. Raw response:\n" + preview);
        }
        return report;
    }

    public string BuildAuditFixPrompt(
        string projectPath, IReadOnlyList<AuditInconsistency> inconsistencies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Documentation audit — fix inconsistencies");
        sb.AppendLine();
        sb.AppendLine("The following inconsistencies were found across the documentation under");
        sb.AppendLine("`" + projectPath + "`. Resolve each one by making the smallest correct");
        sb.AppendLine("change to bring the docs into agreement. If a fact is genuinely unknown,");
        sb.AppendLine("flag it with [TBD: ...] rather than guessing.");
        sb.AppendLine();
        if (inconsistencies.Count == 0)
        {
            sb.AppendLine("- None found.");
        }
        else
        {
            foreach (var inc in inconsistencies)
            {
                sb.AppendLine("- [" + inc.Severity.ToString().ToUpperInvariant() + "] "
                    + string.Join(" + ", inc.Docs) + ": " + inc.Issue);
            }
        }
        sb.AppendLine();
        sb.AppendLine("When done, update any \"last updated\" markers or handoff sections you touched.");
        return sb.ToString();
    }

    /// <summary>
    /// Priority weight for the audit bundle. Lower = read sooner; higher =
    /// backfill / skip if the budget runs out.
    /// </summary>
    private static int DocPriority(DocFile d)
    {
        var name = d.Name.ToLowerInvariant();
        return name switch
        {
            "claude.md"    => 0,
            "agents.md"    => 0,
            "changelog.md" => 1,
            "roadmap.md"   => 2,
            "spec.md"      => 3,
            "readme.md"    => 4,
            "kickoff.md"   => 5,
            _ when d.RelativePath.StartsWith("docs", StringComparison.OrdinalIgnoreCase) => 6,
            _ => 9,
        };
    }

    /// <summary>
    /// Parse Claude's JSON response into a <see cref="ProjectAuditReport"/>.
    /// Tolerates a markdown code fence around the JSON (Claude habit) and
    /// any leading / trailing prose by scanning for the first balanced
    /// <c>{ ... }</c> block.
    /// </summary>
    private static ProjectAuditReport ParseAuditPayload(string raw)
    {
        var json = JsonExtractor.ExtractJsonBlock(raw);
        if (json is null) return ProjectAuditReport.Empty;

        AuditPayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AuditPayload>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                });
        }
        catch
        {
            return ProjectAuditReport.Empty;
        }
        if (parsed is null) return ProjectAuditReport.Empty;

        var items = (parsed.RoadmapItems ?? new())
            .Select(i => new AuditRoadmapItem(
                Title: i.Title ?? "",
                Status: ParseStatus(i.Status),
                Category: i.Category ?? "",
                Source: i.Source ?? "",
                Evidence: i.Evidence ?? ""))
            .Where(i => !string.IsNullOrWhiteSpace(i.Title))
            .ToList();

        var incs = (parsed.Inconsistencies ?? new())
            .Select(i => new AuditInconsistency(
                Severity: ParseSeverity(i.Severity),
                Docs: i.Docs ?? new List<string>(),
                Issue: i.Issue ?? ""))
            .Where(i => !string.IsNullOrWhiteSpace(i.Issue))
            .OrderByDescending(i => i.Severity)
            .ToList();

        return new ProjectAuditReport(
            Design: parsed.Design ?? "",
            RoadmapItems: items,
            Inconsistencies: incs);
    }

    private static AuditItemStatus ParseStatus(string? s) => s?.ToLowerInvariant() switch
    {
        "complete" or "completed" or "done" or "shipped" => AuditItemStatus.Complete,
        "incomplete" or "todo" or "planned" or "pending" => AuditItemStatus.Incomplete,
        _ => AuditItemStatus.Unknown,
    };

    private static FindingSeverity ParseSeverity(string? s) => s?.ToLowerInvariant() switch
    {
        "critical" => FindingSeverity.Critical,
        "warning" => FindingSeverity.Warning,
        _ => FindingSeverity.Info,
    };

    private sealed class AuditPayload
    {
        [JsonPropertyName("design")] public string? Design { get; set; }
        [JsonPropertyName("roadmapItems")] public List<RoadmapItemPayload>? RoadmapItems { get; set; }
        [JsonPropertyName("inconsistencies")] public List<InconsistencyPayload>? Inconsistencies { get; set; }
    }

    private sealed class RoadmapItemPayload
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("source")] public string? Source { get; set; }
        [JsonPropertyName("evidence")] public string? Evidence { get; set; }
    }

    private sealed class InconsistencyPayload
    {
        [JsonPropertyName("severity")] public string? Severity { get; set; }
        [JsonPropertyName("docs")] public List<string>? Docs { get; set; }
        [JsonPropertyName("issue")] public string? Issue { get; set; }
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

    /// <summary>
    /// Git-aware staleness. Compares each doc's last-commit time to the
    /// project's most-recent commit; lag past <see cref="GitStaleThreshold"/>
    /// becomes a Warning. Docs that are tracked but have FS edits more recent
    /// than their last commit get an Info "Uncommitted changes" finding.
    /// Untracked docs (no commits at all) get an Info "Untracked doc".
    /// Entire check is silently skipped when git isn't available or the folder
    /// isn't a repo.
    /// </summary>
    private static readonly TimeSpan GitStaleThreshold = TimeSpan.FromDays(60);

    private static async Task CheckGitStalenessAsync(
        string projectRoot,
        IReadOnlyList<DocFile> docs,
        List<Finding> findings,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot)) return;

        var projectLastCommit = await GitInfo.GetLastCommitTimeAsync(projectRoot, null, ct);
        if (projectLastCommit is null) return; // not a repo / git unavailable

        foreach (var d in docs)
        {
            ct.ThrowIfCancellationRequested();
            var docLastCommit = await GitInfo.GetLastCommitTimeAsync(projectRoot, d.FullPath, ct);

            if (docLastCommit is null)
            {
                findings.Add(new Finding(FindingSeverity.Info, "Untracked doc",
                    "Has no commits yet — make sure it's intentional before relying on it.",
                    d.RelativePath));
                continue;
            }

            var lag = projectLastCommit.Value - docLastCommit.Value;
            if (lag >= GitStaleThreshold)
                findings.Add(new Finding(FindingSeverity.Warning, "Stale doc (Git)",
                    "Last committed " + (int)lag.TotalDays
                    + " days before the project's most recent commit — likely outdated.",
                    d.RelativePath));

            // FS mtime ahead of git suggests local edits not yet committed.
            if (d.Modified > docLastCommit.Value + TimeSpan.FromMinutes(1))
                findings.Add(new Finding(FindingSeverity.Info, "Uncommitted changes",
                    "Filesystem mtime is newer than the last commit — edits aren't in Git yet.",
                    d.RelativePath));
        }
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
