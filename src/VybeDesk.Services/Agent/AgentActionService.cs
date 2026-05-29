using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Services.Agent;

/// <summary>
/// Default <see cref="IAgentActionService"/>. Confines all actions to scoped
/// roots, allow-lists the action kinds, and persists executed actions to
/// <see cref="IAgentActionLogStore"/> so undo survives an app restart.
/// Path confinement canonicalizes "." and ".." via Path.GetFullPath AND
/// resolves symlinks (including symlinked ancestors) so a junction inside
/// a scoped root can't be used to escape it.
///
/// M3 #10 Phase B refactor: the previous <c>Stack&lt;ExecutedAction&gt;</c>
/// + closure pattern is gone. Undo is reconstructed from the persisted
/// entry's fields (Kind / Path / DestinationPath / OriginalContent) at
/// the moment the user clicks Undo, which means cross-session undo is a
/// free consequence of the data living in SQLite.
/// </summary>
public sealed class AgentActionService : IAgentActionService, IDisposable
{
    private readonly IAgentActionLogStore _log;
    private readonly Action _onLogChanged;
    private volatile string[] _roots = Array.Empty<string>();

    // Cached snapshot of the active project's log entries (newest-first).
    // Refreshed when the store fires Changed and when the active project
    // is swapped. Reads of CanUndo / RecentActions hit memory, never the
    // database — the database is touched only on mutations.
    private IReadOnlyList<AgentActionLogEntry> _recentCache = Array.Empty<AgentActionLogEntry>();
    private Guid? _activeProjectId;

    public AgentActionService(IAgentActionLogStore log)
    {
        _log = log;
        _onLogChanged = OnLogChanged;
        _log.Changed += _onLogChanged;
    }

    public IReadOnlyList<string> ScopedRoots => _roots;

    public Guid? ActiveProjectId => _activeProjectId;

    public bool CanUndo => _recentCache.Any(e => e.Status == AgentActionLogStatus.Done);
    public bool CanRedo => _recentCache.Any(e => e.Status == AgentActionLogStatus.Undone);
    public IReadOnlyList<AgentActionLogEntry> RecentActions => _recentCache;

    public event Action? RecentActionsChanged;

    public void SetScopedRoots(IEnumerable<string> roots)
    {
        var list = new List<string>();
        foreach (var r in roots)
        {
            if (string.IsNullOrWhiteSpace(r)) continue;
            try { list.Add(ResolveSymlinks(Path.GetFullPath(r))); }
            catch { /* skip unparseable root */ }
        }
        _roots = list.ToArray();
    }

    public void SetActiveProject(Guid? projectId)
    {
        _activeProjectId = projectId;
        // Fire-and-forget: the cache is best-effort. If the read fails
        // (e.g. database temporarily locked), we'll still notify with
        // whatever state we had so the UI doesn't deadlock on an
        // unfinished await.
        _ = RefreshRecentActionsAsync();
    }

    private async Task RefreshRecentActionsAsync()
    {
        try
        {
            if (_activeProjectId is { } pid)
                _recentCache = await _log.GetByProjectAsync(pid);
            else
                _recentCache = Array.Empty<AgentActionLogEntry>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                "AgentActionService.RefreshRecentActionsAsync failed: " + ex.Message);
            // Leave the cache as-is; better stale than wrong.
        }
        finally
        {
            RecentActionsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Store-side Changed handler: refresh our cache for the currently
    /// active project and notify subscribers. Because the store fires from
    /// whatever thread did the mutation, this method does not assume the
    /// UI thread; consumers that need to marshal back to it should do so
    /// in their own subscription.
    /// </summary>
    private void OnLogChanged() => _ = RefreshRecentActionsAsync();

    public string Describe(AgentAction a) => a.Kind switch
    {
        AgentActionKind.CreateFile => "Create file: " + a.Path,
        AgentActionKind.CreateFolder => "Create folder: " + a.Path,
        AgentActionKind.Move => "Move: " + a.Path + " -> " + a.DestinationPath,
        AgentActionKind.EditFile => "Edit file: " + a.Path
            + " (" + (a.ReplaceAll ? "replace all" : "single match")
            + ", " + a.OldString.Length + " → " + a.NewString.Length + " chars)",
        _ => "Unknown action",
    };

    public ActionValidation Validate(AgentAction a)
    {
        if (_roots.Length == 0)
            return new ActionValidation(false, "No scoped project roots are configured.");

        switch (a.Kind)
        {
            case AgentActionKind.CreateFile:
            case AgentActionKind.CreateFolder:
            {
                if (!TryConfine(a.Path, out var full, out var err))
                    return new ActionValidation(false, err);
                if (File.Exists(full) || Directory.Exists(full))
                    return new ActionValidation(false, "Target already exists.");
                return new ActionValidation(true, "OK");
            }
            case AgentActionKind.Move:
            {
                if (!TryConfine(a.Path, out var src, out var e1))
                    return new ActionValidation(false, "Source: " + e1);
                if (!File.Exists(src) && !Directory.Exists(src))
                    return new ActionValidation(false, "Source does not exist.");
                if (!TryConfine(a.DestinationPath, out var dst, out var e2))
                    return new ActionValidation(false, "Destination: " + e2);
                if (File.Exists(dst) || Directory.Exists(dst))
                    return new ActionValidation(false, "Destination already exists.");
                return new ActionValidation(true, "OK");
            }
            case AgentActionKind.EditFile:
            {
                if (!TryConfine(a.Path, out var full, out var err))
                    return new ActionValidation(false, err);
                if (!File.Exists(full))
                    return new ActionValidation(false, "File not found.");
                if (string.IsNullOrEmpty(a.OldString))
                    return new ActionValidation(false, "old_string must be non-empty.");
                if (a.OldString == a.NewString)
                    return new ActionValidation(false, "old_string equals new_string — nothing to do.");

                string content;
                try { content = File.ReadAllText(full); }
                catch (Exception ex) { return new ActionValidation(false, "Cannot read file: " + ex.Message); }

                var count = CountOccurrences(content, a.OldString);
                if (count == 0)
                    return new ActionValidation(false, "old_string not found in file.");
                if (!a.ReplaceAll && count > 1)
                    return new ActionValidation(false,
                        "old_string appears " + count + " times — set replace_all=true or include more context.");
                return new ActionValidation(true, "OK");
            }
            default:
                return new ActionValidation(false, "Unknown action kind.");
        }
    }

    public async Task ExecuteAsync(AgentAction a, CancellationToken ct = default)
    {
        var v = Validate(a);
        if (!v.IsValid)
            throw new InvalidOperationException("Action rejected: " + v.Message);

        switch (a.Kind)
        {
            case AgentActionKind.CreateFile:
            {
                var full = Path.GetFullPath(a.Path);
                var dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(full, a.Content, ct);
                // Capture the proposed content as NewContent so redo can
                // recreate the file byte-for-byte after an undo.
                await LogAsync(a, originalContent: null, newContent: a.Content, ct);
                break;
            }
            case AgentActionKind.CreateFolder:
            {
                var full = Path.GetFullPath(a.Path);
                Directory.CreateDirectory(full);
                await LogAsync(a, originalContent: null, newContent: null, ct);
                break;
            }
            case AgentActionKind.Move:
            {
                var src = Path.GetFullPath(a.Path);
                var dst = Path.GetFullPath(a.DestinationPath);
                if (Directory.Exists(src)) Directory.Move(src, dst);
                else File.Move(src, dst);
                await LogAsync(a, originalContent: null, newContent: null, ct);
                break;
            }
            case AgentActionKind.EditFile:
            {
                var full = Path.GetFullPath(a.Path);
                // Capture original content BEFORE overwrite so the log
                // entry has the exact bytes to restore on undo.
                var originalContent = await File.ReadAllTextAsync(full, ct);
                var newContent = a.ReplaceAll
                    ? originalContent.Replace(a.OldString, a.NewString)
                    : ReplaceFirst(originalContent, a.OldString, a.NewString);
                await File.WriteAllTextAsync(full, newContent, ct);
                // Pass both: originalContent restores on undo, newContent
                // re-applies on redo (the file may have been deleted by
                // the user between undo and redo).
                await LogAsync(a, originalContent, newContent, ct);
                break;
            }
        }
    }

    /// <summary>
    /// Persist the executed action so it can be undone later (including
    /// after an app restart). No-ops when no project is active — the log
    /// is per-project, and there is nothing meaningful to scope an entry
    /// to without one.
    /// </summary>
    private async Task LogAsync(AgentAction a, string? originalContent, string? newContent, CancellationToken ct)
    {
        if (_activeProjectId is not { } pid) return;
        var entry = new AgentActionLogEntry
        {
            Id = Guid.NewGuid(),
            ProjectId = pid,
            Kind = a.Kind,
            Path = a.Path,
            DestinationPath = a.DestinationPath,
            OriginalContent = originalContent,
            NewContent = newContent,
            Status = AgentActionLogStatus.Done,
            ExecutedAt = DateTimeOffset.Now,
            Description = Describe(a),
        };
        await _log.AddAsync(entry, ct);
        // Synchronously refresh so the post-Execute state is internally
        // consistent (CanUndo / RecentActions reflect the new entry by the
        // time the caller's await returns). The store's Changed event ALSO
        // triggers a refresh on the next tick — harmless duplication.
        await RefreshRecentActionsAsync();
    }

    public async Task UndoLastAsync(CancellationToken ct = default)
    {
        if (_activeProjectId is not { } pid) return;
        var entry = await _log.GetMostRecentUndoableAsync(pid, ct);
        if (entry is null) return;

        // Re-validate paths against current scoped roots. Between the
        // original Execute and this Undo the user may have removed the
        // project, changed roots, or the log could reference a path
        // that's no longer confined. Defence-in-depth: refuse to touch
        // anything outside the current sandbox.
        if (!TryConfine(entry.Path, out _, out _)) return;
        if (entry.Kind == AgentActionKind.Move &&
            !string.IsNullOrEmpty(entry.DestinationPath) &&
            !TryConfine(entry.DestinationPath, out _, out _))
            return;

        switch (entry.Kind)
        {
            case AgentActionKind.CreateFile:
                if (File.Exists(entry.Path)) File.Delete(entry.Path);
                break;
            case AgentActionKind.CreateFolder:
                if (Directory.Exists(entry.Path)) Directory.Delete(entry.Path, recursive: false);
                break;
            case AgentActionKind.Move:
                // Reverse the move: dst → src. If the destination is gone
                // (e.g. user moved it again externally), there's nothing
                // we can safely do — leave the log entry alone (don't
                // mark Undone) so the user sees we didn't actually undo.
                if (Directory.Exists(entry.DestinationPath))
                    Directory.Move(entry.DestinationPath, entry.Path);
                else if (File.Exists(entry.DestinationPath))
                    File.Move(entry.DestinationPath, entry.Path);
                else
                    return;
                break;
            case AgentActionKind.EditFile:
                if (entry.OriginalContent is null) return; // nothing to restore
                await File.WriteAllTextAsync(entry.Path, entry.OriginalContent, ct);
                break;
        }

        // Mark Undone (NOT Remove) so the history still shows the
        // "done then undone" trail to the user.
        await _log.UpdateStatusAsync(entry.Id, AgentActionLogStatus.Undone, ct);
        // Same rationale as LogAsync: keep the cache hot before returning.
        await RefreshRecentActionsAsync();
    }

    public async Task RedoLastAsync(CancellationToken ct = default)
    {
        if (_activeProjectId is not { } pid) return;
        var entry = await _log.GetMostRecentUndoneAsync(pid, ct);
        if (entry is null) return;

        // Same defence-in-depth as UndoLastAsync — re-validate paths
        // against current scoped roots before touching the filesystem.
        if (!TryConfine(entry.Path, out _, out _)) return;
        if (entry.Kind == AgentActionKind.Move &&
            !string.IsNullOrEmpty(entry.DestinationPath) &&
            !TryConfine(entry.DestinationPath, out _, out _))
            return;

        switch (entry.Kind)
        {
            case AgentActionKind.CreateFile:
                // Without saved content we'd be redoing a file with empty
                // body — refuse rather than guess. Legacy entries written
                // before NewContent existed end up here as null.
                if (entry.NewContent is null) return;
                var createDir = Path.GetDirectoryName(entry.Path);
                if (!string.IsNullOrEmpty(createDir)) Directory.CreateDirectory(createDir);
                await File.WriteAllTextAsync(entry.Path, entry.NewContent, ct);
                break;
            case AgentActionKind.CreateFolder:
                Directory.CreateDirectory(entry.Path);
                break;
            case AgentActionKind.Move:
                // Re-do the move: src → dst. If src is gone (e.g. user
                // moved it externally between undo and redo) there's
                // nothing safe to do.
                if (Directory.Exists(entry.Path)) Directory.Move(entry.Path, entry.DestinationPath);
                else if (File.Exists(entry.Path)) File.Move(entry.Path, entry.DestinationPath);
                else return;
                break;
            case AgentActionKind.EditFile:
                if (entry.NewContent is null) return;
                await File.WriteAllTextAsync(entry.Path, entry.NewContent, ct);
                break;
        }

        // Flip back to Done. The history now reads as "done → undone →
        // done" via Description + Status timeline, but only this single
        // row is mutated — we don't append a new entry.
        await _log.UpdateStatusAsync(entry.Id, AgentActionLogStatus.Done, ct);
        await RefreshRecentActionsAsync();
    }

    public ReadFileResult ReadFile(string path, int maxBytes = 50_000)
    {
        if (!TryConfine(path, out var full, out var err))
            return new ReadFileResult(false, "", err);
        if (!File.Exists(full))
            return new ReadFileResult(false, "", "File not found.");
        try
        {
            var bytes = File.ReadAllBytes(full);
            var truncated = bytes.Length > maxBytes;
            var slice = truncated ? bytes.AsSpan(0, maxBytes).ToArray() : bytes;
            var text = System.Text.Encoding.UTF8.GetString(slice);
            if (truncated)
                text += "\n\n[...truncated; full file is " + bytes.Length + " bytes...]";
            return new ReadFileResult(true, text, "");
        }
        catch (Exception ex)
        {
            return new ReadFileResult(false, "", ex.Message);
        }
    }

    public ListDirectoryResult ListDirectory(string path, int maxEntries = 200)
    {
        if (!TryConfine(path, out var full, out var err))
            return new ListDirectoryResult(false, Array.Empty<string>(), err);
        if (!Directory.Exists(full))
            return new ListDirectoryResult(false, Array.Empty<string>(), "Directory not found.");
        try
        {
            var entries = new List<string>();
            foreach (var d in Directory.EnumerateDirectories(full))
                entries.Add(Path.GetFileName(d) + "/");
            foreach (var f in Directory.EnumerateFiles(full))
                entries.Add(Path.GetFileName(f));
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            var truncated = entries.Count > maxEntries;
            if (truncated)
            {
                var hidden = entries.Count - maxEntries;
                entries = entries.Take(maxEntries).ToList();
                entries.Add("[...truncated; " + hidden + " more entries hidden...]");
            }
            return new ListDirectoryResult(true, entries, "");
        }
        catch (Exception ex)
        {
            return new ListDirectoryResult(false, Array.Empty<string>(), ex.Message);
        }
    }

    private bool TryConfine(string path, out string full, out string error)
    {
        full = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Path is empty.";
            return false;
        }
        try { full = ResolveSymlinks(Path.GetFullPath(path)); }
        catch
        {
            error = "Path is invalid.";
            return false;
        }

        var roots = _roots;
        foreach (var root in roots)
        {
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                error = "";
                return true;
            }
        }
        error = "Path is outside all scoped project roots.";
        return false;
    }

    /// <summary>
    /// Walks the path segment-by-segment from the root down, resolving any
    /// existing segment that is a symlink/junction to its final target.
    /// Non-existent suffix segments are appended to the resolved prefix
    /// (so create_file against a not-yet-existing path under a symlinked
    /// parent still ends up at the real filesystem location).
    /// </summary>
    private static string ResolveSymlinks(string fullPath)
    {
        try
        {
            var segments = new List<string>();
            var current = fullPath;
            while (true)
            {
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent))
                {
                    segments.Insert(0, current);
                    break;
                }
                segments.Insert(0, Path.GetFileName(current));
                current = parent;
            }
            if (segments.Count == 0) return fullPath;

            var resolved = segments[0];
            for (int i = 1; i < segments.Count; i++)
            {
                resolved = Path.Combine(resolved, segments[i]);
                FileSystemInfo? info = null;
                if (Directory.Exists(resolved)) info = new DirectoryInfo(resolved);
                else if (File.Exists(resolved)) info = new FileInfo(resolved);
                if (info is not null)
                {
                    var target = info.ResolveLinkTarget(returnFinalTarget: true);
                    if (target is not null) resolved = target.FullName;
                }
            }
            return resolved;
        }
        catch
        {
            return fullPath;
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }

    private static string ReplaceFirst(string haystack, string needle, string replacement)
    {
        var i = haystack.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0) return haystack;
        return haystack[..i] + replacement + haystack[(i + needle.Length)..];
    }

    public void Dispose()
    {
        _log.Changed -= _onLogChanged;
    }
}
