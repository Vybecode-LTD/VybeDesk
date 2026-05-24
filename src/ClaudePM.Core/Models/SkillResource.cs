namespace ClaudePM.Core.Models;

/// <summary>
/// A file living inside a folder-format skill (alongside <c>SKILL.md</c>).
/// Skills commonly ship a <c>references/</c> folder with markdown docs the
/// skill points at, plus optional <c>scripts/</c>, <c>data/</c>,
/// <c>templates/</c>, etc. Flat <c>*.skill</c> skills have no resources.
/// </summary>
/// <param name="RelativePath">
/// Path relative to the skill folder root, with forward slashes. Never
/// <c>SKILL.md</c> itself — that's the skill, not a resource.
/// </param>
/// <param name="FullPath">Absolute path on disk.</param>
/// <param name="SizeBytes">File size in bytes.</param>
public sealed record SkillResource(string RelativePath, string FullPath, long SizeBytes);
