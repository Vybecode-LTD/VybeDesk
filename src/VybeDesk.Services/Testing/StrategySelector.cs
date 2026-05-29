using System.Text;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Services.Testing;

/// <summary>
/// Pure-function recommendation engine: maps a completed
/// <see cref="QuestionnaireAnswers"/> set to a draft strategy (summary
/// prose + which <see cref="TestKind"/>s the strategy calls for + which
/// framework names from the catalog to recommend).
///
/// Database testing is folded into <see cref="TestKind.Integration"/> by
/// design — there is no "database test framework" in the catalog and the
/// summary prose says so explicitly when external systems are flagged.
/// Coverage numbers are NOT chased; the test-strategy-selection skill
/// warns against that as an anti-pattern.
/// </summary>
public static class StrategySelector
{
    public static StrategyRecommendation Recommend(
        QuestionnaireAnswers a, ITestingFrameworkCatalog catalog)
    {
        var kinds = new List<TestKind>();
        var frameworks = new List<string>();

        // 1. Unit testing is the spine. Every strategy starts with the
        //    project's language-native unit framework.
        kinds.Add(TestKind.Unit);
        var primaryFramework = PrimaryUnitFramework(a.Language);
        if (primaryFramework is not null && catalog.ByName(primaryFramework) is not null)
            frameworks.Add(primaryFramework);

        // 2. Integration tests when the project touches external systems
        //    (DBs, APIs, the file system, the network). Same framework as
        //    units — database testing belongs here, not as its own thing.
        var hasExternal = a.ExternalSystems is "Heavy" or "Some";
        if (hasExternal && !kinds.Contains(TestKind.Integration))
            kinds.Add(TestKind.Integration);

        // 3. Component tests for React-flavoured web frontends. RTL needs a
        //    runner — the JS unit framework above (Vitest) already covers
        //    that, so the user gets a coherent pair.
        var isWebFrontend = a.ProjectKind is "WebFrontend";
        var isJavaScriptStack = a.Language is "JavaScript";
        if (isWebFrontend && isJavaScriptStack)
        {
            kinds.Add(TestKind.Component);
            if (catalog.ByName("React Testing Library") is not null)
                frameworks.Add("React Testing Library");
        }

        // 4. End-to-end testing when it's a web frontend AND correctness
        //    actually matters. E2E is the slowest, most-valuable kind — not
        //    worth the maintenance overhead for personal projects.
        var matters = a.Criticality is "Critical" or "Important";
        if (isWebFrontend && matters)
        {
            kinds.Add(TestKind.EndToEnd);
            if (catalog.ByName("Playwright") is not null)
                frameworks.Add("Playwright");
        }

        // 5. Manual checklist for personal solo low-stakes projects — be
        //    honest that ad-hoc click-around is what most people will
        //    actually do, and make it a deliberate practice rather than a
        //    pretense of full automated coverage.
        var lowStakes = a.Criticality is "Personal" && a.TeamSize is "Solo";
        if (lowStakes && !hasExternal)
            kinds.Add(TestKind.ManualChecklist);

        var summary = BuildSummary(a, kinds, frameworks, hasExternal, matters, lowStakes);

        return new StrategyRecommendation(
            summary,
            frameworks,
            kinds);
    }

    private static string? PrimaryUnitFramework(string languageToken) => languageToken switch
    {
        "DotNet"     => "xUnit",
        "Python"     => "pytest",
        "JavaScript" => "Vitest",
        "Cpp"        => "GoogleTest",
        _            => null,  // "Other" — recommendation explains in prose.
    };

    private static string BuildSummary(
        QuestionnaireAnswers a, IReadOnlyList<TestKind> kinds,
        IReadOnlyList<string> frameworks,
        bool hasExternal, bool matters, bool lowStakes)
    {
        var sb = new StringBuilder();

        sb.Append("You're building a ").Append(FriendlyKind(a.ProjectKind))
          .Append(" in ").Append(FriendlyLanguage(a.Language)).Append(", with ")
          .Append(FriendlyCriticality(a.Criticality)).Append(" stakes ")
          .Append(FriendlyTeam(a.TeamSize)).Append(", touching external systems ")
          .Append(FriendlyExternal(a.ExternalSystems)).Append('.').AppendLine();
        sb.AppendLine();

        sb.AppendLine("**Recommended kinds of testing:**");
        foreach (var k in kinds)
            sb.Append("- ").AppendLine(KindRationale(k, hasExternal, matters, lowStakes));
        sb.AppendLine();

        if (frameworks.Count > 0)
        {
            sb.AppendLine("**Frameworks to use:** " + string.Join(", ", frameworks));
            sb.AppendLine();
        }
        else if (a.Language is "Other")
        {
            sb.AppendLine("Your language isn't in the built-in catalog. The kinds above " +
                          "still apply — pick the standard unit test framework for your " +
                          "language and use it for both unit and integration tests.");
            sb.AppendLine();
        }

        if (hasExternal)
        {
            sb.AppendLine(
                "**Note on databases:** database testing isn't its own framework. " +
                "It's integration testing inside whichever unit framework you're " +
                "already using — a temp SQLite file or an in-memory connection per " +
                "test. Don't go looking for a separate \"database test framework\".");
            sb.AppendLine();
        }

        if (lowStakes)
        {
            sb.AppendLine(
                "**Personal project, you alone, low stakes:** keep this lightweight. " +
                "A small manual checklist of \"things to click through before I ship\" " +
                "is a real practice, not a cop-out — and it's better than chasing a " +
                "100% test-coverage number, which is an anti-pattern anyway.");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string FriendlyKind(string projectKind) => projectKind switch
    {
        "Library"       => "library or API",
        "Desktop"       => "desktop application",
        "WebFrontend"   => "web frontend",
        "CLI"           => "command-line tool",
        "Mixed"         => "mixed-scope project",
        _               => "project",
    };

    private static string FriendlyLanguage(string lang) => lang switch
    {
        "DotNet"     => ".NET (C#)",
        "Python"     => "Python",
        "JavaScript" => "JavaScript / TypeScript",
        "Cpp"        => "C++",
        _            => "an unlisted language",
    };

    private static string FriendlyCriticality(string c) => c switch
    {
        "Critical"  => "high",
        "Important" => "real but not safety-critical",
        "Personal"  => "low",
        _           => "unspecified",
    };

    private static string FriendlyTeam(string t) => t switch
    {
        "Solo"        => "working alone",
        "SmallTeam"   => "with a small team",
        "LargerTeam"  => "with a larger team",
        _             => "",
    };

    private static string FriendlyExternal(string e) => e switch
    {
        "Heavy" => "heavily",
        "Some"  => "in places",
        "None"  => "not at all (pure in-process logic)",
        _       => "",
    };

    private static string KindRationale(
        TestKind k, bool hasExternal, bool matters, bool lowStakes) => k switch
    {
        TestKind.Unit =>
            "**Unit tests** for the core logic. The spine of any test strategy — " +
            "fast, cheap, and where the bulk of correctness lives.",
        TestKind.Integration => hasExternal
            ? "**Integration tests** for the parts that hit external systems. Use " +
              "the same unit framework — DB testing belongs here, not a separate " +
              "framework."
            : "**Integration tests** stitching multiple components together.",
        TestKind.Component =>
            "**Component tests** for the rendered UI. React Testing Library on top " +
            "of the unit runner you already have — render the component, drive it " +
            "with userEvent, assert on the result.",
        TestKind.EndToEnd =>
            "**End-to-end tests** for the most important user flows. Slow and " +
            "expensive to maintain, but the only thing that catches real-browser " +
            "regressions before users do.",
        TestKind.ManualChecklist =>
            "**Manual checklist** — a written list of \"things to click through\" " +
            "before each release. Not a cop-out; a deliberate practice for the " +
            "stakes you've described.",
        _ => k.ToString(),
    };
}

/// <summary>A recommended strategy, ready to be turned into a saved <see cref="TestingPlan"/>.</summary>
public sealed record StrategyRecommendation(
    string Summary,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<TestKind> Kinds);
