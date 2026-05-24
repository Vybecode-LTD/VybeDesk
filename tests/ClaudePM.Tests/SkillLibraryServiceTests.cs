using ClaudePM.Core.Models;
using ClaudePM.Services.Skills;
using Xunit;

namespace ClaudePM.Tests;

public class SkillLibraryServiceTests
{
    [Fact]
    public async Task ScanAsync_ParsesFrontMatter()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var content =
            "---\n" +
            "name: my-test-skill\n" +
            "description: >-\n" +
            "  Use this when testing the parser.\n" +
            "---\n\n" +
            "# Body\nHello.\n";
        await File.WriteAllTextAsync(Path.Combine(dir, "my-test-skill.skill"), content);

        var svc = new SkillLibraryService();
        var skills = await svc.ScanAsync(dir);

        Assert.Single(skills);
        Assert.Equal("my-test-skill", skills[0].Name);
        Assert.Contains("testing the parser", skills[0].Description);
        Assert.True(skills[0].HasFrontMatter);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Validate_FlagsMissingNameAndDescription()
    {
        var svc = new SkillLibraryService();
        var skill = new SkillFile { HasFrontMatter = true, Name = "", Description = "", Body = "x" };

        var issues = svc.Validate(skill);

        Assert.Contains(issues, f => f.Category == "Name" && f.Severity == FindingSeverity.Critical);
        Assert.Contains(issues, f => f.Category == "Description" && f.Severity == FindingSeverity.Critical);
    }

    [Fact]
    public void FindDuplicates_DetectsSharedNames()
    {
        var svc = new SkillLibraryService();
        var skills = new List<SkillFile>
        {
            new() { Name = "dup" },
            new() { Name = "dup" },
            new() { Name = "unique" },
        };

        var dups = svc.FindDuplicates(skills);

        Assert.Single(dups);
        Assert.Equal("dup", dups[0].File);
    }

    [Fact]
    public async Task ScanAsync_FindsFolderFormatSkillMd()
    {
        // Claude Code uses ~/.claude/skills/<name>/SKILL.md, not flat *.skill.
        // The scanner must find both.
        var dir = Directory.CreateTempSubdirectory().FullName;
        var skillFolder = Path.Combine(dir, "my-folder-skill");
        Directory.CreateDirectory(skillFolder);
        var content =
            "---\n" +
            "name: my-folder-skill\n" +
            "description: >-\n" +
            "  Use this when a SKILL.md inside a folder needs to be found.\n" +
            "---\n\n" +
            "# Body\nFolder format.\n";
        await File.WriteAllTextAsync(Path.Combine(skillFolder, "SKILL.md"), content);

        var svc = new SkillLibraryService();
        var skills = await svc.ScanAsync(dir);

        Assert.Single(skills);
        Assert.Equal("my-folder-skill", skills[0].Name);
        Assert.Equal("my-folder-skill/SKILL.md", skills[0].FileName);
        Assert.True(skills[0].HasFrontMatter);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task ScanAsync_FindsBothFormatsInSameTree()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var flatContent =
            "---\nname: flat-one\ndescription: >-\n  Use this for flat.\n---\n\nbody\n";
        var folderContent =
            "---\nname: folder-one\ndescription: >-\n  Use this for folder.\n---\n\nbody\n";
        await File.WriteAllTextAsync(Path.Combine(dir, "flat-one.skill"), flatContent);
        var skillFolder = Path.Combine(dir, "folder-one");
        Directory.CreateDirectory(skillFolder);
        await File.WriteAllTextAsync(Path.Combine(skillFolder, "SKILL.md"), folderContent);

        var svc = new SkillLibraryService();
        var skills = await svc.ScanAsync(dir);

        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, s => s.Name == "flat-one");
        Assert.Contains(skills, s => s.Name == "folder-one");

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task ExportAsync_WritesBothFlatAndFolderFormats()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var skill = new SkillFile
        {
            Name = "exported-skill",
            Description = "Use this for testing the dual-format export.",
            Body = "# Body\nstuff\n",
            HasFrontMatter = true,
        };

        var svc = new SkillLibraryService();
        var result = await svc.ExportAsync(skill, dir);

        var flatPath = Path.Combine(dir, "exported-skill.skill");
        var skillMdPath = Path.Combine(dir, "exported-skill", "SKILL.md");
        Assert.True(File.Exists(flatPath), "Flat *.skill file should exist");
        Assert.True(File.Exists(skillMdPath), "Folder SKILL.md file should exist");
        Assert.Contains(flatPath, result);
        Assert.Contains(skillMdPath, result);

        // Re-scan and verify both formats are picked up, parsed correctly.
        var found = await svc.ScanAsync(dir);
        Assert.Equal(2, found.Count);
        Assert.All(found, s => Assert.Equal("exported-skill", s.Name));

        Directory.Delete(dir, recursive: true);
    }
}
