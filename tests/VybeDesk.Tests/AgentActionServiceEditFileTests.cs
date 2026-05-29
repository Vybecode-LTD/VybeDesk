using VybeDesk.Core.Models;
using VybeDesk.Services.Agent;
using Xunit;

namespace VybeDesk.Tests;

public class AgentActionServiceEditFileTests
{
    private static (AgentActionService svc, string root) NewSvcWithScopedRoot()
    {
        // M3 #10 Phase B: AgentActionService now takes IAgentActionLogStore.
        // Reuse the in-memory fake from AgentActionServiceTests so all the
        // edit-file tests run without touching SQLite. Each test that calls
        // ExecuteAsync also sets an active project below so the log entry
        // is actually persisted (no project = no entry = CanUndo stays false).
        var svc = new AgentActionService(new AgentActionServiceTests.InMemoryAgentActionLogStore());
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });
        svc.SetActiveProject(Guid.NewGuid());
        return (svc, root);
    }

    private static string WriteFile(string root, string name, string content)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Validate_RejectsEditFilePathOutsideScopedRoot()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var outsidePath = Path.Combine(Path.GetTempPath(), "vybedesk-edit-out-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(outsidePath, "hello world");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = outsidePath,
            OldString = "hello",
            NewString = "bye",
        };

        var v = svc.Validate(action);
        Assert.False(v.IsValid);
        Assert.Contains("scoped", v.Message, StringComparison.OrdinalIgnoreCase);

        File.Delete(outsidePath);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_RejectsNonexistentFile()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var missing = Path.Combine(root, "does-not-exist.txt");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = missing,
            OldString = "x",
            NewString = "y",
        };

        var v = svc.Validate(action);
        Assert.False(v.IsValid);
        Assert.Contains("not found", v.Message, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_RejectsEmptyOldString()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var file = WriteFile(root, "a.txt", "hello world");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "",
            NewString = "bye",
        };

        var v = svc.Validate(action);
        Assert.False(v.IsValid);
        Assert.Contains("non-empty", v.Message, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_RejectsIdenticalOldAndNewStrings()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var file = WriteFile(root, "a.txt", "hello world");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "hello",
            NewString = "hello",
        };

        var v = svc.Validate(action);
        Assert.False(v.IsValid);
        Assert.Contains("nothing to do", v.Message, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_RejectsWhenOldStringNotFound()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var file = WriteFile(root, "a.txt", "hello world");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "absent",
            NewString = "replacement",
        };

        var v = svc.Validate(action);
        Assert.False(v.IsValid);
        Assert.Contains("not found in file", v.Message, StringComparison.OrdinalIgnoreCase);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_RejectsAmbiguousMatchWhenReplaceAllFalse()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var file = WriteFile(root, "a.txt", "foo foo foo");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "foo",
            NewString = "bar",
            ReplaceAll = false,
        };

        var v = svc.Validate(action);
        Assert.False(v.IsValid);
        Assert.Contains("appears 3 times", v.Message);
        Assert.Contains("replace_all=true", v.Message);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Validate_AcceptsAmbiguousMatchWhenReplaceAllTrue()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var file = WriteFile(root, "a.txt", "foo foo foo");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "foo",
            NewString = "bar",
            ReplaceAll = true,
        };

        var v = svc.Validate(action);
        Assert.True(v.IsValid);
        Assert.Equal("OK", v.Message);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Execute_ReplacesSingleOccurrence()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var file = WriteFile(root, "a.txt", "hello world");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "world",
            NewString = "moon",
        };

        await svc.ExecuteAsync(action);
        Assert.Equal("hello moon", await File.ReadAllTextAsync(file));
        Assert.True(svc.CanUndo);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Execute_ReplacesAllOccurrencesWhenReplaceAllTrue()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var file = WriteFile(root, "a.txt", "foo bar foo baz foo");

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "foo",
            NewString = "qux",
            ReplaceAll = true,
        };

        await svc.ExecuteAsync(action);
        Assert.Equal("qux bar qux baz qux", await File.ReadAllTextAsync(file));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Undo_RestoresOriginalFileContent()
    {
        var (svc, root) = NewSvcWithScopedRoot();
        var original = "line 1\nline 2\nline 3\n";
        var file = WriteFile(root, "a.txt", original);

        var action = new AgentAction
        {
            Kind = AgentActionKind.EditFile,
            Path = file,
            OldString = "line 2",
            NewString = "EDITED",
        };

        await svc.ExecuteAsync(action);
        Assert.Equal("line 1\nEDITED\nline 3\n", await File.ReadAllTextAsync(file));

        await svc.UndoLastAsync();
        Assert.Equal(original, await File.ReadAllTextAsync(file));
        Assert.False(svc.CanUndo);

        Directory.Delete(root, recursive: true);
    }
}
