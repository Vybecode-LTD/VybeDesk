using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>
/// Turns a claude.ai project into a Claude Code handoff package (Module 3).
/// </summary>
public interface ISessionBuilderService
{
    /// <summary>AI review — flags what is likely missing for a clean handoff.</summary>
    Task<string> ReviewAsync(SessionPlan plan, CancellationToken ct = default);

    /// <summary>
    /// Writes the handoff package (CLAUDE.md, README, .gitignore, KICKOFF.md,
    /// transcripts, staged files) and returns the package folder path.
    /// </summary>
    Task<string> GenerateAsync(SessionPlan plan, CancellationToken ct = default);
}
