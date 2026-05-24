using ClaudePM.Core.Models;
using ClaudePM.Services.Agent;
using Xunit;

namespace ClaudePM.Tests;

public class AgentActionServiceTests
{
    [Fact]
    public void Validate_RejectsPathOutsideScopedRoots()
    {
        var svc = new AgentActionService();
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
        var svc = new AgentActionService();
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
        var svc = new AgentActionService();
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });

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
        var svc = new AgentActionService();
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
        var svc = new AgentActionService();
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
        var svc = new AgentActionService();
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
        var svc = new AgentActionService();
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
        var svc = new AgentActionService();
        var root = Directory.CreateTempSubdirectory().FullName;
        svc.SetScopedRoots(new[] { root });

        var r = svc.ListDirectory(Path.GetTempPath());

        Assert.False(r.Success);
        Directory.Delete(root, recursive: true);
    }
}
