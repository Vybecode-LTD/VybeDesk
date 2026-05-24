using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Agent;

/// <summary>
/// Default <see cref="IAgentActionService"/>. Confines all actions to scoped
/// roots, allow-lists the action kinds, and keeps a closure-based undo stack.
/// Path confinement canonicalizes "." and ".." via Path.GetFullPath AND
/// resolves symlinks (including symlinked ancestors) so a junction inside
/// a scoped root can't be used to escape it.
/// </summary>
public sealed class AgentActionService : IAgentActionService
{
    private readonly List<string> _roots = new();
    private readonly Stack<ExecutedAction> _undo = new();

    public IReadOnlyList<string> ScopedRoots => _roots;
    public bool CanUndo => _undo.Count > 0;
    public IReadOnlyList<string> UndoHistory => _undo.Select(e => e.Description).ToList();

    public void SetScopedRoots(IEnumerable<string> roots)
    {
        _roots.Clear();
        foreach (var r in roots)
        {
            if (string.IsNullOrWhiteSpace(r)) continue;
            try { _roots.Add(ResolveSymlinks(Path.GetFullPath(r))); }
            catch { /* skip unparseable root */ }
        }
    }

    public string Describe(AgentAction a) => a.Kind switch
    {
        AgentActionKind.CreateFile => "Create file: " + a.Path,
        AgentActionKind.CreateFolder => "Create folder: " + a.Path,
        AgentActionKind.Move => "Move: " + a.Path + " -> " + a.DestinationPath,
        _ => "Unknown action",
    };

    public ActionValidation Validate(AgentAction a)
    {
        if (_roots.Count == 0)
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
                _undo.Push(new ExecutedAction(Describe(a),
                    () => { if (File.Exists(full)) File.Delete(full); }));
                break;
            }
            case AgentActionKind.CreateFolder:
            {
                var full = Path.GetFullPath(a.Path);
                Directory.CreateDirectory(full);
                _undo.Push(new ExecutedAction(Describe(a),
                    () => { if (Directory.Exists(full)) Directory.Delete(full, recursive: false); }));
                break;
            }
            case AgentActionKind.Move:
            {
                var src = Path.GetFullPath(a.Path);
                var dst = Path.GetFullPath(a.DestinationPath);
                if (Directory.Exists(src)) Directory.Move(src, dst);
                else File.Move(src, dst);
                _undo.Push(new ExecutedAction(Describe(a), () =>
                {
                    if (Directory.Exists(dst)) Directory.Move(dst, src);
                    else if (File.Exists(dst)) File.Move(dst, src);
                }));
                break;
            }
        }
    }

    public Task UndoLastAsync(CancellationToken ct = default)
    {
        if (_undo.Count > 0)
            _undo.Pop().Undo();
        return Task.CompletedTask;
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

        foreach (var root in _roots)
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

    private sealed record ExecutedAction(string Description, Action Undo);
}
