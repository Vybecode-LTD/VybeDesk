using VybeDesk.Core.Models;
using VybeDesk.Services.Session;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// Covers <see cref="SessionTemplates.For"/> — proves each
/// <see cref="SessionTemplate"/> value returns plausible, stack-specific
/// content for all four canonical files. Spot-checks per template against
/// at least one stack-unique marker so future template edits can't
/// accidentally swap one stack's content for another's.
/// </summary>
public class SessionTemplatesTests
{
    private const string Name  = "Sample";
    private const string Desc  = "A sample handoff package.";
    private const string Stack = "Some stack";

    [Fact]
    public void For_ReturnsAvaloniaContent_WhenTemplateIsAvaloniaDotNet()
    {
        var (claudeMd, readme, gitignore, kickoff) =
            SessionTemplates.For(SessionTemplate.AvaloniaDotNet, Name, Desc, Stack);

        Assert.Contains("CommunityToolkit.Mvvm", claudeMd);
        Assert.Contains("compiled bindings", claudeMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Avalonia", readme);
        Assert.Contains("dotnet run --project src/" + Name + ".App", readme);
        Assert.Contains("bin/", gitignore);
        Assert.Contains("obj/", gitignore);
        Assert.Contains("dotnet new sln", kickoff);
        Assert.Contains("Avalonia", kickoff);
    }

    [Fact]
    public void For_ReturnsFastApiContent_WhenTemplateIsFastApiPython()
    {
        var (claudeMd, readme, gitignore, kickoff) =
            SessionTemplates.For(SessionTemplate.FastApiPython, Name, Desc, Stack);

        Assert.Contains("FastAPI", claudeMd);
        Assert.Contains("Pydantic", claudeMd);
        Assert.Contains("Depends(", claudeMd);
        Assert.Contains("uvicorn", readme);
        Assert.Contains("pytest", readme);
        Assert.Contains("__pycache__/", gitignore);
        Assert.Contains(".venv/", gitignore);
        Assert.Contains("FastAPI", kickoff);
        Assert.Contains("app/routers", kickoff);
    }

    [Fact]
    public void For_ReturnsNextJsContent_WhenTemplateIsNextJsTypeScript()
    {
        var (claudeMd, readme, gitignore, kickoff) =
            SessionTemplates.For(SessionTemplate.NextJsTypeScript, Name, Desc, Stack);

        Assert.Contains("Next.js", claudeMd);
        Assert.Contains("App Router", claudeMd);
        Assert.Contains("TypeScript", claudeMd);
        Assert.Contains("npm install", readme);
        Assert.Contains("next.config.js", readme);
        Assert.Contains("node_modules/", gitignore);
        Assert.Contains(".next/", gitignore);
        Assert.Contains("create-next-app", kickoff);
    }

    [Fact]
    public void For_ReturnsPythonCliContent_WhenTemplateIsPythonCli()
    {
        var (claudeMd, readme, gitignore, kickoff) =
            SessionTemplates.For(SessionTemplate.PythonCli, Name, Desc, Stack);

        Assert.Contains("CLI", claudeMd);
        Assert.Contains("pyproject.toml", claudeMd);
        Assert.Contains("argparse", claudeMd);
        Assert.Contains("pip install -e", readme);
        Assert.Contains("__pycache__/", gitignore);
        Assert.Contains(".venv/", gitignore);
        Assert.Contains("pyproject.toml", kickoff);
        Assert.Contains("[project.scripts]", kickoff);
    }

    [Fact]
    public void For_ReturnsPlainContent_WhenTemplateIsPlainMonorepo()
    {
        var (claudeMd, readme, gitignore, kickoff) =
            SessionTemplates.For(SessionTemplate.PlainMonorepo, Name, Desc, Stack);

        Assert.Contains("# CLAUDE.md — " + Name, claudeMd);
        Assert.Contains("Conventions", claudeMd);
        Assert.Contains("Build & Run", readme);
        Assert.Contains(".DS_Store", gitignore);
        Assert.Contains("Kickoff — " + Name, kickoff);

        // The plain template should NOT leak stack-specific markers.
        Assert.DoesNotContain("CommunityToolkit.Mvvm", claudeMd);
        Assert.DoesNotContain("FastAPI", claudeMd);
        Assert.DoesNotContain("Next.js", claudeMd);
    }

    [Fact]
    public void For_SubstitutesProjectName_InClaudeMd()
    {
        const string custom = "CustomProj";
        foreach (var tpl in Enum.GetValues<SessionTemplate>())
        {
            var (claudeMd, readme, _, kickoff) =
                SessionTemplates.For(tpl, custom, Desc, Stack);

            Assert.Contains(custom, claudeMd);
            Assert.Contains(custom, readme);
            Assert.Contains(custom, kickoff);
        }
    }

    [Fact]
    public void For_AlwaysReturnsNonEmptyContent()
    {
        foreach (var tpl in Enum.GetValues<SessionTemplate>())
        {
            var (claudeMd, readme, gitignore, kickoff) =
                SessionTemplates.For(tpl, Name, Desc, Stack);

            Assert.False(string.IsNullOrWhiteSpace(claudeMd),
                "CLAUDE.md is empty for " + tpl);
            Assert.False(string.IsNullOrWhiteSpace(readme),
                "README.md is empty for " + tpl);
            Assert.False(string.IsNullOrWhiteSpace(gitignore),
                ".gitignore is empty for " + tpl);
            Assert.False(string.IsNullOrWhiteSpace(kickoff),
                "KICKOFF.md is empty for " + tpl);
        }
    }
}
