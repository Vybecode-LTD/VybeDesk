namespace ClaudePM.Core.Models;

/// <summary>A documentation file discovered by a project scan (Module 1).</summary>
public sealed record DocFile(
    string FullPath,
    string RelativePath,
    string Name,
    long SizeBytes,
    DateTimeOffset Modified);
