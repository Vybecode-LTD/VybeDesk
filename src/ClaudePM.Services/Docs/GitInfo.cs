using System.Diagnostics;

namespace ClaudePM.Services.Docs;

/// <summary>
/// Thin wrapper around the <c>git</c> CLI for staleness signals in the
/// documentation reconciliation pass. Every method tolerates a missing
/// <c>git</c> binary, a non-repo folder, or an untracked file by returning
/// null — callers degrade gracefully when there's no git context available.
/// </summary>
internal static class GitInfo
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Last commit time for a specific file, or for the whole project when
    /// <paramref name="absolutePath"/> is null.
    /// </summary>
    public static async Task<DateTimeOffset?> GetLastCommitTimeAsync(
        string repoRoot, string? absolutePath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("log");
        psi.ArgumentList.Add("-1");
        psi.ArgumentList.Add("--format=%ct");
        if (absolutePath is not null)
        {
            psi.ArgumentList.Add("--");
            psi.ArgumentList.Add(absolutePath);
        }

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Timeout);

            string output;
            try
            {
                output = await proc.StandardOutput.ReadToEndAsync(cts.Token);
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return null;
            }

            if (proc.ExitCode != 0) return null;

            var trimmed = output.Trim();
            if (string.IsNullOrEmpty(trimmed)) return null;
            return long.TryParse(trimmed, out var unixSeconds)
                ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                : null;
        }
        catch
        {
            // git missing, not on PATH, permission denied, etc. — silent no-op.
            return null;
        }
    }
}
