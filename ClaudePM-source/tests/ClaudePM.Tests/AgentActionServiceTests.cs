using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using ClaudePM.Services.Agent;
using Xunit;

namespace ClaudePM.Tests;

public class AgentActionServiceTests
{
    /// <summary>
    /// Tiny in-memory <see cref="IAgentActionLogStore"/> for tests. Mirrors
    /// the SQLite store's contract (newest-first, status updates,
    /// per-project filtering) without touching the filesystem. Lives next
    /// to the tests rather than in a Test Helpers file because the agent
    /// suite is the only consumer.
    /// </summary>
    internal sealed class InMemoryAgentActionLogStore : IAgentActionLogStore
    {
        private readonly List<AgentActionLogEntry> _entries = new();
        public event Action? Changed;

        public Task AddAsync(AgentActionLogEntry entry, CancellationToken ct = default)
        {
            _entries.Add(entry);
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AgentActionLogEntry>> GetByProjectAsync(
            Guid projectId, int limit = 50, CancellationToken ct = default)
        {
            IReadOnlyList<AgentActionLogEntry> list = _entries
                .Where(e => e.ProjectId == projectId)
                .OrderByDescending(e => e.ExecutedAt)
                .Take(limit)
                .ToList();
            return Task.FromResult(list);
        }

        public Task<AgentActionLogEntry?> GetMostRecentUndoableAsync(
            Guid projectId, CancellationToken ct = default)
        {
            var entry = _entries
                .Where(e => e.ProjectId == projectId && e.Status == AgentActionLogStatus.Done)
                .OrderByDescending(e => e.ExecutedAt)
                .FirstOrDefault();
            return Task.FromResult(entry);
        }

        public Task<AgentActionLogEntry?> GetMostRecentUndoneAsync(
            Guid projectId, CancellationToken ct = default)
        {
            var entry = _entries
                .Where(e => e.ProjectId == projectId && e.Status == AgentActionLogStatus.Undone)
                .OrderByDescending(e => e.ExecutedAt)
                .FirstOrDefault();
            return Task.FromResult(entry);
        }

        public Task UpdateStatusAsync(Guid id, AgentActionLogStatus status, CancellationToken ct = default)
        {
            for (var i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == id)
                    _entries[i] = _entries[i] with { Status = status };
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid id, CancellationToken ct = default)
        {
            _entries.RemoveAll(e => e.Id == id);
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        public Task ClearProjectAsync(Guid projectId, CancellationToken ct = default)
        {
            _entries.RemoveAll(e => e.ProjectId == projectId);
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        // Read-only window for tests that want to assert on the persisted state.
        public IReadOnlyList<AgentActionLogEntry> All => _entries;
    }

    private static AgentActionService NewSvc(out InMemoryAgentActionLogStore store)
    {
        store = new InMemoryAgentActionLogStore();
        return new AgentActionService(store);
    }

    [Fact]
    public void Validate_RejectsPathOutsideScopedRoots()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });

        var outside = new AgentAction
        {
            Kind = AgentActionKind.CreateFolder,
            Path = Path.Combine(Path.GetTempPath(), "claudepm-outside-" + Guid.NewGuid()),
        };

        Assert.False(svc.Validate(outside).IsValid);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_RejectsTraversalEscape()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });

        var escape = new AgentAction
        {
            Kind = AgentActionKind.CreateFolder,
            Path = Path.Combine(root, "..", "claudepm-escaped"),
        };

        Assert.False(svc.Validate(escape).IsValid);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task CreateFile_ThenUndo_RemovesTheFile()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());

        var target = Path.Combine(root, "sub", "note.txt");
        var action = new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = target,
            Content = "hello",
        };

        await svc.ExecuteAsync(action);
        Assert.True(File.Exists(target));
        Assert.True(svc.CanUndo);

        await svc.UndoLastAsync();
        Assert.False(File.Exists(target));
        Assert.False(svc.CanUndo);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ReadFile_ReadsContentsInsideScope()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        var file = Path.Combine(root, "data.txt");
        File.WriteAllText(file, "hello world");

        var r = svc.ReadFile(file);

        Assert.True(r.Success);
        Assert.Equal("hello world", r.Content);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ReadFile_BlocksPathOutsideScope()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        var outside = Path.Combine(Path.GetTempPath(), "claudepm-out-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(outside, "should not be readable");

        var r = svc.ReadFile(outside);

        Assert.False(r.Success);
        Assert.Contains("scoped", r.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        File.Delete(outside);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ReadFile_TruncatesPastMaxBytes()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        var file = Path.Combine(root, "big.txt");
        File.WriteAllText(file, new string('a', 1000));

        var r = svc.ReadFile(file, maxBytes: 100);

        Assert.True(r.Success);
        Assert.Contains("truncated", r.Content);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ListDirectory_ReturnsEntriesInsideScope()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllText(Path.Combine(root, "a.txt"), "");
        File.WriteAllText(Path.Combine(root, "b.txt"), "");

        var r = svc.ListDirectory(root);

        Assert.True(r.Success);
        Assert.Contains("sub/", r.Entries);
        Assert.Contains("a.txt", r.Entries);
        Assert.Contains("b.txt", r.Entries);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ListDirectory_BlocksPathOutsideScope()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });

        var r = svc.ListDirectory(Path.GetTempPath());

        Assert.False(r.Success);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_RejectsActionThroughSymlinkOutsideScope()
    {
        // A symlink under a scoped root that points OUTSIDE the root must
        // not become an escape hatch. Path.GetFullPath alone does not catch
        // this — the fix walks segments and resolves links.
        var root = Directory.CreateTempSubdirectory().FullName;
        var outside = Directory.CreateTempSubdirectory().FullName;
        var linkPath = Path.Combine(root, "escape");

        try { Directory.CreateSymbolicLink(linkPath, outside); }
        catch (UnauthorizedAccessException) { return; } // no symlink perm — skip
        catch (IOException) { return; }                 // no symlink perm — skip

        try
        {
            var svc = NewSvc(out _);
            svc.SetScopedRoots(new[] { root });

            var action = new AgentAction
            {
                Kind = AgentActionKind.CreateFile,
                Path = Path.Combine(linkPath, "evil.txt"),
                Content = "should not write",
            };

            var v = svc.Validate(action);
            Assert.False(v.IsValid);
            Assert.Contains("scoped", v.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(linkPath); } catch { }
            try { Directory.Delete(root, recursive: true); } catch { }
            try { Directory.Delete(outside, recursive: true); } catch { }
        }
    }

    // ─── M3 #10 Phase B: store-backed coverage ──────────────────────────

    [Fact]
    public async Task Execute_PersistsEntryToLogStoreScopedToActiveProject()
    {
        var svc = NewSvc(out var store);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        var projectId = Guid.NewGuid();
        svc.SetActiveProject(projectId);

        var target = Path.Combine(root, "logged.txt");
        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = target,
            Content = "x",
        });

        var entry = Assert.Single(store.All);
        Assert.Equal(projectId, entry.ProjectId);
        Assert.Equal(AgentActionKind.CreateFile, entry.Kind);
        Assert.Equal(AgentActionLogStatus.Done, entry.Status);
        Assert.Contains("logged.txt", entry.Description);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Execute_DoesNotLogWhenNoActiveProject()
    {
        // No active project = nothing to scope the entry to. Action still
        // runs (and is observable via the filesystem); the log just doesn't
        // capture it. CanUndo stays false.
        var svc = NewSvc(out var store);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        // Note: deliberately NOT calling SetActiveProject.

        var target = Path.Combine(root, "no-project.txt");
        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = target,
            Content = "x",
        });

        Assert.Empty(store.All);
        Assert.False(svc.CanUndo);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Undo_MarksEntryUndoneRatherThanRemoving()
    {
        // The history list shows "done then undone" — the row is NOT
        // deleted, only its status flips. This was an explicit Phase B
        // requirement (UpdateStatusAsync, not RemoveAsync).
        var svc = NewSvc(out var store);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());

        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = Path.Combine(root, "trail.txt"),
            Content = "x",
        });

        await svc.UndoLastAsync();

        var entry = Assert.Single(store.All);
        Assert.Equal(AgentActionLogStatus.Undone, entry.Status);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task UndoLast_PullsMostRecentDoneForActiveProjectOnly()
    {
        // Cross-project isolation: an action in project A should not be
        // undoable from project B.
        var svc = NewSvc(out var store);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });

        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();

        svc.SetActiveProject(projectA);
        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = Path.Combine(root, "a.txt"),
            Content = "a",
        });

        svc.SetActiveProject(projectB);
        // SetActiveProject fires-and-forgets the cache refresh; yield
        // back to the scheduler so the refresh task runs before we
        // assert on CanUndo (which reads the cache snapshot).
        await Task.Yield();
        await Task.Yield();
        Assert.False(svc.CanUndo); // project B has no actions

        // Try to undo while on project B — no-op (project A's action is
        // not visible; GetMostRecentUndoableAsync filters by ProjectId).
        await svc.UndoLastAsync();
        Assert.True(File.Exists(Path.Combine(root, "a.txt")));

        // Switch back to project A — the action reappears as undoable.
        svc.SetActiveProject(projectA);
        await Task.Yield();
        await Task.Yield();
        Assert.True(svc.CanUndo);

        Directory.Delete(root, recursive: true);
    }

    // ─── v0.31 redo coverage ─────────────────────────────────────────────

    [Fact]
    public async Task Redo_RestoresCreateFileWithSavedContent()
    {
        // Execute → undo (file gone) → user (or test) deletes nothing
        // else → redo. The file must reappear with the exact bytes the
        // agent originally proposed, sourced from the entry's NewContent.
        var svc = NewSvc(out var store);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());

        var target = Path.Combine(root, "redo-me.txt");
        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = target,
            Content = "first draft\nline 2",
        });

        await svc.UndoLastAsync();
        Assert.False(File.Exists(target));
        Assert.True(svc.CanRedo);
        Assert.False(svc.CanUndo);

        await svc.RedoLastAsync();
        Assert.True(File.Exists(target));
        Assert.Equal("first draft\nline 2", await File.ReadAllTextAsync(target));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Redo_RestoresEditFileWithNewContent()
    {
        // Edit (original → edited) → undo (back to original) → redo
        // (back to edited). Verifies NewContent for EditFile carries the
        // post-edit body, not the pre-edit body.
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());

        var original = "line A\nline B\nline C\n";
        var file = Path.Combine(root, "doc.txt");
        File.WriteAllText(file, original);

        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "line B",
            NewString = "EDITED",
        });
        var edited = "line A\nEDITED\nline C\n";
        Assert.Equal(edited, await File.ReadAllTextAsync(file));

        await svc.UndoLastAsync();
        Assert.Equal(original, await File.ReadAllTextAsync(file));
        Assert.True(svc.CanRedo);

        await svc.RedoLastAsync();
        Assert.Equal(edited, await File.ReadAllTextAsync(file));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Redo_MarksEntryDone_AfterSuccess()
    {
        // Status flips back to Done after a successful redo — the row is
        // mutated in place rather than a new row being appended.
        var svc = NewSvc(out var store);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());

        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = Path.Combine(root, "flip.txt"),
            Content = "data",
        });
        await svc.UndoLastAsync();
        Assert.Single(store.All); // still one entry, just Undone now

        await svc.RedoLastAsync();

        var entry = Assert.Single(store.All);
        Assert.Equal(AgentActionLogStatus.Done, entry.Status);
        Assert.True(svc.CanUndo);
        Assert.False(svc.CanRedo);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task CanRedo_TrueWhenUndoneEntryExists_FalseOtherwise()
    {
        // Empty cache → false. After execute (Done) → false. After undo
        // (Undone) → true. After redo (Done again) → false.
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());

        Assert.False(svc.CanRedo);

        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = Path.Combine(root, "x.txt"),
            Content = "x",
        });
        Assert.False(svc.CanRedo);
        Assert.True(svc.CanUndo);

        await svc.UndoLastAsync();
        Assert.True(svc.CanRedo);
        Assert.False(svc.CanUndo);

        await svc.RedoLastAsync();
        Assert.False(svc.CanRedo);
        Assert.True(svc.CanUndo);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task RecentActions_ReturnsNewestFirstForActiveProject()
    {
        var svc = NewSvc(out _);
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());

        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = Path.Combine(root, "first.txt"),
            Content = "1",
        });
        // Small delay so the second entry has a strictly-later timestamp;
        // otherwise OrderByDescending(ExecutedAt) is non-deterministic at
        // sub-millisecond resolution on Windows clocks.
        await Task.Delay(15);
        await svc.ExecuteAsync(new AgentAction
        {
            Kind = AgentActionKind.CreateFile,
            Path = Path.Combine(root, "second.txt"),
            Content = "2",
        });

        var recent = svc.RecentActions;
        Assert.Equal(2, recent.Count);
        Assert.Contains("second.txt", recent[0].Description);
        Assert.Contains("first.txt", recent[1].Description);

        Directory.Delete(root, recursive: true);
    }
}
