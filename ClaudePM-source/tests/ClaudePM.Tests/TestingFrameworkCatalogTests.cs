using ClaudePM.Core.Models;
using ClaudePM.Services.Testing;
using Xunit;

namespace ClaudePM.Tests;

/// <summary>
/// Built-in framework catalog tests. Validates the spec's structural
/// invariants (every entry has required fields), the seven seed entries
/// the spec calls out by name, and the language-lookup behaviour the VM
/// relies on.
/// </summary>
public sealed class TestingFrameworkCatalogTests
{
    private readonly TestingFrameworkCatalog _catalog = new();

    [Fact]
    public void All_ContainsTheSevenSeedFrameworks()
    {
        var names = _catalog.All.Select(f => f.Name).ToList();
        Assert.Contains("xUnit", names);
        Assert.Contains("GoogleTest", names);
        Assert.Contains("pytest", names);
        Assert.Contains("Vitest", names);
        Assert.Contains("Jest", names);
        Assert.Contains("React Testing Library", names);
        Assert.Contains("Playwright", names);
    }

    [Fact]
    public void EveryEntry_HasNonEmptyNameLanguageAndSetupPromptTemplate()
    {
        foreach (var f in _catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Name),
                $"Framework with empty Name: {f}");
            Assert.False(string.IsNullOrWhiteSpace(f.Language),
                $"Framework {f.Name} has empty Language");
            Assert.False(string.IsNullOrWhiteSpace(f.SetupPromptTemplate),
                $"Framework {f.Name} has empty SetupPromptTemplate");
            Assert.NotEmpty(f.Kinds);
        }
    }

    [Fact]
    public void ForLanguage_DotNet_ReturnsXUnitAndAnyCrossLanguageTools()
    {
        var entries = _catalog.ForLanguage("DotNet");
        Assert.Contains(entries, f => f.Name == "xUnit");
        // Playwright is Language="Any" and should surface for every stack.
        Assert.Contains(entries, f => f.Name == "Playwright");
    }

    [Fact]
    public void ForLanguage_JavaScript_ReturnsVitestJestRtlAndPlaywright()
    {
        var entries = _catalog.ForLanguage("JavaScript");
        Assert.Contains(entries, f => f.Name == "Vitest");
        Assert.Contains(entries, f => f.Name == "Jest");
        Assert.Contains(entries, f => f.Name == "React Testing Library");
        Assert.Contains(entries, f => f.Name == "Playwright");
    }

    [Fact]
    public void ForLanguage_UnknownToken_StillReturnsCrossLanguageEntries()
    {
        // "Other" maps to no language-specific framework, but Playwright
        // (Any) should still be available for any frontend stack.
        var entries = _catalog.ForLanguage("Other");
        Assert.All(entries, f => Assert.Equal("Any", f.Language));
    }

    [Fact]
    public void ByName_ReturnsRequestedEntryOrNull()
    {
        Assert.NotNull(_catalog.ByName("xUnit"));
        Assert.Null(_catalog.ByName("xUnit.net5")); // doesn't exist
    }

    [Fact]
    public void DatabaseTesting_IsNotARepresentedAsSeparateFramework()
    {
        // The spec is explicit: database testing belongs INSIDE the
        // language's unit/integration framework. Catch any future drift.
        var names = _catalog.All.Select(f => f.Name);
        Assert.DoesNotContain(names,
            n => n.Contains("Database", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names,
            n => n.Contains("DbUnit", StringComparison.OrdinalIgnoreCase));
    }
}
