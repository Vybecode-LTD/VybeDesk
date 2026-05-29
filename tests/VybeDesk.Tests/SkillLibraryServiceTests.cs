using VybeDesk.Core.Models;
using VybeDesk.Services.Skills;
using Xunit;

namespace VybeDesk.Tests;

public class SkillLibraryServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SkillLibraryService _svc;

    public SkillLibraryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "vybedesk-test-skills-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _svc = new SkillLibraryService();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private string CreateSkill(string name, string description, string body,
        string? folder = null, string? resourceContent = null)
    {
        folder ??= name;
        var skillDir = Path.Combine(_tempDir, folder);
        Directory.CreateDirectory(skillDir);
        var content = $"---\nname: {name}\ndescription: >-\n  {description}\n---\n\n{body}\n";
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), content);

        if (resourceContent is not null)
            File.WriteAllText(Path.Combine(skillDir, "reference.md"), resourceContent);

        return skillDir;
    }

    // ---- ScanAsync ----

    [Fact]
    public async Task ScanAsync_FindsSkillMdFiles()
    {
        CreateSkill("test-skill", "Use when testing scanning. A description that is long enough.", "# Body\nContent here.");

        var skills = await _svc.ScanAsync(_tempDir);

        var skill = Assert.Single(skills);
        Assert.Equal("test-skill", skill.Name);
        Assert.True(skill.HasFrontMatter);
        Assert.Contains("Body", skill.Body);
    }

    [Fact]
    public async Task ScanAsync_IgnoresNonSkillMdFiles()
    {
        // A regular .md file that is NOT named SKILL.md should be ignored.
        var dir = Path.Combine(_tempDir, "some-folder");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "README.md"), "---\nname: readme\n---\nNot a skill.");

        var skills = await _svc.ScanAsync(_tempDir);
        Assert.Empty(skills);
    }

    [Fact]
    public async Task ScanAsync_CaseInsensitiveFileName()
    {
        var dir = Path.Combine(_tempDir, "my-skill");
        Directory.CreateDirectory(dir);
        // Lowercase "skill.md" should still be found.
        File.WriteAllText(Path.Combine(dir, "skill.md"),
            "---\nname: my-skill\ndescription: >-\n  Use when testing case insensitivity in the scanner.\n---\n\nBody.\n");

        var skills = await _svc.ScanAsync(_tempDir);
        Assert.Single(skills);
    }

    [Fact]
    public async Task ScanAsync_MissingFolder_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _svc.ScanAsync(Path.Combine(_tempDir, "nonexistent")));
    }

    [Fact]
    public async Task ScanAsync_OrdersAlphabetically()
    {
        CreateSkill("zebra-skill", "Use when testing alphabetical ordering of skills.", "Body Z.");
        CreateSkill("alpha-skill", "Use when testing alphabetical ordering of skills.", "Body A.");

        var skills = await _svc.ScanAsync(_tempDir);
        Assert.Equal(2, skills.Count);
        Assert.Equal("alpha-skill", skills[0].Name);
        Assert.Equal("zebra-skill", skills[1].Name);
    }

    // ---- Validate ----

    [Fact]
    public void Validate_ValidSkill_NoFindings()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = true,
            Name = "valid-skill",
            Description = "Use when testing that a well-formed skill passes validation cleanly.",
            Body = "# Instructions\nDo something useful.",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Empty(findings);
    }

    [Fact]
    public void Validate_NoFrontMatter_Critical()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = false,
            Name = "test",
            Description = "Use when testing frontmatter detection for the validator.",
            Body = "Content.",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Contains(findings, f => f.Severity == FindingSeverity.Critical && f.Category == "Frontmatter");
    }

    [Fact]
    public void Validate_EmptyName_Critical()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = true,
            Name = "",
            Description = "Use when testing the empty name validation rule of the validator.",
            Body = "Content.",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Contains(findings, f => f.Severity == FindingSeverity.Critical && f.Category == "Name");
    }

    [Fact]
    public void Validate_UppercaseName_Warning()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = true,
            Name = "BadName",
            Description = "Use when testing that uppercase names are warned about by the validator.",
            Body = "Content.",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Contains(findings, f => f.Severity == FindingSeverity.Warning && f.Category == "Name");
    }

    [Fact]
    public void Validate_ClaudeInName_Warning()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = true,
            Name = "claude-helper",
            Description = "Use when testing the reserved name detection in the validator.",
            Body = "Content.",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Contains(findings, f => f.Category == "Name" && f.Message.Contains("reserved"));
    }

    [Fact]
    public void Validate_ShortDescription_Warning()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = true,
            Name = "short-desc",
            Description = "Too short.",
            Body = "Content.",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Contains(findings, f => f.Severity == FindingSeverity.Warning && f.Category == "Description");
    }

    [Fact]
    public void Validate_EmptyBody_Warning()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = true,
            Name = "no-body",
            Description = "Use when testing the empty body detection in the validator.",
            Body = "",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Contains(findings, f => f.Category == "Body");
    }

    [Fact]
    public void Validate_MissingTriggerGuidance_Info()
    {
        var skill = new SkillFile
        {
            HasFrontMatter = true,
            Name = "no-guidance",
            Description = "A description that is long enough but has no activation phrases at all in its body.",
            Body = "Content.",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.Contains(findings, f => f.Severity == FindingSeverity.Info &&
                                       f.Message.Contains("trigger"));
    }

    [Fact]
    public void Validate_FindingsOrderedBySeverityDescending()
    {
        // Multiple issues of different severity — output should be Critical first.
        var skill = new SkillFile
        {
            HasFrontMatter = false,
            Name = "",
            Description = "",
            Body = "",
            FileName = "SKILL.md",
        };

        var findings = _svc.Validate(skill);
        Assert.True(findings.Count >= 2);
        for (int i = 1; i < findings.Count; i++)
            Assert.True(findings[i - 1].Severity >= findings[i].Severity);
    }

    // ---- FindDuplicates ----

    [Fact]
    public void FindDuplicates_NoDuplicates_Empty()
    {
        var skills = new List<SkillFile>
        {
            new() { Name = "alpha" },
            new() { Name = "beta" },
        };

        Assert.Empty(_svc.FindDuplicates(skills));
    }

    [Fact]
    public void FindDuplicates_CaseInsensitive()
    {
        var skills = new List<SkillFile>
        {
            new() { Name = "my-skill" },
            new() { Name = "MY-SKILL" },
        };

        var dupes = _svc.FindDuplicates(skills);
        Assert.Single(dupes);
        Assert.Equal(FindingSeverity.Critical, dupes[0].Severity);
    }

    // ---- Serialize ----

    [Fact]
    public void Serialize_ProducesFrontmatterAndBody()
    {
        var skill = new SkillFile
        {
            Name = "my-skill",
            Description = "Use when something needs to happen in the workflow.",
            Body = "# Do the thing\nStep 1.\n",
        };

        var result = _svc.Serialize(skill);

        Assert.StartsWith("---\n", result);
        Assert.Contains("name: my-skill\n", result);
        Assert.Contains("description: >-\n", result);
        Assert.Contains("# Do the thing", result);
        Assert.EndsWith("\n", result);
    }

    // ---- PopulateResources ----

    [Fact]
    public void PopulateResources_FindsSiblingFiles()
    {
        CreateSkill("res-test", "Use when testing that PopulateResources picks up sibling files.", "Body.",
            resourceContent: "# Reference\nSome reference data.");

        var skill = new SkillFile
        {
            FullPath = Path.Combine(_tempDir, "res-test", "SKILL.md"),
            Name = "res-test",
        };

        _svc.PopulateResources(skill);

        Assert.Single(skill.Resources);
        Assert.Equal("reference.md", skill.Resources[0].FileName);
        Assert.True(skill.Resources[0].SizeBytes > 0);
    }

    [Fact]
    public void PopulateResources_ExcludesSkillMdItself()
    {
        CreateSkill("self-exclude", "Use when verifying PopulateResources excludes SKILL.md.", "Body.");
        var skill = new SkillFile
        {
            FullPath = Path.Combine(_tempDir, "self-exclude", "SKILL.md"),
            Name = "self-exclude",
        };

        _svc.PopulateResources(skill);

        // Only SKILL.md exists in the folder — resources should be empty.
        Assert.Empty(skill.Resources);
    }

    // ---- ExportAsync ----

    [Fact]
    public async Task ExportAsync_CopiesFolder()
    {
        CreateSkill("exportable", "Use when testing that ExportAsync copies the skill folder.", "Body.");

        var skill = new SkillFile
        {
            FullPath = Path.Combine(_tempDir, "exportable", "SKILL.md"),
            Name = "exportable",
        };

        var exportTarget = Path.Combine(_tempDir, "exports");
        var exportPath = await _svc.ExportAsync(skill, exportTarget);

        Assert.True(Directory.Exists(exportPath));
        Assert.True(File.Exists(Path.Combine(exportPath, "SKILL.md")));
    }

    [Fact]
    public async Task ExportAsync_RefusesOverwrite()
    {
        CreateSkill("no-overwrite", "Use when testing that ExportAsync refuses to overwrite.", "Body.");

        var skill = new SkillFile
        {
            FullPath = Path.Combine(_tempDir, "no-overwrite", "SKILL.md"),
            Name = "no-overwrite",
        };

        var exportTarget = Path.Combine(_tempDir, "exports2");
        await _svc.ExportAsync(skill, exportTarget);

        // Second export to same target should throw.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _svc.ExportAsync(skill, exportTarget));
    }

    // ---- RenameAsync ----

    [Fact]
    public async Task RenameAsync_RenamesFolderAndFrontmatter()
    {
        CreateSkill("old-name", "Use when testing that RenameAsync renames the folder and frontmatter.", "Body.");
        var skill = new SkillFile
        {
            FullPath = Path.Combine(_tempDir, "old-name", "SKILL.md"),
            Name = "old-name",
        };

        await _svc.RenameAsync(skill, "new-name");

        Assert.Equal("new-name", skill.Name);
        Assert.True(File.Exists(skill.FullPath));
        var content = File.ReadAllText(skill.FullPath);
        Assert.Contains("name: new-name", content);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "old-name")));
    }

    [Fact]
    public async Task RenameAsync_InvalidFormat_Throws()
    {
        CreateSkill("rename-me", "Use when testing that RenameAsync rejects invalid formats.", "Body.");
        var skill = new SkillFile
        {
            FullPath = Path.Combine(_tempDir, "rename-me", "SKILL.md"),
            Name = "rename-me",
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _svc.RenameAsync(skill, "BadName"));
    }

    [Fact]
    public async Task RenameAsync_ClaudeInName_Throws()
    {
        CreateSkill("will-fail", "Use when testing that RenameAsync rejects claude in the name.", "Body.");
        var skill = new SkillFile
        {
            FullPath = Path.Combine(_tempDir, "will-fail", "SKILL.md"),
            Name = "will-fail",
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _svc.RenameAsync(skill, "claude-helper"));
    }

    // ---- ReadResourceAsync ----

    [Fact]
    public async Task ReadResourceAsync_ExistingFile_ReturnsContent()
    {
        CreateSkill("readable", "Use when testing ReadResourceAsync returns file content.", "Body.",
            resourceContent: "Reference content here.");

        var resource = new SkillResource
        {
            FullPath = Path.Combine(_tempDir, "readable", "reference.md"),
            FileName = "reference.md",
        };

        var content = await _svc.ReadResourceAsync(resource);
        Assert.Equal("Reference content here.", content);
    }

    [Fact]
    public async Task ReadResourceAsync_MissingFile_ReturnsFallback()
    {
        var resource = new SkillResource
        {
            FullPath = Path.Combine(_tempDir, "nonexistent", "ghost.md"),
            FileName = "ghost.md",
        };

        var result = await _svc.ReadResourceAsync(resource);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task ReadResourceAsync_NullResource_ReturnsEmpty()
    {
        var result = await _svc.ReadResourceAsync(null!);
        Assert.Equal("", result);
    }

    // ---- Parse (via ScanAsync) ----

    [Fact]
    public async Task Parse_NoFrontmatter_SetsHasFrontMatterFalse()
    {
        var dir = Path.Combine(_tempDir, "no-fm");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "Just body, no frontmatter.");

        var skills = await _svc.ScanAsync(_tempDir);
        var skill = Assert.Single(skills);
        Assert.False(skill.HasFrontMatter);
        Assert.Contains("Just body", skill.Body);
    }

    [Fact]
    public async Task Parse_InlineDescription_ParsesCorrectly()
    {
        var dir = Path.Combine(_tempDir, "inline-desc");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"),
            "---\nname: inline-desc\ndescription: Use when a simple inline description is given.\n---\n\nBody.\n");

        var skills = await _svc.ScanAsync(_tempDir);
        var skill = Assert.Single(skills);
        Assert.Equal("inline-desc", skill.Name);
        Assert.Equal("Use when a simple inline description is given.", skill.Description);
    }

    [Fact]
    public async Task Parse_QuotedName_UnquotesCorrectly()
    {
        var dir = Path.Combine(_tempDir, "quoted");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"),
            "---\nname: \"quoted-name\"\ndescription: >-\n  Use when testing that quoted names are unquoted.\n---\n\nBody.\n");

        var skills = await _svc.ScanAsync(_tempDir);
        Assert.Equal("quoted-name", Assert.Single(skills).Name);
    }
}
