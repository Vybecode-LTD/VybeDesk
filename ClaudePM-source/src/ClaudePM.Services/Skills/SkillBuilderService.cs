using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Skills;

/// <summary>
/// Default <see cref="ISkillBuilderService"/>. Orchestrates the
/// skill-design workflow (skill-design-workflow) and applies the authoring
/// craft (skill-file-authoring) via two AI prompts:
/// <list type="number">
/// <item>Optional clarifying-question generation when the user toggles
/// research ON — interactive Q&amp;A, NOT internet research, because the app
/// has no web access.</item>
/// <item>The actual draft — name from the user, description and body from
/// the AI applying the routing-style + imperative-body rules.</item>
/// </list>
/// Validation and serialization are deliberately delegated to
/// <see cref="ISkillLibraryService"/> so the Builder and the Manager share
/// one source of truth.
/// </summary>
public sealed class SkillBuilderService : ISkillBuilderService
{
    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAiService _ai;
    private readonly ISkillLibraryService _library;

    public SkillBuilderService(IAiService ai, ISkillLibraryService library)
    {
        _ai = ai;
        _library = library;
    }

    public async Task<IReadOnlyList<string>> GenerateClarifyingQuestionsAsync(
        SkillBuilderInputs inputs, CancellationToken ct = default)
    {
        const string system =
            "You help design Claude skills (loadable knowledge modules for an AI agent). " +
            "Given the user's rough idea, ask 3–5 focused clarifying questions whose answers " +
            "will sharpen the routing description and the body. Cover at minimum: the intended " +
            "TRIGGER scenarios (what user phrasing or task should fire this skill), the SCOPE " +
            "boundary (what's in and what's out), at least one concrete USE CASE, and what the " +
            "skill should NOT do. Be specific and concrete; no platitudes.\n\n" +
            "Return STRICT JSON only — a single object with one field 'questions' whose value is " +
            "an array of question strings. No prose, no markdown fences, no commentary.";

        var user =
            "Skill name: " + inputs.Name + "\n" +
            "Rough description: " + inputs.RoughDescription + "\n" +
            "Notes:\n" + inputs.Notes;

        var raw = await _ai.CompleteAsync(system, user, ct);
        return ParseQuestions(raw);
    }

    public async Task<SkillFile> DraftAsync(
        SkillBuilderInputs inputs,
        IReadOnlyList<QuestionAnswer>? answers,
        CancellationToken ct = default)
    {
        // System prompt encodes skill-file-authoring craft: description is a
        // ROUTER (one-line summary + explicit "use when" + literal triggers),
        // under 1024 chars; body is IMPERATIVE, leads with core principle,
        // ends with anti-patterns.
        const string system =
            "You author Claude skill files. Given the user's inputs (and any clarifying answers " +
            "they provided), produce ONE skill that covers a single coherent capability.\n\n" +
            "The DESCRIPTION is a router, not a summary. Write it as three parts in this order:\n" +
            "  1. One sentence saying what the skill does.\n" +
            "  2. Explicit 'use when' scenarios phrased the way they actually arise.\n" +
            "  3. A list of literal trigger words and casual phrasings.\n" +
            "Keep it under 1024 characters. Cut hedging, not triggers.\n\n" +
            "The BODY is the on-demand content. Imperative voice. Lead with the core principle, " +
            "then the procedure, then edge cases. END with a clearly-labeled 'Anti-patterns' " +
            "section. Use Markdown. Keep it focused on one capability — if it sprawls, narrow.\n\n" +
            "Do NOT include YAML frontmatter, do NOT include the name in the output — both come " +
            "from the user. Return STRICT JSON only — a single object with two fields, " +
            "'description' (string) and 'body' (string). No prose, no markdown fences.";

        var sb = new StringBuilder();
        sb.AppendLine("Skill name: " + inputs.Name);
        sb.AppendLine("Rough description: " + inputs.RoughDescription);
        sb.AppendLine("Notes:");
        sb.AppendLine(string.IsNullOrWhiteSpace(inputs.Notes) ? "(none)" : inputs.Notes);
        if (answers is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("Clarifying answers:");
            foreach (var qa in answers)
            {
                sb.AppendLine("Q: " + qa.Question);
                sb.AppendLine("A: " + (string.IsNullOrWhiteSpace(qa.Answer) ? "(no answer)" : qa.Answer));
            }
        }

        var raw = await _ai.CompleteAsync(system, sb.ToString(), ct);
        var (description, body) = ParseDraft(raw);

        // The draft is a plain SkillFile in memory — we don't write it to
        // disk here. EmitAsync handles persistence later, so the user can
        // review and re-draft as many times as they like before committing.
        return new SkillFile
        {
            Name = inputs.Name.Trim(),
            Description = description,
            Body = body,
            HasFrontMatter = true,
            // No FullPath yet — that's set when EmitAsync writes the file.
        };
    }

    /// <summary>
    /// Delegates to <see cref="ISkillLibraryService.Validate"/> so the
    /// Builder's findings are byte-for-byte identical to the Manager's.
    /// </summary>
    public IReadOnlyList<Finding> Validate(SkillFile skill)
        => _library.Validate(skill);

    public async Task<SkillEmitResult> EmitAsync(
        SkillFile skill, string targetFolder, CancellationToken ct = default)
    {
        if (skill is null)
            throw new ArgumentNullException(nameof(skill));
        if (string.IsNullOrWhiteSpace(skill.Name))
            throw new InvalidOperationException(
                "Skill has no name — cannot determine output file names.");
        if (string.IsNullOrWhiteSpace(targetFolder))
            throw new ArgumentException("Target folder is required.", nameof(targetFolder));
        if (!Directory.Exists(targetFolder))
            Directory.CreateDirectory(targetFolder);

        // Render once via the library's serializer so the two output forms
        // are guaranteed to be byte-identical.
        var text = _library.Serialize(skill);

        var flatPath = Path.Combine(targetFolder, skill.Name + ".skill");
        var folderPath = Path.Combine(targetFolder, skill.Name);
        var folderSkillPath = Path.Combine(folderPath, "SKILL.md");

        if (File.Exists(flatPath))
            throw new InvalidOperationException(
                "A file named '" + skill.Name + ".skill' already exists in the target.");
        if (Directory.Exists(folderPath))
            throw new InvalidOperationException(
                "A folder named '" + skill.Name + "' already exists in the target.");

        // Write the flat form, then the folder form. If the second write
        // fails we leave the first behind on disk — the alternative
        // (deleting on partial failure) costs more than it earns for a
        // user-driven action.
        await File.WriteAllTextAsync(flatPath, text, ct);
        Directory.CreateDirectory(folderPath);
        await File.WriteAllTextAsync(folderSkillPath, text, ct);

        return new SkillEmitResult(flatPath, folderPath);
    }

    // ---- JSON parsing -------------------------------------------------------
    //
    // Claude is good at JSON but occasionally wraps it in ```json fences or
    // adds a stray sentence. The two helpers below recover the JSON envelope
    // and surface a clear error if parsing fails.

    private static IReadOnlyList<string> ParseQuestions(string raw)
    {
        var json = ExtractJson(raw);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("questions", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Clarifying-question generation: AI response did not contain a 'questions' array.");
        }
        var list = new List<string>();
        foreach (var q in arr.EnumerateArray())
        {
            if (q.ValueKind == JsonValueKind.String)
            {
                var s = q.GetString();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s.Trim());
            }
        }
        if (list.Count == 0)
            throw new InvalidOperationException(
                "Clarifying-question generation: AI returned an empty questions array.");
        return list;
    }

    private static (string Description, string Body) ParseDraft(string raw)
    {
        var json = ExtractJson(raw);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            // The most common cause is that the AI replied conversationally
            // ("I'll help you build a skill that...") instead of the strict
            // JSON the system prompt requires. Surface a user-actionable
            // message instead of the raw "'I' is an invalid start of a
            // value" text the JSON reader produces.
            throw new InvalidOperationException(
                "Claude's draft response wasn't valid JSON — it likely " +
                "replied in prose instead of the structured draft. This " +
                "usually means the rough description was too vague. Make " +
                "your description more specific (what problem does the " +
                "skill solve? what should trigger it?), then click Re-draft. " +
                "(Raw parse error: " + ex.Message + ")");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("description", out var d) || d.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException(
                    "Claude's draft was missing a 'description' field. Click Re-draft.");
            if (!root.TryGetProperty("body", out var b) || b.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException(
                    "Claude's draft was missing a 'body' field. Click Re-draft.");
            return ((d.GetString() ?? "").Trim(), (b.GetString() ?? "").Trim());
        }
    }

    /// <summary>
    /// Strips ```json``` or ``` fences if Claude wrapped the JSON in them,
    /// and trims leading prose before the first '{'. Returns the JSON
    /// substring or the raw text if no envelope was detected.
    /// </summary>
    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("AI returned an empty response.");

        var trimmed = raw.Trim();

        // ```json ... ``` or ``` ... ```
        var fence = Regex.Match(trimmed,
            @"```(?:json)?\s*(?<body>[\s\S]*?)\s*```",
            RegexOptions.IgnoreCase);
        if (fence.Success) return fence.Groups["body"].Value.Trim();

        // Bare JSON, possibly with a leading sentence.
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);

        // No JSON envelope at all — the AI replied in pure prose. Throw a
        // clear message rather than handing the raw text to the JSON parser,
        // which would produce a confusing "'I' is an invalid start of a
        // value"-style error.
        throw new InvalidOperationException(
            "Claude's response had no JSON envelope at all — it replied in " +
            "prose. This usually happens when the inputs are too vague. " +
            "Tighten the rough description (what the skill does, when it " +
            "should fire), then click Re-draft.");
    }
}
