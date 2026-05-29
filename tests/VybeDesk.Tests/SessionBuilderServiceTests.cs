using VybeDesk.Core.Models;
using VybeDesk.Services.Session;
using VybeDesk.Tests.Doubles;
using Xunit;

namespace VybeDesk.Tests;

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

    /// <summary>
    /// Proves the M4 #15 wiring is intact end-to-end: picking the
    /// Avalonia template at the wizard level results in stack-specific
    /// content landing on disk (not the plain default). Guards against
    /// future refactors that accidentally bypass <see cref="SessionTemplates"/>.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_WritesAvaloniaTemplateContent_WhenTemplateIsAvaloniaDotNet()
    {
        var outDir = Directory.CreateTempSubdirectory().FullName;
        var svc = new SessionBuilderService(new StubAiService());

        var plan = new SessionPlan
        {
            ProjectName = "AvaloniaProj",
            Description = "An Avalonia handoff.",
            Stack = "Avalonia / .NET 9",
            OutputLocation = outDir,
            Template = SessionTemplate.AvaloniaDotNet,
        };

        var root = await svc.GenerateAsync(plan);

        var claudeMd = await File.ReadAllTextAsync(Path.Combine(root, "CLAUDE.md"));
        var gitignore = await File.ReadAllTextAsync(Path.Combine(root, ".gitignore"));

        Assert.Contains("CommunityToolkit.Mvvm", claudeMd);
        Assert.Contains("compiled bindings", claudeMd, StringComparison.OrdinalIgnoreCase);
        // Avalonia template uses the .NET gitignore — bin/ obj/ markers.
        Assert.Contains("bin/", gitignore);
        Assert.Contains("obj/", gitignore);
        // The plain-template marker should NOT leak in.
        Assert.DoesNotContain("Generic scaffolding", claudeMd);

        Directory.Delete(outDir, recursive: true);
    }

    [Fact]
    public async Task GenerateAsync_RefusesToOverwriteExistingDirectory()
    {
        var outDir = Directory.CreateTempSubdirectory().FullName;
        var svc = new SessionBuilderService(new StubAiService());

        var plan = new SessionPlan
        {
            ProjectName = "Overwrite-Test",
            Description = "First run.",
            OutputLocation = outDir,
        };

        // First generation succeeds.
        var root = await svc.GenerateAsync(plan);
        Assert.True(File.Exists(Path.Combine(root, "CLAUDE.md")));

        // Second generation with the same project name must refuse —
        // the output directory already has content from the first run.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GenerateAsync(plan));
        Assert.Contains("already exists", ex.Message);

        Directory.Delete(outDir, recursive: true);
    }

    /// <summary>
    /// Default plan (no Template set) lands on PlainMonorepo and the
    /// generated content reflects that — not stack-specific.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_UsesPlainTemplate_WhenNoneSpecified()
    {
        var outDir = Directory.CreateTempSubdirectory().FullName;
        var svc = new SessionBuilderService(new StubAiService());

        var plan = new SessionPlan
        {
            ProjectName = "PlainProj",
            Description = "A plain handoff.",
            OutputLocation = outDir,
            // Template intentionally not set — default is PlainMonorepo.
        };

        var root = await svc.GenerateAsync(plan);
        var claudeMd = await File.ReadAllTextAsync(Path.Combine(root, "CLAUDE.md"));

        Assert.DoesNotContain("CommunityToolkit.Mvvm", claudeMd);
        Assert.DoesNotContain("FastAPI", claudeMd);
        Assert.DoesNotContain("Next.js", claudeMd);

        Directory.Delete(outDir, recursive: true);
    }
}
