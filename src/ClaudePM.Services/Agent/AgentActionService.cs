using ClaudePM.Core.Models;
using ClaudePM.Core.Services;

namespace ClaudePM.Services.Agent;

/// <summary>
/// Default <see cref="IAgentActionService"/>. Confines all actions to scoped
/// roots, allow-lists the action kinds, and keeps a closure-based undo stack.
/// Note: path confinement canonicalizes "." and ".." via Path.GetFullPath;
/// full symlink-target resolution is a future hardening step.
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
            try { _roots.Add(Path.GetFullPath(r)); }
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

    private bool TryConfine(string path, out string full, out string error)
    {
        full = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Path is empty.";
            return false;
        }
        try { full = Path.GetFullPath(path); }
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

    private sealed record ExecutedAction(string Description, Action Undo);
}
