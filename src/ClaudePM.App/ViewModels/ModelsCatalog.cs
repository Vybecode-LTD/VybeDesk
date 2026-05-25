namespace ClaudePM.App.ViewModels;

/// <summary>A quick-pick entry for the model dropdown — shared between
/// Settings (global default) and Projects (per-project override).</summary>
public sealed record ModelOption(string Id, string DisplayName, string Tier);

/// <summary>
/// Shared, app-wide catalog of common Claude model IDs used by both the
/// Settings dropdown and the Projects per-project override picker. The
/// goal is one source of truth: when Anthropic ships a new tier, update
/// this file and both pickers refresh automatically.
///
/// Users can also paste any valid ID into a freeform TextBox alongside
/// the dropdown (preview / unreleased models the catalog hasn't been
/// updated for).
///
/// Pricing accurate as of 2026-01 per docs.claude.com.
/// </summary>
public static class ModelsCatalog
{
    public static readonly IReadOnlyList<ModelOption> All = new[]
    {
        // Latest generation
        new ModelOption("claude-opus-4-7",   "Claude Opus 4.7",
            "Most capable · agentic coding · $5 / $25 per MTok"),
        new ModelOption("claude-sonnet-4-6", "Claude Sonnet 4.6",
            "Balanced · ~1.7× cheaper than Opus 4.7 · $3 / $15 per MTok · recommended default"),
        new ModelOption("claude-haiku-4-5",  "Claude Haiku 4.5",
            "Fastest · ~5× cheaper than Opus 4.7 · $1 / $5 per MTok · great for quick edits / classification"),

        // Previous-gen (still callable)
        new ModelOption("claude-opus-4-6",   "Claude Opus 4.6 (previous)",
            "Previous-gen Opus · $5 / $25 per MTok"),
        new ModelOption("claude-sonnet-4-5", "Claude Sonnet 4.5 (previous)",
            "Previous-gen Sonnet · $3 / $15 per MTok"),
        new ModelOption("claude-opus-4-5",   "Claude Opus 4.5 (older)",
            "Older Opus · $5 / $25 per MTok"),
        new ModelOption("claude-opus-4-1",   "Claude Opus 4.1 (older, expensive)",
            "Older Opus · $15 / $75 per MTok · avoid unless you need it"),
    };

    /// <summary>Sentinel option for the per-project override picker, meaning
    /// "use the global Settings.Model" — its <c>Id</c> is the empty string,
    /// which the Projects VM maps to <c>Project.Model = null</c> on save.</summary>
    public static readonly ModelOption UseGlobalDefault =
        new("", "(Use global default)", "Inherits the model picked in Settings.");
}
