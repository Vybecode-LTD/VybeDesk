namespace VybeDesk.App.ViewModels;

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
        // Latest generation — IDs per https://docs.anthropic.com/en/docs/about-claude/models
        new ModelOption("claude-sonnet-4-20250514", "Claude Sonnet 4 (latest)",
            "Balanced · great default · $3 / $15 per MTok"),
        new ModelOption("claude-opus-4-20250514",   "Claude Opus 4 (latest)",
            "Most capable · agentic coding · $15 / $75 per MTok"),
        new ModelOption("claude-haiku-3-5-20241022", "Claude Haiku 3.5",
            "Fastest · cheapest · $0.80 / $4 per MTok"),

        // Previous-gen (still callable)
        new ModelOption("claude-sonnet-4-5-20241022", "Claude Sonnet 3.5 v2",
            "Previous-gen Sonnet · $3 / $15 per MTok"),
        new ModelOption("claude-3-5-haiku-20241022", "Claude 3.5 Haiku",
            "Previous-gen Haiku · $1 / $5 per MTok"),
    };

    /// <summary>Sentinel option for the per-project override picker, meaning
    /// "use the global Settings.Model" — its <c>Id</c> is the empty string,
    /// which the Projects VM maps to <c>Project.Model = null</c> on save.</summary>
    public static readonly ModelOption UseGlobalDefault =
        new("", "(Use global default)", "Inherits the model picked in Settings.");
}
