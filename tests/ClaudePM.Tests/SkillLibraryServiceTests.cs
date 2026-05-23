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
}
