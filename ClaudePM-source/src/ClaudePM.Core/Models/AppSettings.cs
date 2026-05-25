namespace ClaudePM.Core.Models;

/// <summary>Persisted, non-secret app settings. The API key is NOT stored here.</summary>
public sealed class AppSettings
{
    public string Model { get; set; } = "claude-opus-4-7";
    public string DefaultOutputPath { get; set; } = "";
    public List<string> ProjectRoots { get; set; } = new();
    public string Theme { get; set; } = "Dark";
}
