using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Vision;

/// <summary>
/// Module 8 — Vision Audit. Orchestrates the four-step drift detection
/// workflow per the <c>vision-drift-detection</c> skill and the
/// <c>build-prompts/vision-audit.md</c> spec.
/// <list type="number">
/// <item>Extract a vision from docs (reuses <see cref="IDocReconciliationService.ScanAsync"/>).</item>
/// <item>(UI gate — vision must be explicitly approved before audit.)</item>
/// <item>Audit structurally (cheap, size-independent) or in targeted mode
/// (reads a bounded set of the most relevant source files).</item>
/// <item>Build the report and the Claude Code deep-dive prompt.</item>
/// </list>
/// Deliberately size-independent — sending an entire codebase to one AI
/// call fails on large projects and costs too much for a shallow answer.
/// The structural pass uses only project shape; the targeted pass adds a
/// CAPPED set of files. The genuinely deep code review is handed off to a
/// coding agent via the deep-dive prompt.
/// </summary>
public sealed class VisionAuditService : IVisionAuditService
{
    // Caps. The targeted pass reads at most N files; each file is truncated
    // to a fixed character budget. The shape walker stops after the cap and
    // at a fixed depth. These are intentional safety rails — without them a
    // big monorepo could blow past the AI context window.
    private const int MaxRelevantFiles = 10;
    private const int MaxFileContentChars = 8_000;
    private const int MaxShapeItems = 600;
    private const int MaxShapeDepth = 6;
    private const int MaxDocBundleChars = 25_000;

    private static readonly HashSet<string> IgnoredFolderNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git", "node_modules", ".vs", ".idea", ".vscode",
            "dist", "build", "out", "target", ".next", "__pycache__",
            ".pytest_cache", ".gradle", ".mvn", "venv", ".venv", "env",
            ".claude", ".cursor",
        };

    private readonly IAiService _ai;
    private readonly IDocReconciliationService _docs;

    public VisionAuditService(IAiService ai, IDocReconciliationService docs)
    {
        _ai = ai;
        _docs = docs;
    }

    // ===== Step 1: extract =====================================================

    public async Task<IReadOnlyList<VisionStatement>> ExtractVisionAsync(
        string projectFolder, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectFolder))
            throw new ArgumentException("Project folder is required.", nameof(projectFolder));
        if (!Directory.Exists(projectFolder))
            throw new DirectoryNotFoundException(
                "Project folder not found: " + projectFolder);

        var docs = await _docs.ScanAsync(projectFolder, ct);
        var docText = await ReadDocBundleAsync(docs, ct);

        const string system =
            "You distil a project's vision from its documentation into concrete, " +
            "testable statements. Each statement says ONE specific thing the " +
            "project must do or be — e.g. 'users can create an account', 'data " +
            "persists between sessions', 'works offline'. AVOID vague aspirations " +
            "('it should be good', 'high quality'). AVOID implementation details. " +
            "Stay specific and testable. Aim for 5–12 statements covering the " +
            "key capabilities the project promises.\n\n" +
            "Return STRICT JSON only — a single object with one field 'statements' " +
            "whose value is an array of statement strings. No prose, no markdown " +
            "fences, no commentary.";

        var user = string.IsNullOrWhiteSpace(docText)
            ? "(no documentation found in the project — produce a best-effort skeleton " +
              "of empty placeholder statements the user can fill in.)"
            : "Project documentation follows.\n\n" + docText;

        var raw = await _ai.CompleteAsync(system, user, ct);
        var texts = ParseStatements(raw);
        return texts.Select(t => new VisionStatement { Text = t }).ToList();
    }

    // ===== Step 3: audit =======================================================

    public async Task<AuditReport> AuditAsync(
        VisionRecord approvedVision,
        string projectFolder,
        AuditMode mode,
        CancellationToken ct = default)
    {
        if (approvedVision is null)
            throw new ArgumentNullException(nameof(approvedVision));
        if (!approvedVision.IsApproved)
            throw new InvalidOperationException(
                "The vision has not been approved. The audit cannot run against an " +
                "unapproved vision — the approval gate guarantees the measuring " +
                "stick is correct before anything is measured against it.");
        if (approvedVision.Statements.Count == 0)
            throw new InvalidOperationException(
                "The vision has no statements to audit. Add at least one statement " +
                "before approving.");
        if (string.IsNullOrWhiteSpace(projectFolder))
            throw new ArgumentException("Project folder is required.", nameof(projectFolder));
        if (!Directory.Exists(projectFolder))
            throw new DirectoryNotFoundException(
                "Project folder not found: " + projectFolder);

        var shape = GatherProjectShape(projectFolder);
        var docs = await _docs.ScanAsync(projectFolder, ct);
        var docText = await ReadDocBundleAsync(docs, ct);

        string? fileContentsBundle = null;
        if (mode == AuditMode.Targeted)
        {
            var relevantFiles = await SelectRelevantFilesAsync(approvedVision, shape, ct);
            fileContentsBundle = ReadBoundedFileBundle(projectFolder, relevantFiles);
        }

        var verdicts = await GetVerdictsAsync(
            approvedVision, shape, docText, fileContentsBundle, ct);
        return new AuditReport(mode, verdicts, DateTimeOffset.Now);
    }

    // ===== Step 5: report + deep-dive prompt ===================================

    public string BuildReportMarkdown(AuditReport report, string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Vision audit — " + projectName);
        sb.AppendLine();
        sb.AppendLine("Mode: **" + report.Mode + "**  ·  generated " +
                      report.GeneratedAt.ToString("yyyy-MM-dd HH:mm"));
        sb.AppendLine();

        // Lead with Off-track items per the skill's report guidance.
        foreach (var rank in new[] { AlignmentRank.OffTrack, AlignmentRank.AtRisk, AlignmentRank.OnTrack })
        {
            var slice = report.Verdicts.Where(v => v.Rank == rank).ToList();
            if (slice.Count == 0) continue;

            sb.AppendLine("## " + Heading(rank) + " (" + slice.Count + ")");
            sb.AppendLine();
            foreach (var v in slice)
            {
                sb.AppendLine("### " + v.StatementText);
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(v.Evidence))
                {
                    sb.AppendLine("**Evidence:** " + v.Evidence);
                    sb.AppendLine();
                }
                if (!string.IsNullOrWhiteSpace(v.Recommendation))
                {
                    sb.AppendLine("**Recommendation:** " + v.Recommendation);
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("> Structural audits cannot catch behavioural drift inside " +
                      "correctly-named files. For At-risk and Off-track items, " +
                      "hand the deep-dive prompt to Claude Code for line-level " +
                      "verification in the actual code.");

        return sb.ToString();
    }

    public string BuildDeepDivePrompt(AuditReport report, string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Vision-drift deep dive — " + projectName);
        sb.AppendLine();
        sb.AppendLine("A structural audit found gaps between the approved vision and " +
                      "the project's shape. Your job is to confirm or correct those " +
                      "findings by reading the actual code — the structural audit " +
                      "could only see file/folder names and dependencies, so " +
                      "behavioural drift inside correctly-named files would be invisible to it.");
        sb.AppendLine();
        sb.AppendLine("## Approved vision");
        sb.AppendLine();
        foreach (var v in report.Verdicts)
            sb.AppendLine("- " + v.StatementText);
        sb.AppendLine();

        var flagged = report.Verdicts
            .Where(v => v.Rank is AlignmentRank.OffTrack or AlignmentRank.AtRisk)
            .ToList();

        if (flagged.Count == 0)
        {
            sb.AppendLine("## Investigate");
            sb.AppendLine();
            sb.AppendLine("The structural audit found no At-risk or Off-track items. " +
                          "Spot-check 2–3 vision statements you think are most likely " +
                          "to harbour silent drift, read the relevant code, and " +
                          "report back any discrepancies.");
        }
        else
        {
            sb.AppendLine("## Investigate these flagged items");
            sb.AppendLine();
            foreach (var v in flagged)
            {
                sb.AppendLine("### " + v.StatementText + " — " + v.Rank);
                if (!string.IsNullOrWhiteSpace(v.Evidence))
                    sb.AppendLine("Structural evidence: " + v.Evidence);
                if (!string.IsNullOrWhiteSpace(v.Recommendation))
                    sb.AppendLine("Audit recommendation: " + v.Recommendation);
                sb.AppendLine();
            }

            sb.AppendLine("For each item: open the relevant source files, read the " +
                          "implementation, and either confirm the structural finding " +
                          "or correct it with line-level evidence. Distinguish " +
                          "missing capability from incomplete capability from " +
                          "implemented-but-untested capability — they need different " +
                          "fixes. Do NOT modify code in this pass; report only.");
        }

        return sb.ToString();
    }

    // ===== AI orchestration helpers ============================================

    private async Task<IReadOnlyList<string>> SelectRelevantFilesAsync(
        VisionRecord vision, ProjectShape shape, CancellationToken ct)
    {
        var system =
            "Pick up to " + MaxRelevantFiles + " source files most likely to be " +
            "relevant to the project's vision, based ONLY on file/folder names " +
            "and the dependency manifest. The goal: pick files whose contents " +
            "would tell us whether each vision statement is honoured by the code. " +
            "Prefer files clearly named for vision concepts; skip tests, config " +
            "noise, and generated code unless they're the only signal.\n\n" +
            "Return STRICT JSON only: { files: [\"relative/path\", ...] }. Use " +
            "RELATIVE paths from the project root, exactly as listed in the shape " +
            "data. Max " + MaxRelevantFiles + " entries.";

        var user = BuildShapePromptUser(vision, shape, includeDocs: false, docText: null);
        var raw = await _ai.CompleteAsync(system, user, ct);
        var picks = ParseFilePicks(raw);
        return picks.Take(MaxRelevantFiles).ToList();
    }

    private async Task<IReadOnlyList<StatementVerdict>> GetVerdictsAsync(
        VisionRecord vision, ProjectShape shape, string docText,
        string? fileContents, CancellationToken ct)
    {
        var system =
            "You audit each vision statement against the project's structure and " +
            "documentation" + (fileContents is null ? "" : " AND selected source-file contents") +
            ". For EACH statement, decide one of three ranks:\n" +
            "  - OnTrack: clear evidence the capability exists or is underway.\n" +
            "  - AtRisk:  partial / ambiguous / stubbed evidence.\n" +
            "  - OffTrack: no evidence at all.\n" +
            "Give SHORT factual evidence and a CONCRETE recommendation per statement. " +
            "Be honest about what structural evidence can and can't show — call out " +
            "behavioural assumptions you cannot verify from shape alone.\n\n" +
            "Return STRICT JSON only — a single object with one field 'verdicts' " +
            "whose value is an array, one entry per statement, in the same order " +
            "given. Each entry: { statementId: string, rank: \"OnTrack\"|\"AtRisk\"|\"OffTrack\", " +
            "evidence: string, recommendation: string }. No prose, no fences.";

        var user = BuildShapePromptUser(vision, shape, includeDocs: true, docText: docText);
        if (fileContents is not null)
        {
            user += "\n\n## Selected source files\n\n" + fileContents;
        }

        var raw = await _ai.CompleteAsync(system, user, ct);
        return ParseVerdicts(raw, vision);
    }

    // ===== Shape gathering =====================================================

    /// <summary>Builds the common user-prompt portion (vision + shape + docs).</summary>
    private static string BuildShapePromptUser(
        VisionRecord vision, ProjectShape shape, bool includeDocs, string? docText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Approved vision (statementId — text)");
        sb.AppendLine();
        foreach (var s in vision.Statements)
            sb.AppendLine("- " + s.Id + " — " + s.Text);

        sb.AppendLine();
        sb.AppendLine("## Project shape");
        sb.AppendLine();
        sb.AppendLine("### Folders");
        foreach (var f in shape.Folders.Take(150)) sb.AppendLine("- " + f);
        sb.AppendLine();
        sb.AppendLine("### Files");
        foreach (var f in shape.Files.Take(300)) sb.AppendLine("- " + f);

        if (shape.Manifests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Dependency manifests");
            foreach (var m in shape.Manifests)
            {
                sb.AppendLine();
                sb.AppendLine("#### " + m.Path);
                sb.AppendLine("```");
                sb.AppendLine(m.Content);
                sb.AppendLine("```");
            }
        }

        if (includeDocs && !string.IsNullOrWhiteSpace(docText))
        {
            sb.AppendLine();
            sb.AppendLine("## Documentation");
            sb.AppendLine();
            sb.AppendLine(docText);
        }

        return sb.ToString();
    }

    private static ProjectShape GatherProjectShape(string root)
    {
        var folders = new List<string>();
        var files = new List<string>();
        var manifests = new List<ManifestEntry>();

        void Walk(string dir, int depth)
        {
            if (depth > MaxShapeDepth) return;
            if (folders.Count + files.Count >= MaxShapeItems) return;

            var rel = Path.GetRelativePath(root, dir);
            if (rel != ".") folders.Add(rel.Replace('\\', '/'));

            try
            {
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (IgnoredFolderNames.Contains(name)) continue;
                    // Hidden dotted folders are usually noise — except .github
                    // which carries CI config that's actually informative.
                    if (name.StartsWith('.') && !name.Equals(".github", StringComparison.OrdinalIgnoreCase))
                        continue;
                    Walk(sub, depth + 1);
                }
                foreach (var file in Directory.GetFiles(dir))
                {
                    if (folders.Count + files.Count >= MaxShapeItems) break;
                    var name = Path.GetFileName(file);
                    var relF = Path.GetRelativePath(root, file).Replace('\\', '/');
                    files.Add(relF);

                    if (IsManifest(name))
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            if (content.Length > 6_000)
                                content = content.Substring(0, 6_000) + "\n…[truncated]";
                            manifests.Add(new ManifestEntry(relF, content));
                        }
                        catch
                        {
                            // skip unreadable manifest
                        }
                    }
                }
            }
            catch
            {
                // skip unreadable directory
            }
        }

        Walk(root, 0);
        return new ProjectShape(folders, files, manifests);
    }

    private static bool IsManifest(string filename)
    {
        var lower = filename.ToLowerInvariant();
        if (lower is "package.json" or "pyproject.toml" or "requirements.txt"
                  or "cargo.toml" or "go.mod" or "gemfile" or "build.gradle"
                  or "pom.xml" or "composer.json" or "mix.exs")
            return true;
        return lower.EndsWith(".csproj") || lower.EndsWith(".sln") || lower.EndsWith(".fsproj");
    }

    // ===== File reading helpers ================================================

    private static async Task<string> ReadDocBundleAsync(
        IReadOnlyList<DocFile> docs, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var budgetLeft = MaxDocBundleChars;

        // Prioritise the docs that usually carry the project's intent.
        var prioritised = docs
            .OrderBy(d => OrderKey(d.Name))
            .ToList();

        foreach (var doc in prioritised)
        {
            if (budgetLeft <= 0) break;
            ct.ThrowIfCancellationRequested();

            string content;
            try { content = await File.ReadAllTextAsync(doc.FullPath, ct); }
            catch { continue; }

            if (content.Length > budgetLeft)
                content = content.Substring(0, budgetLeft) + "\n…[truncated]";
            budgetLeft -= content.Length;

            sb.AppendLine("### " + doc.RelativePath);
            sb.AppendLine();
            sb.AppendLine(content);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static int OrderKey(string name) => name.ToLowerInvariant() switch
    {
        "claude.md" => 0,
        "readme.md" => 1,
        "spec.md" => 2,
        "roadmap.md" => 3,
        "changelog.md" => 4,
        _ => 99,
    };

    private static string ReadBoundedFileBundle(string root, IReadOnlyList<string> relativePaths)
    {
        var sb = new StringBuilder();
        foreach (var rel in relativePaths)
        {
            var full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) continue;
            string content;
            try { content = File.ReadAllText(full); }
            catch { continue; }
            if (content.Length > MaxFileContentChars)
                content = content.Substring(0, MaxFileContentChars) + "\n…[truncated]";
            sb.AppendLine("### " + rel);
            sb.AppendLine("```");
            sb.AppendLine(content);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ===== JSON parsing ========================================================

    private static IReadOnlyList<string> ParseStatements(string raw)
    {
        using var doc = JsonDocument.Parse(ExtractJson(raw));
        if (!doc.RootElement.TryGetProperty("statements", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Vision extraction: AI response did not contain a 'statements' array.");

        var list = new List<string>();
        foreach (var s in arr.EnumerateArray())
        {
            if (s.ValueKind == JsonValueKind.String)
            {
                var v = s.GetString();
                if (!string.IsNullOrWhiteSpace(v)) list.Add(v.Trim());
            }
        }
        return list;
    }

    private static IReadOnlyList<string> ParseFilePicks(string raw)
    {
        using var doc = JsonDocument.Parse(ExtractJson(raw));
        if (!doc.RootElement.TryGetProperty("files", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "File selection: AI response did not contain a 'files' array.");

        var list = new List<string>();
        foreach (var s in arr.EnumerateArray())
        {
            if (s.ValueKind == JsonValueKind.String)
            {
                var v = s.GetString();
                if (!string.IsNullOrWhiteSpace(v)) list.Add(v.Trim().Replace('\\', '/'));
            }
        }
        return list;
    }

    private static IReadOnlyList<StatementVerdict> ParseVerdicts(string raw, VisionRecord vision)
    {
        using var doc = JsonDocument.Parse(ExtractJson(raw));
        if (!doc.RootElement.TryGetProperty("verdicts", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException(
                "Audit: AI response did not contain a 'verdicts' array.");

        // Index statements by Id so we can match verdicts back even if the AI
        // returns them out of order.
        var byId = vision.Statements.ToDictionary(s => s.Id);

        var verdicts = new List<StatementVerdict>();
        foreach (var v in arr.EnumerateArray())
        {
            var idStr = v.TryGetProperty("statementId", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString() ?? ""
                : "";
            var rankStr = v.TryGetProperty("rank", out var rEl) && rEl.ValueKind == JsonValueKind.String
                ? rEl.GetString() ?? "OffTrack"
                : "OffTrack";
            var evidence = v.TryGetProperty("evidence", out var eEl) && eEl.ValueKind == JsonValueKind.String
                ? eEl.GetString() ?? ""
                : "";
            var recommendation = v.TryGetProperty("recommendation", out var rcEl) && rcEl.ValueKind == JsonValueKind.String
                ? rcEl.GetString() ?? ""
                : "";

            if (!Guid.TryParse(idStr, out var stmtId)) continue;
            if (!byId.TryGetValue(stmtId, out var stmt)) continue;

            var rank = rankStr.Trim().ToLowerInvariant() switch
            {
                "ontrack" => AlignmentRank.OnTrack,
                "atrisk" => AlignmentRank.AtRisk,
                _ => AlignmentRank.OffTrack,
            };

            verdicts.Add(new StatementVerdict(
                stmtId, stmt.Text, rank, evidence.Trim(), recommendation.Trim()));
        }

        // Belt-and-suspenders: ensure every statement got a verdict; fabricate
        // an OffTrack "missing verdict" if the AI dropped one. The spec
        // requires a verdict for every statement.
        foreach (var s in vision.Statements)
        {
            if (verdicts.Any(v => v.StatementId == s.Id)) continue;
            verdicts.Add(new StatementVerdict(
                s.Id, s.Text, AlignmentRank.OffTrack,
                "The audit produced no verdict for this statement.",
                "Re-run the audit, or refine this statement to make it more concrete."));
        }

        return verdicts;
    }

    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("AI returned an empty response.");

        var trimmed = raw.Trim();

        var fence = Regex.Match(trimmed,
            @"```(?:json)?\s*(?<body>[\s\S]*?)\s*```",
            RegexOptions.IgnoreCase);
        if (fence.Success) return fence.Groups["body"].Value.Trim();

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);

        throw new InvalidOperationException(
            "The AI's response wasn't JSON — it replied in prose. This usually " +
            "means the model misinterpreted the prompt; click the action again, " +
            "or refine your inputs.");
    }

    private static string Heading(AlignmentRank rank) => rank switch
    {
        AlignmentRank.OffTrack => "Off track",
        AlignmentRank.AtRisk => "At risk",
        AlignmentRank.OnTrack => "On track",
        _ => rank.ToString(),
    };

    private sealed record ProjectShape(
        IReadOnlyList<string> Folders,
        IReadOnlyList<string> Files,
        IReadOnlyList<ManifestEntry> Manifests);

    private sealed record ManifestEntry(string Path, string Content);
}
