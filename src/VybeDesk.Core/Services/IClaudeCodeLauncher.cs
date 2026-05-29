namespace VybeDesk.Core.Services;

/// <summary>
/// Launches the Claude Code CLI against a project folder, or falls back to
/// copying a ready-to-paste command to the clipboard if the binary isn't on
/// PATH. Implemented in the App layer because launching and clipboard access
/// are platform / framework concerns.
/// </summary>
public interface IClaudeCodeLauncher
{
    Task<ClaudeLaunchResult> LaunchAsync(string projectFolderPath);
}

/// <summary>
/// Outcome of a Claude Code launch attempt. <see cref="Launched"/> is true
/// when the CLI was actually spawned; false when we fell back to clipboard
/// (or failed). <see cref="Message"/> is user-facing.
/// </summary>
public sealed record ClaudeLaunchResult(bool Launched, string Message);
