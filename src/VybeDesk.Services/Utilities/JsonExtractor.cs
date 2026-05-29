using System.Text.RegularExpressions;

namespace VybeDesk.Services.Utilities;

/// <summary>
/// Shared utility for extracting a JSON object from AI responses that may
/// contain markdown fences, leading prose, or trailing commentary. Used by
/// <see cref="Vision.VisionAuditService"/>,
/// <see cref="Skills.SkillBuilderService"/>, and
/// <see cref="Docs.DocReconciliationService"/>.
/// </summary>
public static class JsonExtractor
{
    /// <summary>
    /// Extracts the first balanced <c>{ ... }</c> JSON block from <paramref name="text"/>,
    /// stripping <c>```json</c> fences and leading/trailing prose first.
    /// Returns <c>null</c> when <paramref name="text"/> is null/empty or contains
    /// no JSON object. Throws <see cref="InvalidOperationException"/> only when
    /// callers explicitly need a non-null result (they should check for null or
    /// call <see cref="ExtractJsonBlockOrThrow"/> instead).
    /// </summary>
    public static string? ExtractJsonBlock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();

        // ```json ... ``` or ``` ... ```
        var fence = Regex.Match(trimmed,
            @"```(?:json)?\s*(?<body>[\s\S]*?)\s*```",
            RegexOptions.IgnoreCase);
        if (fence.Success)
        {
            var body = fence.Groups["body"].Value.Trim();
            if (body.Length > 0) return body;
        }

        // Balanced-brace scan: find the first '{' and its matching '}'.
        int start = trimmed.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        for (int i = start; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return trimmed.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    /// <summary>
    /// Same as <see cref="ExtractJsonBlock"/> but throws when no JSON is found.
    /// Convenience for callers that treat a missing JSON envelope as a hard error.
    /// </summary>
    public static string ExtractJsonBlockOrThrow(string? text)
    {
        return ExtractJsonBlock(text)
            ?? throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(text)
                    ? "AI returned an empty response."
                    : "The AI's response wasn't JSON — it replied in prose. This usually " +
                      "means the model misinterpreted the prompt; retry, or refine your inputs.");
    }
}
