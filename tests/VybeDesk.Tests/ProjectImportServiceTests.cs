using System.Text;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Import;
using NSubstitute;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// Tests for the M4 #14 import flow. Each test uses an isolated temp
/// folder under <see cref="Path.GetTempPath"/> and substitutes the
/// project/prompt stores so the assertions stay focused on the import
/// service's own behaviour.
///
/// Git-timestamp resolution is environment-dependent (it shells out
/// to <c>git</c> and walks up the tree for <c>.git</c>) so tests do
/// not assert on <see cref="ProjectImportResult.HadGitTimestamp"/>.
/// Either branch — git success or mtime fallback — is acceptable; the
/// tests focus on the other observable effects of import.
/// </summary>
public sealed class ProjectImportServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly List<Project> _addedProjects = new();
    private readonly List<PromptEntry> _addedPrompts = new();
    private readonly IProjectStore _projects;
    private readonly IPromptStore _prompts;
    private readonly ProjectImportService _service;

    public ProjectImportServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _projects = Substitute.For<IProjectStore>();
        _projects.AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _addedProjects.Add(ci.Arg<Project>());
                return Task.CompletedTask;
            });

        _prompts = Substitute.For<IPromptStore>();
        _prompts.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IReadOnlyList<PromptEntry>>(
                _addedPrompts.ToList()));
        _prompts.AddAsync(Arg.Any<PromptEntry>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _addedPrompts.Add(ci.Arg<PromptEntry>());
                return Task.CompletedTask;
            });

        _service = new ProjectImportService(_projects, _prompts);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task ReturnsFailure_WhenFolderDoesNotExist()
    {
        var bogusPath = Path.Combine(_tempRoot, "does-not-exist-" + Guid.NewGuid());

        var result = await _service.ImportFromFolderAsync(bogusPath);

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.Equal(0, result.PromptsImported);
        Assert.Equal(0, result.PromptsSkippedDuplicate);
        Assert.False(result.HadGitTimestamp);
        Assert.False(result.HadClaudeMd);
        Assert.Contains("doesn't exist", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_addedProjects);
        Assert.Empty(_addedPrompts);
    }

    [Fact]
    public async Task BareFolder_NoClaudeMdNoCommands_CreatesProjectWithEmptyDescription()
    {
        var folder = NewSubdir("solo");

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.NotNull(result.Project);
        Assert.Equal("", result.Project!.Description);
        Assert.False(result.HadClaudeMd);
        // HadGitTimestamp is intentionally NOT asserted — Path.GetTempPath()
        // is normally outside any git repo, but on systems where it isn't
        // (e.g. a CI workspace under a git checkout) `git log -1` will walk
        // up and find a parent .git. Either branch (git success or mtime
        // fallback) leaves Success=true and Project populated correctly,
        // which is what this test cares about.
        Assert.Equal(0, result.PromptsImported);
        Assert.Equal(0, result.PromptsSkippedDuplicate);
        Assert.NotEqual(default, result.Project.LastActivity);

        Assert.Single(_addedProjects);
        Assert.Empty(_addedPrompts);
    }

    [Fact]
    public async Task PicksUpClaudeMd_AsDescription_Verbatim()
    {
        var folder = NewSubdir("with-claude");
        var body =
            "# My project\n\n" +
            "This is the description that should land in Project.Description verbatim.\n";
        await File.WriteAllTextAsync(Path.Combine(folder, "CLAUDE.md"), body, Encoding.UTF8);

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.True(result.HadClaudeMd);
        Assert.Equal(body, result.Project!.Description);
    }

    [Fact]
    public async Task TruncatesClaudeMd_PastMaxDescriptionChars_WithMarker()
    {
        var folder = NewSubdir("with-huge-claude");
        var huge = new string('x', ProjectImportService.MaxDescriptionChars + 500);
        await File.WriteAllTextAsync(Path.Combine(folder, "CLAUDE.md"), huge, Encoding.UTF8);

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.True(result.HadClaudeMd);
        var desc = result.Project!.Description;
        Assert.True(desc.Length < huge.Length, "Description must be truncated.");
        Assert.Contains("truncated", desc, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(new string('x', 100), desc); // sanity: leading content preserved
    }

    [Fact]
    public async Task PicksUpCommandsAsPromptEntries_WithProjectTagAndTitleCase()
    {
        var folder = NewSubdir("with-commands");
        var commandsDir = Path.Combine(folder, ".claude", "commands");
        Directory.CreateDirectory(commandsDir);

        await File.WriteAllTextAsync(
            Path.Combine(commandsDir, "review-pr.md"),
            "Review the PR carefully.",
            Encoding.UTF8);
        await File.WriteAllTextAsync(
            Path.Combine(commandsDir, "fix_bug.md"),
            "Investigate and fix the bug.",
            Encoding.UTF8);

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Equal(2, result.PromptsImported);
        Assert.Equal(0, result.PromptsSkippedDuplicate);
        Assert.Equal(2, _addedPrompts.Count);

        var projectTag = result.Project!.Name.ToLowerInvariant();

        var reviewPrompt = _addedPrompts.Single(p => p.Title == "Review Pr");
        Assert.Equal("Review the PR carefully.", reviewPrompt.Body);
        Assert.Equal("Imported", reviewPrompt.Category);
        Assert.Contains(projectTag, reviewPrompt.Tags);

        var fixPrompt = _addedPrompts.Single(p => p.Title == "Fix Bug");
        Assert.Equal("Investigate and fix the bug.", fixPrompt.Body);
        Assert.Equal("Imported", fixPrompt.Category);
        Assert.Contains(projectTag, fixPrompt.Tags);
    }

    [Fact]
    public async Task SkipsDuplicatePrompts_WhenTitleAndProjectTagAlreadyPresent()
    {
        // Use an explicit folder name so the projectTag derivation is
        // predictable. NewSubdir appends a guid suffix, which would make the
        // tag hard to compute up-front.
        var folderName = "dup-test-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var folder = Path.Combine(_tempRoot, folderName);
        Directory.CreateDirectory(folder);
        var projectTag = folderName.ToLowerInvariant();

        // Pre-existing prompt with the SAME title that would be derived from
        // "review.md" (=> "Review") AND tagged with the project name.
        _addedPrompts.Add(new PromptEntry
        {
            Title = "Review",
            Body = "Older copy.",
            Category = "Imported",
            Tags = new List<string> { projectTag },
        });

        var commandsDir = Path.Combine(folder, ".claude", "commands");
        Directory.CreateDirectory(commandsDir);
        await File.WriteAllTextAsync(
            Path.Combine(commandsDir, "review.md"),
            "Newer copy.",
            Encoding.UTF8);

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Equal(0, result.PromptsImported);
        Assert.Equal(1, result.PromptsSkippedDuplicate);
        // The pre-existing prompt is still the only one; we did NOT add a duplicate.
        Assert.Single(_addedPrompts);
        Assert.Equal("Older copy.", _addedPrompts[0].Body);
    }

    [Fact]
    public async Task WalksCommandsDirectory_Recursively()
    {
        var folder = NewSubdir("recursive");
        var nested = Path.Combine(folder, ".claude", "commands", "qa");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(
            Path.Combine(nested, "smoke-test.md"),
            "Run smoke tests.",
            Encoding.UTF8);

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Equal(1, result.PromptsImported);
        var prompt = Assert.Single(_addedPrompts);
        Assert.Equal("Smoke Test", prompt.Title);
        Assert.Equal("Run smoke tests.", prompt.Body);
    }

    [Fact]
    public async Task AutoDetectsLogo_FromFaviconIco()
    {
        // favicon.ico sits at the top of the candidate priority list — even
        // a competing logo.png should lose to it.
        var folder = NewSubdir("with-favicon");
        var favicon = Path.Combine(folder, "favicon.ico");
        var loser   = Path.Combine(folder, "logo.png");
        await File.WriteAllBytesAsync(favicon, new byte[] { 0x00, 0x00, 0x01, 0x00 });
        await File.WriteAllBytesAsync(loser,   new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Equal(favicon, result.Project!.LogoPath);
    }

    [Fact]
    public async Task AutoDetectsLogo_FromLogoPng_WhenNoFavicon()
    {
        var folder = NewSubdir("with-logo-png");
        var logo = Path.Combine(folder, "logo.png");
        await File.WriteAllBytesAsync(logo, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Equal(logo, result.Project!.LogoPath);
    }

    [Fact]
    public async Task AutoDetectsLogo_FromGlobMatch()
    {
        // None of the prioritised filenames are present, but a *logo*.png
        // exists at the root — the glob fallback should pick it up.
        var folder = NewSubdir("with-glob-logo");
        var globLogo = Path.Combine(folder, "mylogo.png");
        await File.WriteAllBytesAsync(globLogo, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Equal(globLogo, result.Project!.LogoPath);
    }

    [Fact]
    public async Task NoLogo_WhenNoCandidates_LeavesLogoPathNull()
    {
        // No favicon, no logo, no icon, no glob match — Project.LogoPath
        // stays null so the Home card falls back to the project glyph.
        var folder = NewSubdir("bare");

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Null(result.Project!.LogoPath);
    }

    [Fact]
    public async Task DerivesProjectName_FromFolderName()
    {
        // Use an explicit path (skip NewSubdir's guid suffix) so the assertion
        // can match the folder name exactly.
        var folderName = "my-cool-project-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var folder = Path.Combine(_tempRoot, folderName);
        Directory.CreateDirectory(folder);

        var result = await _service.ImportFromFolderAsync(folder);

        Assert.True(result.Success);
        Assert.Equal(folderName, result.Project!.Name);
    }

    private string NewSubdir(string name)
    {
        // Append a guid so repeated test runs don't collide if the cleanup
        // races a previous run's teardown.
        var path = Path.Combine(_tempRoot, name + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(path);
        return path;
    }
}
