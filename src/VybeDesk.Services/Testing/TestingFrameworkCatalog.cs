using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Services.Testing;

/// <summary>
/// The default, ships-with-the-app framework catalog. Seven starter
/// entries cover the language families the app currently supports;
/// adding a new framework is one new <see cref="TestingFramework"/>
/// literal in <see cref="Seed"/>, no logic edit. The setup prompts
/// instruct Claude Code to set up the folder layout AND write one example
/// test — establishing the pattern is more useful than just adding the
/// framework.
/// </summary>
public sealed class TestingFrameworkCatalog : ITestingFrameworkCatalog
{
    public IReadOnlyList<TestingFramework> All { get; }

    public TestingFrameworkCatalog() => All = Seed;

    public IReadOnlyList<TestingFramework> ForLanguage(string languageToken)
        => All.Where(f => f.Language == languageToken || f.Language == "Any")
              .ToList();

    public TestingFramework? ByName(string name)
        => All.FirstOrDefault(f => f.Name == name);

    private static readonly IReadOnlyList<TestingFramework> Seed = new[]
    {
        new TestingFramework(
            Name: "xUnit",
            Language: "DotNet",
            Kinds: new[] { TestKind.Unit, TestKind.Integration },
            SetupPromptTemplate:
                "Set up xUnit for the .NET project at {{ProjectPath}} ({{ProjectName}}).\n\n" +
                "1. Add an xUnit test project alongside the existing source project(s), " +
                "following the conventional `tests/<ProjectName>.Tests` layout.\n" +
                "2. Reference the source project(s) from the test project.\n" +
                "3. Write ONE example test that exercises a real method in the source " +
                "code — pick the simplest non-trivial method and assert its expected " +
                "behaviour. The point is to establish the pattern; subsequent tests " +
                "will follow this shape.\n" +
                "4. Confirm `dotnet test` runs the example test and passes.\n\n" +
                "Database testing belongs here as integration tests inside this same " +
                "xUnit project, NOT as a separate framework. Use a temp SQLite file or " +
                "an in-memory SQLite connection per test, scoped via IDisposable."),

        new TestingFramework(
            Name: "GoogleTest",
            Language: "Cpp",
            Kinds: new[] { TestKind.Unit },
            SetupPromptTemplate:
                "Set up GoogleTest for the C++ project at {{ProjectPath}} ({{ProjectName}}).\n\n" +
                "1. Add GoogleTest as a dependency (CMake's FetchContent is the lowest-" +
                "friction path; vcpkg also works if the project already uses it).\n" +
                "2. Establish a `tests/` folder with a CMakeLists.txt that builds a " +
                "test executable linking against gtest_main.\n" +
                "3. Write ONE example test that exercises a real function in the source " +
                "code, establishing the pattern.\n" +
                "4. Confirm the test executable builds and the example passes via ctest " +
                "or running the test binary directly."),

        new TestingFramework(
            Name: "pytest",
            Language: "Python",
            Kinds: new[] { TestKind.Unit, TestKind.Integration },
            SetupPromptTemplate:
                "Set up pytest for the Python project at {{ProjectPath}} ({{ProjectName}}).\n\n" +
                "1. Add `pytest` to the dev dependencies (pyproject.toml/requirements-dev.txt — " +
                "match the project's existing convention).\n" +
                "2. Create a `tests/` folder with a `conftest.py` (even if empty initially) " +
                "and one `test_*.py` file.\n" +
                "3. Write ONE example test that exercises a real function in the source " +
                "code, establishing the assert/arrange/act pattern.\n" +
                "4. Confirm `pytest` discovers and passes the example test.\n\n" +
                "Database testing belongs here as integration tests inside this same " +
                "pytest suite, NOT as a separate framework. Use a temp SQLite file or " +
                "fixtures that wrap a per-test transaction."),

        new TestingFramework(
            Name: "Vitest",
            Language: "JavaScript",
            Kinds: new[] { TestKind.Unit, TestKind.Integration },
            SetupPromptTemplate:
                "Set up Vitest for the JavaScript/TypeScript project at {{ProjectPath}} " +
                "({{ProjectName}}).\n\n" +
                "1. Install vitest as a dev dependency. If the project uses Vite already, " +
                "configuration is implicit; otherwise add a minimal vitest.config.ts.\n" +
                "2. Add an `npm test` script that runs `vitest`.\n" +
                "3. Place tests alongside source files as `*.test.ts` (or `__tests__/` " +
                "folder, whichever matches the project's existing convention).\n" +
                "4. Write ONE example test that exercises a real function in the source " +
                "code, establishing the pattern.\n" +
                "5. Confirm `npm test` passes."),

        new TestingFramework(
            Name: "Jest",
            Language: "JavaScript",
            Kinds: new[] { TestKind.Unit },
            Note: "Established alternative to Vitest. Only pick this if the project " +
                  "already uses Jest, or if a specific tooling dependency requires it. " +
                  "Vitest is the default recommendation for new JS/TS projects.",
            SetupPromptTemplate:
                "Set up Jest for the JavaScript/TypeScript project at {{ProjectPath}} " +
                "({{ProjectName}}).\n\n" +
                "1. Install jest, ts-jest (if TypeScript), and @types/jest as dev deps.\n" +
                "2. Add a jest.config.js (or `jest` field in package.json) and an " +
                "`npm test` script that runs `jest`.\n" +
                "3. Place tests as `*.test.ts` next to source or under `__tests__/`.\n" +
                "4. Write ONE example test exercising real source-code behaviour.\n" +
                "5. Confirm `npm test` passes."),

        new TestingFramework(
            Name: "React Testing Library",
            Language: "JavaScript",
            Kinds: new[] { TestKind.Component },
            Note: "Runs on top of Vitest or Jest — pick one of those first. RTL is " +
                  "what you use to render and assert against React components.",
            SetupPromptTemplate:
                "Add React Testing Library to the JS/TS project at {{ProjectPath}} " +
                "({{ProjectName}}). A test runner (Vitest or Jest) must already be set up.\n\n" +
                "1. Install @testing-library/react, @testing-library/jest-dom, and " +
                "@testing-library/user-event as dev deps. Wire up jest-dom matchers " +
                "in the test setup file.\n" +
                "2. Pick the simplest non-trivial React component in the project. Write " +
                "ONE example test that renders it and asserts on the rendered output " +
                "using userEvent for any interactions. This establishes the " +
                "render/find/assert pattern.\n" +
                "3. Confirm the test runs via the existing `npm test` command."),

        new TestingFramework(
            Name: "Playwright",
            Language: "Any",
            Kinds: new[] { TestKind.EndToEnd },
            Note: "Front-end-stack-agnostic. Tests a real browser hitting a real " +
                  "running app — the slowest and most valuable kind of test.",
            SetupPromptTemplate:
                "Set up Playwright for end-to-end testing of {{ProjectName}} at " +
                "{{ProjectPath}}.\n\n" +
                "1. Install Playwright via `npm init playwright@latest` (or the " +
                "equivalent for the project's package manager). Pick the test folder " +
                "(`e2e/` is conventional), browsers (Chromium is enough to start), " +
                "and yes-to-GitHub-Actions if a workflow exists.\n" +
                "2. Configure the baseURL to point at the locally-running app. If the " +
                "app needs to be started for tests, add a `webServer` block in " +
                "playwright.config.ts that runs the dev/start command.\n" +
                "3. Write ONE example test that opens the app, asserts the page title " +
                "or a unique element on the landing screen, and exits cleanly. This " +
                "establishes the spec.ts pattern.\n" +
                "4. Confirm `npx playwright test` passes."),
    };
}
