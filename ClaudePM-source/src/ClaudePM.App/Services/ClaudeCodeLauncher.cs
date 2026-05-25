using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ClaudePM.Core.Services;

namespace ClaudePM.App.Services;

/// <summary>
/// Windows-only Claude Code launcher. Probes the PATH for the `claude` binary;
/// if present, opens a new cmd.exe window cd'd into the project and runs it.
/// If absent, copies a ready-to-paste `cd … && claude` command to the
/// clipboard so the user can run it themselves in their preferred terminal.
/// </summary>
public sealed class ClaudeCodeLauncher : IClaudeCodeLauncher
{
    private const string Binary = "claude";

    public async Task<ClaudeLaunchResult> LaunchAsync(string projectFolderPath)
    {
        if (string.IsNullOrWhiteSpace(projectFolderPath))
            return new ClaudeLaunchResult(false, "Set a folder path on the project first.");
        if (!Directory.Exists(projectFolderPath))
            return new ClaudeLaunchResult(false, "The project folder doesn't exist on disk.");

        var onPath = await IsBinaryOnPathAsync(Binary);
        if (onPath)
        {
            try
            {
                // /c start ""  →  spawn detached so a new console window opens
                // cmd.exe /k claude  →  keep the new window open after claude exits
                // WorkingDirectory sets cwd of the spawned cmd, which `start` inherits
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c start \"\" cmd.exe /k " + Binary,
                    WorkingDirectory = projectFolderPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                return new ClaudeLaunchResult(true,
                    "Launched Claude Code in a new terminal at " + projectFolderPath + ".");
            }
            catch (Exception ex)
            {
                return new ClaudeLaunchResult(false, "Launch failed: " + ex.Message);
            }
        }

        // Fallback: copy a runnable command to the clipboard so the user can
        // paste it in whatever terminal they prefer.
        var cmd = "cd \"" + projectFolderPath + "\" && " + Binary;
        var copied = await TryCopyToClipboardAsync(cmd);
        return copied
            ? new ClaudeLaunchResult(false,
                "`" + Binary + "` not on PATH. Command copied to clipboard.")
            : new ClaudeLaunchResult(false,
                "`" + Binary + "` not on PATH and clipboard unavailable. Run: " + cmd);
    }

    private static async Task<bool> IsBinaryOnPathAsync(string binary)
    {
        try
        {
            // `where` is the Windows equivalent of `which`. Exit code 0 = found.
            using var proc = Process.Start(new ProcessStartInfo("where", binary)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryCopyToClipboardAsync(string text)
    {
        var top = Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (top?.Clipboard is null) return false;

        try
        {
            await top.Clipboard.SetTextAsync(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
