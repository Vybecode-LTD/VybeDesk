namespace ClaudePM.Core.Models;

public enum FindingSeverity { Info, Warning, Critical }

/// <summary>A single documentation-reconciliation finding (Module 1).</summary>
public sealed record Finding(
    FindingSeverity Severity,
    string Category,
    string Message,
    string File);
