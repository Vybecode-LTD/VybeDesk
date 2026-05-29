namespace VybeDesk.Core.Models;

/// <summary>Persisted, non-secret app settings. The API key is NOT stored here.</summary>
public sealed class AppSettings
{
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public string DefaultOutputPath { get; set; } = "";
}
