using ClaudePM.Core.Models;
using ClaudePM.Services.Testing;
using Xunit;

namespace ClaudePM.Tests;

/// <summary>
/// Pure-function strategy selector tests. Each test uses a real catalog
/// instance (no mock) since the strategy logic and the catalog are
/// designed to evolve together — testing them together catches drift.
/// </summary>
public sealed class StrategySelectorTests
{
    private readonly TestingFrameworkCatalog _catalog = new();

    [Fact]
    public void DotNetApi_TouchingDb_RecommendsXUnitWithUnitAndIntegration()
    {
        var rec = StrategySelector.Recommend(new QuestionnaireAnswers
        {
            ProjectKind = "Library",
            Language = "DotNet",
            Criticality = "Important",
            TeamSize = "SmallTeam",
            ExternalSystems = "Heavy",
        }, _catalog);

        Assert.Contains("xUnit", rec.Frameworks);
        Assert.Contains(TestKind.Unit, rec.Kinds);
        Assert.Contains(TestKind.Integration, rec.Kinds);
        // The summary should call out the database-is-integration convention
        // when external systems are flagged.
        Assert.Contains("database", rec.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WebFrontend_CriticalReact_RecommendsVitestRtlAndPlaywright()
    {
        var rec = StrategySelector.Recommend(new QuestionnaireAnswers
        {
            ProjectKind = "WebFrontend",
            Language = "JavaScript",
            Criticality = "Critical",
            TeamSize = "LargerTeam",
            ExternalSystems = "Some",
        }, _catalog);

        Assert.Contains("Vitest", rec.Frameworks);
        Assert.Contains("React Testing Library", rec.Frameworks);
        Assert.Contains("Playwright", rec.Frameworks);
        Assert.Contains(TestKind.Unit, rec.Kinds);
        Assert.Contains(TestKind.Component, rec.Kinds);
        Assert.Contains(TestKind.EndToEnd, rec.Kinds);
    }

    [Fact]
    public void WebFrontend_PersonalStakes_OmitsPlaywright()
    {
        // E2E maintenance overhead isn't worth it for personal projects.
        var rec = StrategySelector.Recommend(new QuestionnaireAnswers
        {
            ProjectKind = "WebFrontend",
            Language = "JavaScript",
            Criticality = "Personal",
            TeamSize = "Solo",
            ExternalSystems = "None",
        }, _catalog);

        Assert.DoesNotContain("Playwright", rec.Frameworks);
        Assert.DoesNotContain(TestKind.EndToEnd, rec.Kinds);
    }

    [Fact]
    public void PersonalSoloPureLogic_IncludesManualChecklist()
    {
        var rec = StrategySelector.Recommend(new QuestionnaireAnswers
        {
            ProjectKind = "CLI",
            Language = "Python",
            Criticality = "Personal",
            TeamSize = "Solo",
            ExternalSystems = "None",
        }, _catalog);

        Assert.Contains(TestKind.ManualChecklist, rec.Kinds);
    }

    [Fact]
    public void NoExternalSystems_OmitsIntegration()
    {
        var rec = StrategySelector.Recommend(new QuestionnaireAnswers
        {
            ProjectKind = "Library",
            Language = "Python",
            Criticality = "Important",
            TeamSize = "Solo",
            ExternalSystems = "None",
        }, _catalog);

        Assert.DoesNotContain(TestKind.Integration, rec.Kinds);
    }

    [Fact]
    public void OtherLanguage_HasNoFrameworkButKindsStillRecommended()
    {
        // The summary explains that the kinds still apply — the user picks
        // the framework for their stack themselves.
        var rec = StrategySelector.Recommend(new QuestionnaireAnswers
        {
            ProjectKind = "Library",
            Language = "Other",
            Criticality = "Important",
            TeamSize = "Solo",
            ExternalSystems = "Some",
        }, _catalog);

        Assert.Empty(rec.Frameworks);
        Assert.Contains(TestKind.Unit, rec.Kinds);
        Assert.Contains(TestKind.Integration, rec.Kinds);
        Assert.Contains("catalog", rec.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Summary_AlwaysIncludesFriendlyLanguagePhrase()
    {
        var rec = StrategySelector.Recommend(new QuestionnaireAnswers
        {
            ProjectKind = "Desktop",
            Language = "Cpp",
            Criticality = "Critical",
            TeamSize = "SmallTeam",
            ExternalSystems = "Some",
        }, _catalog);

        Assert.Contains("C++", rec.Summary);
    }
}
