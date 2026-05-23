using ClaudePM.Core.Models;
using ClaudePM.Services.Ai;
using ClaudePM.Services.Session;
using Xunit;

namespace ClaudePM.Tests;

public class SessionBuilderServiceTests
{
    [Fact]
    public async Task GenerateAsync_WritesHandoffPackage()
    {
        var outDir = Directory.CreateTempSubdirectory().FullName;
        var svc = new SessionBuilderService(new StubAiService());

        var plan = new SessionPlan
        {
            ProjectName = "Test Project",
            Description = "A test handoff.",
            Stack = "Avalonia / .NET",
            OutputLocation = outDir,
            Transcripts = { new TranscriptEntry { Title = "Chat 1", Body = "Some discussion." } },
        };

        var root = await svc.GenerateAsync(plan);

        Assert.True(File.Exists(Path.Combine(root, "CLAUDE.md")));
        Assert.True(File.Exists(Path.Combine(root, "README.md")));
        Assert.True(File.Exists(Path.Combine(root, ".gitignore")));
        Assert.True(File.Exists(Path.Combine(root, "KICKOFF.md")));
        Assert.True(Directory.Exists(Path.Combine(root, "docs", "transcripts")));
        Assert.Single(Directory.GetFiles(Path.Combine(root, "docs", "transcripts")));

        Directory.Delete(outDir, recursive: true);
    }

    [Fact]
    public async Task GenerateAsync_RequiresProjectName()
    {
        var svc = new SessionBuilderService(new StubAiService());
        var plan = new SessionPlan { ProjectName = "", OutputLocation = Path.GetTempPath() };

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GenerateAsync(plan));
    }
}
