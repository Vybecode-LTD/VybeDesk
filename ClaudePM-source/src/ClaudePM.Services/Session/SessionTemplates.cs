using ClaudePM.Core.Models;

namespace ClaudePM.Services.Session;

/// <summary>
/// Per-<see cref="SessionTemplate"/> content for the four canonical files
/// every handoff package ships (CLAUDE.md / README.md / .gitignore /
/// KICKOFF.md). All content is plain const-string composition — easy to
/// review in source. Substitution points are <c>{projectName}</c>,
/// <c>{description}</c>, and <c>{stack}</c>.
///
/// The user can edit anything after generation; the templates aim for
/// "competent and idiomatic" rather than encyclopedic.
/// </summary>
public static class SessionTemplates
{
    /// <summary>
    /// Returns the four scaffold strings for the chosen template, with
    /// project name, description, and stack substituted in.
    /// </summary>
    public static (string ClaudeMd, string Readme, string GitIgnore, string Kickoff)
        For(SessionTemplate template, string projectName, string description, string stack)
    {
        var name = string.IsNullOrWhiteSpace(projectName) ? "<project>" : projectName.Trim();
        var desc = string.IsNullOrWhiteSpace(description) ? "<To be defined.>" : description.Trim();
        var stk  = string.IsNullOrWhiteSpace(stack) ? "<To be defined.>" : stack.Trim();

        return template switch
        {
            SessionTemplate.AvaloniaDotNet => (
                ClaudeMd: AvaloniaClaudeMd(name, desc),
                Readme:   AvaloniaReadme(name, desc),
                GitIgnore: DotNetGitIgnore,
                Kickoff:  AvaloniaKickoff(name, desc, stk)),
            SessionTemplate.FastApiPython => (
                ClaudeMd: FastApiClaudeMd(name, desc),
                Readme:   FastApiReadme(name, desc),
                GitIgnore: PythonGitIgnore,
                Kickoff:  FastApiKickoff(name, desc, stk)),
            SessionTemplate.NextJsTypeScript => (
                ClaudeMd: NextJsClaudeMd(name, desc),
                Readme:   NextJsReadme(name, desc),
                GitIgnore: NextJsGitIgnore,
                Kickoff:  NextJsKickoff(name, desc, stk)),
            SessionTemplate.PythonCli => (
                ClaudeMd: PythonCliClaudeMd(name, desc),
                Readme:   PythonCliReadme(name, desc),
                GitIgnore: PythonGitIgnore,
                Kickoff:  PythonCliKickoff(name, desc, stk)),
            _ => (
                ClaudeMd: PlainClaudeMd(name, desc),
                Readme:   PlainReadme(name, desc),
                GitIgnore: PlainGitIgnore,
                Kickoff:  PlainKickoff(name, desc, stk)),
        };
    }

    // ===== Plain (default, generic) ==========================================

    private static string PlainClaudeMd(string name, string desc) =>
        "# CLAUDE.md — " + name + "\n\n" +
        "> Context file. New sessions read this first. Keep \"Last Completed Task\" current.\n\n" +
        "## Last Completed Task\n" +
        "Project handed off from a claude.ai chat session via ClaudePM. The design and\n" +
        "decisions are captured here and in docs/transcripts/. No code has been generated\n" +
        "yet — see KICKOFF.md for the first task.\n\n" +
        "## Overview\n" + desc + "\n\n" +
        "## Architecture\n" +
        "<To be defined — see docs/transcripts/ for the design discussion.>\n\n" +
        "## Build, Test, Run\n" +
        "<To be defined.>\n\n" +
        "## Conventions\n" +
        "- Keep this file's \"Last Completed Task\" current at the end of every session.\n" +
        "- Prefer small, reviewable changes; each commit message explains the why.\n" +
        "- New deps go through a short justification in the commit message.\n\n" +
        "## Gotchas\n" +
        "<Document footguns here as you find them.>\n\n" +
        "## Reference Docs\n" +
        "The original claude.ai conversation(s) are in docs/transcripts/. See KICKOFF.md\n" +
        "for the first task.\n";

    private static string PlainReadme(string name, string desc) =>
        "# " + name + "\n\n" +
        desc + "\n\n" +
        "## Overview\n" +
        "Handed off from claude.ai via ClaudePM. See CLAUDE.md for context and KICKOFF.md\n" +
        "to begin work in Claude Code.\n\n" +
        "## Stack\n" +
        "TBD — see CLAUDE.md.\n\n" +
        "## Build & Run\n" +
        "<To be defined once the project is scaffolded.>\n\n" +
        "## Project layout\n" +
        "```\n" +
        "/        — source\n" +
        "/docs    — transcripts and notes from the kickoff conversation\n" +
        "```\n";

    private static string PlainKickoff(string name, string desc, string stack) =>
        "# Kickoff — " + name + "\n\n" +
        "You're picking up development on **" + name + "**, a project handed off from a\n" +
        "claude.ai conversation. Read CLAUDE.md and docs/transcripts/ first.\n\n" +
        "## Description\n" + desc + "\n\n" +
        "## Stack\n" + stack + "\n\n" +
        "## First task\n" +
        "1. Read CLAUDE.md (project context) and every file under docs/transcripts/.\n" +
        "2. Summarise the proposed architecture back to the user in your own words and\n" +
        "   confirm any open questions before scaffolding.\n" +
        "3. Once confirmed, scaffold the project structure for the chosen stack.\n\n" +
        "Don't start coding until the user confirms the direction.\n";

    public const string PlainGitIgnore =
        ".DS_Store\n" +
        "Thumbs.db\n" +
        "*.log\n" +
        "*.tmp\n" +
        "*.swp\n" +
        ".vs/\n" +
        ".idea/\n" +
        ".vscode/\n" +
        "bin/\n" +
        "obj/\n" +
        "build/\n" +
        "dist/\n" +
        "node_modules/\n";

    // ===== Avalonia + .NET ===================================================

    private static string AvaloniaClaudeMd(string name, string desc) =>
        "# CLAUDE.md — " + name + "\n\n" +
        "> Context file. New sessions read this first. Keep \"Last Completed Task\" current.\n\n" +
        "## Last Completed Task\n" +
        "Project handed off from a claude.ai chat session via ClaudePM. No code has\n" +
        "been generated yet — see KICKOFF.md for the first task.\n\n" +
        "## Overview\n" + desc + "\n\n" +
        "## Architecture\n" +
        "Avalonia 11 desktop app on .NET 9 with strict MVVM:\n" +
        "- **Core** — domain models + interfaces. No framework deps.\n" +
        "- **Services** — concrete services (data, IO, AI clients). Depend only on Core.\n" +
        "- **App** — Avalonia Views / ViewModels, DI composition root.\n" +
        "- **Tests** — xUnit.\n\n" +
        "MVVM via **CommunityToolkit.Mvvm** source generators. ViewModel classes MUST be\n" +
        "`partial` — without it, `[ObservableProperty]` / `[RelayCommand]` silently no-op.\n" +
        "Compiled bindings (`x:DataType`) on every View. No `INotifyPropertyChanged` by\n" +
        "hand. No `new`-ing ViewModels in code-behind — DI everywhere.\n\n" +
        "## Build, Test, Run\n" +
        "- Build: `dotnet build`\n" +
        "- Test: `dotnet test`\n" +
        "- Run: `dotnet run --project src/" + name + ".App`\n\n" +
        "## Conventions\n" +
        "- Views end in `View`, ViewModels in `ViewModel`, services in `Service`.\n" +
        "- ViewModels are `sealed partial` and inherit `ObservableObject` (directly or\n" +
        "  via a shared base).\n" +
        "- Long-running work runs off the UI thread; commands are async with cancellation.\n" +
        "- Strict one-directional layering: Core ← Services ← App.\n\n" +
        "## Gotchas\n" +
        "- Missing `partial` on a ViewModel = generators silently emit nothing.\n" +
        "  Symptom: bindings show literal text or commands don't fire.\n" +
        "- Compiled bindings need `x:DataType` on the View root AND on every\n" +
        "  `DataTemplate` whose item type differs.\n" +
        "- Avalonia 11.3 requires `Avalonia.Themes.Fluent` or `Avalonia.Themes.Simple`\n" +
        "  in App.axaml — without one, controls render unstyled.\n\n" +
        "## Reference Docs\n" +
        "Original claude.ai conversation(s) in docs/transcripts/. See KICKOFF.md to begin.\n";

    private static string AvaloniaReadme(string name, string desc) =>
        "# " + name + "\n\n" +
        desc + "\n\n" +
        "## Stack\n" +
        "Avalonia 11 / .NET 9 / CommunityToolkit.Mvvm.\n\n" +
        "## Build & Run\n" +
        "```\n" +
        "dotnet restore\n" +
        "dotnet build\n" +
        "dotnet test\n" +
        "dotnet run --project src/" + name + ".App\n" +
        "```\n\n" +
        "## Project layout\n" +
        "```\n" +
        "src/\n" +
        "  " + name + ".Core/      — domain models + interfaces (no framework deps)\n" +
        "  " + name + ".Services/  — concrete services (data, IO, AI clients)\n" +
        "  " + name + ".App/       — Avalonia Views / ViewModels / DI composition root\n" +
        "tests/\n" +
        "  " + name + ".Tests/     — xUnit tests\n" +
        "```\n";

    private static string AvaloniaKickoff(string name, string desc, string stack) =>
        "# Kickoff — " + name + "\n\n" +
        "You're picking up development on **" + name + "**, an Avalonia 11 / .NET 9\n" +
        "desktop application. Read CLAUDE.md and docs/transcripts/ first.\n\n" +
        "## Description\n" + desc + "\n\n" +
        "## Stack\n" + stack + "\n\n" +
        "## First task\n" +
        "1. Read CLAUDE.md and every file under docs/transcripts/.\n" +
        "2. Confirm the layered structure with the user (Core / Services / App / Tests).\n" +
        "3. Scaffold the four-project solution:\n" +
        "   - `dotnet new sln -n " + name + "`\n" +
        "   - `dotnet new classlib -n " + name + ".Core -o src/" + name + ".Core`\n" +
        "   - `dotnet new classlib -n " + name + ".Services -o src/" + name + ".Services`\n" +
        "   - `dotnet new avalonia.app -n " + name + ".App -o src/" + name + ".App`\n" +
        "   - `dotnet new xunit -n " + name + ".Tests -o tests/" + name + ".Tests`\n" +
        "   - Wire project references, add CommunityToolkit.Mvvm to App.\n" +
        "4. Add the DI composition root in `App.axaml.cs` and a tiny smoke-test View.\n\n" +
        "Don't start coding until the user confirms the direction.\n";

    public const string DotNetGitIgnore =
        "bin/\n" +
        "obj/\n" +
        "*.user\n" +
        "*.suo\n" +
        ".vs/\n" +
        "*.userprefs\n" +
        "*.swp\n" +
        ".DS_Store\n" +
        "Thumbs.db\n" +
        "TestResults/\n" +
        "coverage/\n" +
        "*.coverage\n" +
        "*.lutconfig\n" +
        "publish/\n";

    // ===== FastAPI + Python ==================================================

    private static string FastApiClaudeMd(string name, string desc) =>
        "# CLAUDE.md — " + name + "\n\n" +
        "> Context file. New sessions read this first. Keep \"Last Completed Task\" current.\n\n" +
        "## Last Completed Task\n" +
        "Project handed off from a claude.ai chat session via ClaudePM. No code has\n" +
        "been generated yet — see KICKOFF.md for the first task.\n\n" +
        "## Overview\n" + desc + "\n\n" +
        "## Architecture\n" +
        "FastAPI service on Python 3.12+. Layered:\n" +
        "- `app/routers/` — endpoint handlers, one file per resource.\n" +
        "- `app/services/` — business logic. Pure functions where possible.\n" +
        "- `app/schemas/` — Pydantic models for request/response shapes.\n" +
        "- `app/dependencies/` — `Depends(...)` providers (db session, auth, etc).\n" +
        "- `app/main.py` — `FastAPI()` app construction + router registration.\n\n" +
        "Type hints are mandatory; they drive both the editor experience and FastAPI's\n" +
        "auto-generated OpenAPI schema. Dependency injection via `Depends(...)` — no\n" +
        "module-level singletons for things that need test substitution.\n\n" +
        "## Build, Test, Run\n" +
        "- Set up: `python -m venv .venv && .venv/Scripts/activate` (Win) or `source .venv/bin/activate` (POSIX)\n" +
        "- Install: `pip install -r requirements.txt`\n" +
        "- Run: `uvicorn app.main:app --reload`\n" +
        "- Test: `pytest`\n\n" +
        "## Conventions\n" +
        "- Every endpoint returns a Pydantic schema, never a raw dict.\n" +
        "- Tests use `httpx.AsyncClient` against the in-memory app — no live server.\n" +
        "- Async route handlers by default; sync only where a sync dependency forces it.\n" +
        "- `pytest` fixtures for setup; one fixture per concern (db, auth, client).\n\n" +
        "## Gotchas\n" +
        "- Forget `await` on an async dependency = silent coroutine warning, no data.\n" +
        "- Pydantic v2 changed `dict()` → `model_dump()`; old guides will mislead.\n" +
        "- Forgetting to register a router in `app/main.py` = 404s with no error.\n\n" +
        "## Reference Docs\n" +
        "Original claude.ai conversation(s) in docs/transcripts/. See KICKOFF.md to begin.\n";

    private static string FastApiReadme(string name, string desc) =>
        "# " + name + "\n\n" +
        desc + "\n\n" +
        "## Stack\n" +
        "FastAPI / Python 3.12+ / Pydantic v2 / pytest.\n\n" +
        "## Build & Run\n" +
        "```\n" +
        "python -m venv .venv\n" +
        ".venv/Scripts/activate     # Windows\n" +
        "source .venv/bin/activate  # POSIX\n" +
        "pip install -r requirements.txt\n" +
        "uvicorn app.main:app --reload\n" +
        "pytest\n" +
        "```\n\n" +
        "## Project layout\n" +
        "```\n" +
        "app/\n" +
        "  main.py            — FastAPI app construction\n" +
        "  routers/           — one file per resource\n" +
        "  services/          — business logic\n" +
        "  schemas/           — Pydantic request/response models\n" +
        "  dependencies/      — Depends(...) providers\n" +
        "tests/               — pytest tests\n" +
        "requirements.txt\n" +
        "```\n";

    private static string FastApiKickoff(string name, string desc, string stack) =>
        "# Kickoff — " + name + "\n\n" +
        "You're picking up development on **" + name + "**, a FastAPI service on\n" +
        "Python 3.12+. Read CLAUDE.md and docs/transcripts/ first.\n\n" +
        "## Description\n" + desc + "\n\n" +
        "## Stack\n" + stack + "\n\n" +
        "## First task\n" +
        "1. Read CLAUDE.md and every file under docs/transcripts/.\n" +
        "2. Confirm the resource model with the user — what endpoints, what data shapes.\n" +
        "3. Scaffold:\n" +
        "   - `app/main.py` with `FastAPI()` and router registration.\n" +
        "   - One `app/routers/<resource>.py` per resource discussed.\n" +
        "   - Matching `app/schemas/<resource>.py` Pydantic models.\n" +
        "   - `app/services/<resource>.py` for business logic (pure where possible).\n" +
        "   - `app/dependencies/db.py` (or similar) for shared `Depends(...)` providers.\n" +
        "   - `requirements.txt` pinning fastapi, uvicorn[standard], pydantic, pytest, httpx.\n" +
        "   - One `tests/test_<resource>.py` per router using `httpx.AsyncClient`.\n\n" +
        "Don't start coding until the user confirms the direction.\n";

    public const string PythonGitIgnore =
        "__pycache__/\n" +
        "*.pyc\n" +
        "*.pyo\n" +
        "*.pyd\n" +
        "venv/\n" +
        ".venv/\n" +
        "env/\n" +
        ".pytest_cache/\n" +
        ".mypy_cache/\n" +
        ".ruff_cache/\n" +
        ".coverage\n" +
        "htmlcov/\n" +
        "dist/\n" +
        "build/\n" +
        "*.egg-info/\n" +
        ".env\n" +
        ".env.local\n" +
        ".DS_Store\n" +
        "Thumbs.db\n";

    // ===== Next.js + TypeScript ==============================================

    private static string NextJsClaudeMd(string name, string desc) =>
        "# CLAUDE.md — " + name + "\n\n" +
        "> Context file. New sessions read this first. Keep \"Last Completed Task\" current.\n\n" +
        "## Last Completed Task\n" +
        "Project handed off from a claude.ai chat session via ClaudePM. No code has\n" +
        "been generated yet — see KICKOFF.md for the first task.\n\n" +
        "## Overview\n" + desc + "\n\n" +
        "## Architecture\n" +
        "Next.js 14+ with the **App Router** (NOT pages router) and **TypeScript strict\n" +
        "mode**. Layered by concern:\n" +
        "- `app/` — routes (folders = URL segments, `page.tsx` per route, `layout.tsx`\n" +
        "  for shared chrome). Server Components by default; mark `'use client'` only\n" +
        "  where interactivity actually needs it.\n" +
        "- `components/` — reusable UI pieces, separated into `ui/` (presentational)\n" +
        "  and feature folders.\n" +
        "- `lib/` — non-UI logic (data fetching, utilities, validation).\n" +
        "- `app/api/` — Route Handlers when needed.\n\n" +
        "`fetch` caching defaults are AGGRESSIVE in App Router — use\n" +
        "`{ cache: 'no-store' }` or `next: { revalidate: N }` deliberately.\n\n" +
        "## Build, Test, Run\n" +
        "- Install: `npm install` (or `pnpm install` / `bun install`)\n" +
        "- Dev: `npm run dev` (Turbopack on Next.js 14+)\n" +
        "- Build: `npm run build`\n" +
        "- Test: `npm test` (Vitest by default — change if the project uses Jest)\n" +
        "- Lint: `npm run lint`\n\n" +
        "## Conventions\n" +
        "- TypeScript `strict: true` — no implicit `any`, no `// @ts-ignore` without a comment.\n" +
        "- Server Components by default; client components are the exception, not the rule.\n" +
        "- Forms use Server Actions where possible.\n" +
        "- One component per file; named exports for components, default export for `page.tsx`.\n\n" +
        "## Gotchas\n" +
        "- `'use client'` is FILE-scoped: the whole file (and everything it imports that\n" +
        "  isn't itself a Server Component boundary) becomes client.\n" +
        "- `fetch` deduplicates within a render but `cache` defaults vary by `dynamic`\n" +
        "  config — read the App Router caching docs before chasing stale data.\n" +
        "- `next/image` requires a configured `remotePatterns` for external hosts.\n\n" +
        "## Reference Docs\n" +
        "Original claude.ai conversation(s) in docs/transcripts/. See KICKOFF.md to begin.\n";

    private static string NextJsReadme(string name, string desc) =>
        "# " + name + "\n\n" +
        desc + "\n\n" +
        "## Stack\n" +
        "Next.js 14+ (App Router) / TypeScript strict / React 18+.\n\n" +
        "## Build & Run\n" +
        "```\n" +
        "npm install\n" +
        "npm run dev        # local dev server\n" +
        "npm run build      # production build\n" +
        "npm test           # unit tests\n" +
        "npm run lint       # eslint\n" +
        "```\n\n" +
        "## Project layout\n" +
        "```\n" +
        "app/               — App Router routes (folders = URL segments)\n" +
        "  layout.tsx       — root layout\n" +
        "  page.tsx         — home route\n" +
        "  api/             — Route Handlers\n" +
        "components/        — reusable UI\n" +
        "  ui/              — presentational primitives\n" +
        "lib/               — utilities, fetchers, validators\n" +
        "public/            — static assets\n" +
        "tsconfig.json\n" +
        "next.config.js\n" +
        "package.json\n" +
        "```\n";

    private static string NextJsKickoff(string name, string desc, string stack) =>
        "# Kickoff — " + name + "\n\n" +
        "You're picking up development on **" + name + "**, a Next.js 14+ (App Router)\n" +
        "TypeScript application. Read CLAUDE.md and docs/transcripts/ first.\n\n" +
        "## Description\n" + desc + "\n\n" +
        "## Stack\n" + stack + "\n\n" +
        "## First task\n" +
        "1. Read CLAUDE.md and every file under docs/transcripts/.\n" +
        "2. Confirm the route map and data model with the user.\n" +
        "3. Scaffold:\n" +
        "   - `npx create-next-app@latest " + name + " --typescript --eslint --app --src-dir=false`\n" +
        "     (accept TypeScript strict, App Router, ESLint; decline Tailwind unless asked).\n" +
        "   - Replace the default `app/page.tsx` with a placeholder routed to the\n" +
        "     discussed home view.\n" +
        "   - Add `app/<route>/page.tsx` stubs for each route discussed.\n" +
        "   - Add `lib/` with one initial utility module per data shape.\n" +
        "   - Add `components/ui/` with placeholder primitives.\n\n" +
        "Don't start coding until the user confirms the direction.\n";

    public const string NextJsGitIgnore =
        "node_modules/\n" +
        ".next/\n" +
        "out/\n" +
        "build/\n" +
        "dist/\n" +
        ".turbo/\n" +
        ".vercel/\n" +
        ".env\n" +
        ".env*.local\n" +
        "*.log\n" +
        "npm-debug.log*\n" +
        "yarn-debug.log*\n" +
        "yarn-error.log*\n" +
        "pnpm-debug.log*\n" +
        ".DS_Store\n" +
        "Thumbs.db\n" +
        "coverage/\n" +
        "next-env.d.ts\n" +
        ".idea/\n" +
        ".vscode/\n";

    // ===== Python CLI ========================================================

    private static string PythonCliClaudeMd(string name, string desc) =>
        "# CLAUDE.md — " + name + "\n\n" +
        "> Context file. New sessions read this first. Keep \"Last Completed Task\" current.\n\n" +
        "## Last Completed Task\n" +
        "Project handed off from a claude.ai chat session via ClaudePM. No code has\n" +
        "been generated yet — see KICKOFF.md for the first task.\n\n" +
        "## Overview\n" + desc + "\n\n" +
        "## Architecture\n" +
        "Python CLI on 3.12+, packaged with a `pyproject.toml` and a console\n" +
        "entry point. Layered:\n" +
        "- `src/<pkg>/cli.py` — argparse / Click / Typer entry point. Thin — parses args\n" +
        "  then hands off to the library code.\n" +
        "- `src/<pkg>/commands/` — one module per command.\n" +
        "- `src/<pkg>/core/` — library code with no CLI awareness; importable as a\n" +
        "  regular Python package.\n" +
        "- `tests/` — pytest, mirroring the package shape.\n\n" +
        "Argument parsing lives ONLY in `cli.py`. The command modules take typed\n" +
        "arguments — that's what makes the package usable as a library and easy to test.\n\n" +
        "## Build, Test, Run\n" +
        "- Set up: `python -m venv .venv && .venv/Scripts/activate` (Win) or `source .venv/bin/activate` (POSIX)\n" +
        "- Install: `pip install -e .[dev]`\n" +
        "- Run: `" + name.ToLowerInvariant() + " --help`\n" +
        "- Test: `pytest`\n\n" +
        "## Conventions\n" +
        "- Type hints everywhere; `mypy --strict` clean.\n" +
        "- Exit codes are meaningful: `0` success, `1` user error, `2` internal error.\n" +
        "- stdout for results, stderr for status/errors — never mix them.\n" +
        "- Tests use `pytest` + `capsys` / `tmp_path` fixtures.\n\n" +
        "## Gotchas\n" +
        "- `print(..., file=sys.stderr)` for status messages — pipes break otherwise.\n" +
        "- Editable installs (`pip install -e`) require the entry point in `pyproject.toml`\n" +
        "  to land before the install — re-run `pip install -e .` if you add one.\n" +
        "- Don't catch `KeyboardInterrupt` at the command boundary; let the CLI layer\n" +
        "  handle it once for a clean exit.\n\n" +
        "## Reference Docs\n" +
        "Original claude.ai conversation(s) in docs/transcripts/. See KICKOFF.md to begin.\n";

    private static string PythonCliReadme(string name, string desc) =>
        "# " + name + "\n\n" +
        desc + "\n\n" +
        "## Stack\n" +
        "Python 3.12+ CLI / pyproject.toml / pytest.\n\n" +
        "## Build & Run\n" +
        "```\n" +
        "python -m venv .venv\n" +
        ".venv/Scripts/activate     # Windows\n" +
        "source .venv/bin/activate  # POSIX\n" +
        "pip install -e .[dev]\n" +
        "" + name.ToLowerInvariant() + " --help\n" +
        "pytest\n" +
        "```\n\n" +
        "## Project layout\n" +
        "```\n" +
        "src/\n" +
        "  " + SafePyPkg(name) + "/\n" +
        "    cli.py             — entry point (argparse / Click / Typer)\n" +
        "    commands/          — one module per command\n" +
        "    core/              — library code, no CLI awareness\n" +
        "tests/                 — pytest tests mirroring the package shape\n" +
        "pyproject.toml         — package metadata + console entry point\n" +
        "```\n";

    private static string PythonCliKickoff(string name, string desc, string stack) =>
        "# Kickoff — " + name + "\n\n" +
        "You're picking up development on **" + name + "**, a Python 3.12+ CLI.\n" +
        "Read CLAUDE.md and docs/transcripts/ first.\n\n" +
        "## Description\n" + desc + "\n\n" +
        "## Stack\n" + stack + "\n\n" +
        "## First task\n" +
        "1. Read CLAUDE.md and every file under docs/transcripts/.\n" +
        "2. Confirm the command surface with the user — what subcommands, what flags.\n" +
        "3. Scaffold:\n" +
        "   - `src/" + SafePyPkg(name) + "/__init__.py` (empty).\n" +
        "   - `src/" + SafePyPkg(name) + "/cli.py` with the argument parser and entry point.\n" +
        "   - `src/" + SafePyPkg(name) + "/commands/` with one module per discussed command.\n" +
        "   - `src/" + SafePyPkg(name) + "/core/` with library-shaped helpers.\n" +
        "   - `pyproject.toml` with metadata, dev deps (pytest, mypy, ruff), and a\n" +
        "     `[project.scripts]` entry point pointing at `cli:main`.\n" +
        "   - `tests/test_cli.py` exercising `--help` and the smoke path of each command.\n\n" +
        "Don't start coding until the user confirms the direction.\n";

    /// <summary>
    /// Convert a free-form project name into a valid Python package identifier
    /// (lowercase, ASCII letters/digits/underscores, must start with a letter).
    /// </summary>
    private static string SafePyPkg(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (c is '-' or ' ' or '.') sb.Append('_');
        }
        var s = sb.ToString().Trim('_');
        if (s.Length == 0 || !char.IsLetter(s[0])) s = "pkg_" + s;
        return s;
    }
}
