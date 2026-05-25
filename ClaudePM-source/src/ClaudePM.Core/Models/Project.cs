namespace ClaudePM.Core.Models;

/// <summary>A registered project — the unit Modules 1, 3, and 4 operate within.</summary>
public sealed class Project
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Per-project Anthropic model override (e.g. "claude-sonnet-4-6"). Null
    /// means "use the global setting from Settings". Lets a user pick e.g.
    /// Opus for a research-heavy project and Haiku for a quick-fix sandbox
    /// without flipping the global default.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Per-project default output path — used as the starting folder for
    /// generated handoff packages, exports, etc., from this project's
    /// context. Null means "use the global Settings.OutputPath".
    /// </summary>
    public string? DefaultOutputPath { get; set; }

    /// <summary>
    /// Optional path to a logo/icon image shown on the Home dashboard card.
    /// Null/blank means "no logo — render the module glyph as fallback". Set
    /// either via auto-detect on import (<see cref="IProjectImportService"/>)
    /// or by hand via the Projects editor. Absolute path; PNG / JPG / SVG / ICO
    /// supported.
    /// </summary>
    public string? LogoPath { get; set; }
}

public enum ProjectStatus { Active, OnHold, Archived }
