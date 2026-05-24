using ClaudePM.Core.Models;

namespace ClaudePM.Core.Services;

/// <summary>
/// A read-only catalog of testing frameworks the Testing Manager can
/// recommend and generate setup prompts for. Ships with the app — NOT user
/// data, NOT in the database. Adding a framework is one data record, not a
/// logic edit. The catalog deliberately treats database testing as
/// <see cref="TestKind.Integration"/> inside the project's language
/// framework (xUnit, pytest, etc.), NOT as its own framework.
/// </summary>
public interface ITestingFrameworkCatalog
{
    /// <summary>All frameworks the app knows about.</summary>
    IReadOnlyList<TestingFramework> All { get; }

    /// <summary>
    /// Frameworks targeting a specific language token (e.g. "DotNet",
    /// "Python", "JavaScript", "Cpp"). Match is case-sensitive on the token
    /// the questionnaire uses. Returns an empty list for unknown tokens.
    /// </summary>
    IReadOnlyList<TestingFramework> ForLanguage(string languageToken);

    /// <summary>Looks up a single framework by exact <see cref="TestingFramework.Name"/>.</summary>
    TestingFramework? ByName(string name);
}

/// <summary>
/// One catalog entry. Self-contained record so adding a framework later
/// means appending one literal to the seed list, not editing logic.
/// </summary>
/// <param name="Name">Display name (e.g. "xUnit", "Playwright").</param>
/// <param name="Language">
/// Questionnaire-vocab language token this framework targets
/// (e.g. "DotNet", "Python"). Use "Any" for tools that work across language
/// ecosystems (Playwright, which is web-end-to-end regardless of stack).
/// </param>
/// <param name="Kinds">Which <see cref="TestKind"/>s this framework serves.</param>
/// <param name="SetupPromptTemplate">
/// A Claude Code prompt the user can paste into a fresh session to add the
/// framework, establish the test folder layout, and write one example test
/// that establishes the pattern. May contain <c>{{ProjectName}}</c> and
/// <c>{{ProjectPath}}</c> placeholders that the VM fills in.
/// </param>
/// <param name="Note">
/// Optional note shown alongside the recommendation (e.g. "Established
/// alternative to Vitest — only pick this if you already have it").
/// </param>
public sealed record TestingFramework(
    string Name,
    string Language,
    IReadOnlyList<TestKind> Kinds,
    string SetupPromptTemplate,
    string Note = "");
