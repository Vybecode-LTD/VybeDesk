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
}

public enum ProjectStatus { Active, OnHold, Archived }
