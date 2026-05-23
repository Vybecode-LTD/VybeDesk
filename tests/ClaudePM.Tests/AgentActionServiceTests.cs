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
}
