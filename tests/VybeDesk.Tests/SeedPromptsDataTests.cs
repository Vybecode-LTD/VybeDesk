using VybeDesk.Services.Storage;
using Xunit;

namespace VybeDesk.Tests;

public class SeedPromptsDataTests
{
    [Fact]
    public void All_HasExactly30Prompts()
    {
        Assert.Equal(30, SeedPromptsData.All.Count);
    }

    [Fact]
    public void All_HasFiveCategories()
    {
        var categories = SeedPromptsData.All.Select(p => p.Category).Distinct().ToList();
        Assert.Equal(5, categories.Count);
    }

    [Fact]
    public void All_SixPromptsPerCategory()
    {
        var groups = SeedPromptsData.All.GroupBy(p => p.Category);
        Assert.All(groups, g => Assert.Equal(6, g.Count()));
    }

    [Fact]
    public void All_NoDuplicateTitles()
    {
        var titles = SeedPromptsData.All.Select(p => p.Title).ToList();
        var distinct = titles.Distinct().ToList();
        Assert.Equal(titles.Count, distinct.Count);
    }

    [Fact]
    public void All_EveryPromptHasNonEmptyTitle()
    {
        Assert.All(SeedPromptsData.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Title),
                "Seed prompt has empty title");
        });
    }

    [Fact]
    public void All_EveryPromptHasNonEmptyBody()
    {
        Assert.All(SeedPromptsData.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Body),
                $"Seed prompt '{p.Title}' has empty body");
        });
    }

    [Fact]
    public void All_EveryPromptHasNonEmptyCategory()
    {
        Assert.All(SeedPromptsData.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Category),
                $"Seed prompt '{p.Title}' has empty category");
        });
    }

    [Fact]
    public void All_EveryPromptHasAtLeastOneTag()
    {
        Assert.All(SeedPromptsData.All, p =>
        {
            Assert.True(p.Tags.Count > 0,
                $"Seed prompt '{p.Title}' has no tags");
        });
    }

    [Fact]
    public void All_NoEmptyTags()
    {
        Assert.All(SeedPromptsData.All, p =>
        {
            Assert.All(p.Tags, tag =>
            {
                Assert.False(string.IsNullOrWhiteSpace(tag),
                    $"Seed prompt '{p.Title}' contains an empty tag");
            });
        });
    }

    [Fact]
    public void All_TemplateVariablesAreWellFormed()
    {
        // Every {{ must have a matching }}, and variables should be lowercase/underscored.
        foreach (var prompt in SeedPromptsData.All)
        {
            var body = prompt.Body;
            var title = prompt.Title;

            int openCount = CountOccurrences(body, "{{");
            int closeCount = CountOccurrences(body, "}}");
            Assert.True(openCount == closeCount,
                $"Seed prompt '{title}' body has unbalanced template braces: {openCount} {{ vs {closeCount} }}");

            int titleOpen = CountOccurrences(title, "{{");
            int titleClose = CountOccurrences(title, "}}");
            Assert.True(titleOpen == titleClose,
                $"Seed prompt '{title}' title has unbalanced template braces");
        }
    }

    [Fact]
    public void All_ExpectedCategories()
    {
        var categories = SeedPromptsData.All.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();
        Assert.Contains("Doc & VCS hygiene", categories);
        Assert.Contains("Testing & regression", categories);
        Assert.Contains("Efficient task execution", categories);
        Assert.Contains("New session starters", categories);
        Assert.Contains("Common dev tasks", categories);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
