using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using VybeDesk.Services.Skills;
using NSubstitute;
using Xunit;

namespace VybeDesk.Tests;

/// <summary>
/// SkillBuilderService tests. The three tests below cover the spec's
/// hard requirements:
/// <list type="bullet">
/// <item>A draft that violates a validation rule is reported by the
/// SHARED validation (delegated to <see cref="ISkillLibraryService.Validate"/>).</item>
/// <item><see cref="ISkillBuilderService.EmitAsync"/> produces BOTH the
/// flat <c>.skill</c> file AND the <c>&lt;name&gt;/SKILL.md</c> folder
/// form.</item>
/// <item>A SkillFile produced by the builder passes the Skill Library's
/// validation IDENTICALLY — proving the shared-validation requirement
/// actually holds at runtime, not just on paper.</item>
/// </list>
/// The AI dependency is substituted with a no-op since none of these
/// tests exercise the live AI path (they construct SkillFile values
/// directly, which is what the EmitAsync / Validate methods consume).
/// </summary>
public sealed class SkillBuilderServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly ISkillLibraryService _library;
    private readonly SkillBuilderService _builder;

    public SkillBuilderServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(),
            "vybedesk-tests-builder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        // Real library service (its Validate + Serialize are what the
        // builder delegates to — that's the whole point of the test).
        _library = new SkillLibraryService();

        // No-op AI — these tests never call DraftAsync or
        // GenerateClarifyingQuestionsAsync.
        var ai = Substitute.For<IAiService>();
        _builder = new SkillBuilderService(ai, _library);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public void Validate_ReportsRuleViolations_ViaSharedLibraryValidation()
    {
        // A skill with a name containing "claude" (reserved) AND no
        // description should fail at least two rules from the library's
        // validator. We don't assert the exact count — we assert that the
        // builder surfaces the SAME findings the library does.
        var skill = new SkillFile
        {
            Name = "my-claude-helper",        // 'claude' is reserved → Warning
            Description = "",                  // empty → Critical
            Body = "Some body content.",
            HasFrontMatter = true,
        };

        var fromBuilder = _builder.Validate(skill);
        var fromLibrary = _library.Validate(skill);

        // Builder findings are identical to library findings — proving the
        // delegation actually holds.
        Assert.Equal(fromLibrary.Count, fromBuilder.Count);
        for (int i = 0; i < fromLibrary.Count; i++)
            Assert.Equal(fromLibrary[i], fromBuilder[i]);

        // Sanity: at least one Critical and one Warning came back — the
        // bad-skill setup should fire at least those two classes.
        Assert.Contains(fromBuilder, f => f.Severity == FindingSeverity.Critical);
        Assert.Contains(fromBuilder, f => f.Severity == FindingSeverity.Warning);
    }

    [Fact]
    public async Task EmitAsync_ProducesBothFlatFileAndFolderForm()
    {
        var skill = WellFormedSkill();

        var result = await _builder.EmitAsync(skill, _tempRoot);

        Assert.True(File.Exists(result.FlatFilePath),
            "Flat .skill file was not written.");
        Assert.True(Directory.Exists(result.FolderPath),
            "Folder form was not written.");
        var folderSkill = Path.Combine(result.FolderPath, "SKILL.md");
        Assert.True(File.Exists(folderSkill),
            "Folder form is missing its SKILL.md file.");

        // Both forms must contain byte-identical text.
        var flatText = await File.ReadAllTextAsync(result.FlatFilePath);
        var folderText = await File.ReadAllTextAsync(folderSkill);
        Assert.Equal(flatText, folderText);
    }

    [Fact]
    public async Task EmittedFolderForm_PassesLibraryScan_AndValidatesIdentically()
    {
        // Emit a well-formed skill, then have the SKILL LIBRARY scan the
        // emitted folder and validate the resulting SkillFile. The library's
        // findings on the scanned skill must match the builder's findings on
        // the in-memory draft — proving that what comes out of the builder
        // IS a skill the library understands the same way.
        var draft = WellFormedSkill();
        var result = await _builder.EmitAsync(draft, _tempRoot);

        var scanned = await _library.ScanAsync(_tempRoot);
        var fromScan = Assert.Single(scanned);
        Assert.Equal(draft.Name, fromScan.Name);
        Assert.Equal(draft.Description, fromScan.Description);

        var libraryFindings = _library.Validate(fromScan);
        var builderFindings = _builder.Validate(draft);

        // Same number, same severities, same categories — identical pipeline.
        Assert.Equal(libraryFindings.Count, builderFindings.Count);
        for (int i = 0; i < libraryFindings.Count; i++)
        {
            Assert.Equal(libraryFindings[i].Severity, builderFindings[i].Severity);
            Assert.Equal(libraryFindings[i].Category, builderFindings[i].Category);
        }
    }

    [Fact]
    public async Task EmitAsync_RefusesToOverwriteExistingTarget()
    {
        var skill = WellFormedSkill();
        await _builder.EmitAsync(skill, _tempRoot);

        // Second emit to the same target with the same skill must fail —
        // overwriting an existing folder or file silently is the kind of
        // surprise this app deliberately avoids.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _builder.EmitAsync(skill, _tempRoot));
    }

    /// <summary>
    /// A skill that passes every library validation rule cleanly — name is
    /// hyphenated, description is in-budget and contains an explicit "use
    /// when" trigger phrase, body is non-empty.
    /// </summary>
    private static SkillFile WellFormedSkill() => new()
    {
        Name = "demo-skill",
        Description =
            "A demo skill used by the test suite. Use when running unit tests " +
            "for the skill builder to verify the emit and validation pipeline " +
            "is wired correctly. Triggers: test, demo, skill-builder.",
        Body =
            "# Demo skill\n\n" +
            "This is a placeholder body. Imperative voice would normally lead " +
            "with the core principle.\n\n" +
            "## Anti-patterns\n\n" +
            "- Treating the demo body as a real instruction set.\n",
        HasFrontMatter = true,
    };
}
