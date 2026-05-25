using System.Text;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Session;

/// <summary>
/// Default <see cref="ISessionBuilderService"/>. Assembles a Claude Code handoff
/// package — it does not generate the project's code; Claude Code does that.
/// </summary>
public sealed class SessionBuilderService : ISessionBuilderService
{
    private readonly IAiService _ai;

    public SessionBuilderService(IAiService ai) => _ai = ai;

    public async Task<string> ReviewAsync(SessionPlan plan, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PROJECT: " + plan.ProjectName);
        sb.AppendLine("DESCRIPTION: " + plan.Description);
        if (!string.IsNullOrWhiteSpace(plan.Stack))
            sb.AppendLine("STACK: " + plan.Stack);
        sb.AppendLine();
        sb.AppendLine("STAGED FILES:");
        if (plan.FilePaths.Count == 0)
            sb.AppendLine("(none)");
        else
            foreach (var f in plan.FilePaths)
                sb.AppendLine("- " + Path.GetFileName(f));
        sb.AppendLine();
        sb.AppendLine("TRANSCRIPTS:");
        foreach (var t in plan.Transcripts)
        {
            sb.AppendLine("### " + t.DisplayTitle);
            var body = t.Body.Length > 4000 ? t.Body[..4000] + "\n[...truncated...]" : t.Body;
            sb.AppendLine(body);
            sb.AppendLine();
        }

        const string system =
            "You are preparing a handoff from a claude.ai chat project to Claude Code. " +
            "Given the project description, conversation transcripts, and staged file list, " +
            "identify what is likely MISSING for a clean handoff: files referenced in the " +
            "conversation but not staged, unstated decisions, config or schemas, environment " +
            "details, and unresolved questions. Output a concise markdown checklist only.";

        return await _ai.CompleteAsync(system, sb.ToString(), ct);
    }

    public async Task<string> GenerateAsync(SessionPlan plan, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plan.ProjectName))
            throw new InvalidOperationException("Project name is required.");
        if (string.IsNullOrWhiteSpace(plan.OutputLocation))
            throw new InvalidOperationException("Output location is required.");
        if (!Directory.Exists(plan.OutputLocation))
            throw new DirectoryNotFoundException("Output location not found: " + plan.OutputLocation);

        var root = Path.Combine(plan.OutputLocation, SafeName(plan.ProjectName));
        Directory.CreateDirectory(root);
        var transcriptDir = Path.Combine(root, "docs", "transcripts");
        Directory.CreateDirectory(transcriptDir);

        // Template-driven scaffolding (M4 #15). Each template picks
        // stack-appropriate content for the four canonical files.
        var (claudeMd, readme, gitignore, kickoff) = SessionTemplates.For(
            plan.Template, plan.ProjectName, plan.Description, plan.Stack);

        await File.WriteAllTextAsync(Path.Combine(root, "CLAUDE.md"), claudeMd, ct);
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), readme, ct);
        await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), gitignore, ct);
        await File.WriteAllTextAsync(Path.Combine(root, "KICKOFF.md"), kickoff, ct);

        for (var i = 0; i < plan.Transcripts.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = plan.Transcripts[i];
            var fname = (i + 1).ToString("D2") + "-" + SafeName(t.DisplayTitle) + ".md";
            await File.WriteAllTextAsync(
                Path.Combine(transcriptDir, fname),
                "# " + t.DisplayTitle + "\n\n" + t.Body, ct);
        }

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CLAUDE.md", "README.md", ".gitignore", "KICKOFF.md" };
        foreach (var src in plan.FilePaths)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(src)) continue;
            var fname = Path.GetFileName(src);
            if (reserved.Contains(fname)) fname = "staged-" + fname;
            var dest = Path.Combine(root, fname);
            if (File.Exists(dest)) continue;
            File.Copy(src, dest);
        }

        return root;
    }

    private static string SafeName(string name)
    {
        var cleaned = new string(name
            .Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')
            .ToArray())
            .Trim('-');
        return cleaned.Length == 0 ? "project" : cleaned;
    }
}
