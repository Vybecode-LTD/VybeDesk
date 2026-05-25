# Kickoff — ClaudePM

> **HISTORICAL DOCUMENT.** This is the original bootstrap prompt from the
> first Claude Code session against this repo. Most of the "deferred v1.1
> items" listed below (drag-and-drop file staging, FTS5 search, redesign
> diff view, prompt version history, streaming `tool_use`, git-aware
> staleness) have all shipped — see [CHANGELOG.md](CHANGELOG.md) for the
> versioned history and [ROADMAP.md](ROADMAP.md) for what's still planned.
> Kept here for repository archaeology; for current state read
> [CLAUDE.md](CLAUDE.md) and [README.md](README.md) instead.

This repo was scaffolded module-by-module in a claude.ai session and handed off
to Claude Code. Read `CLAUDE.md` first (context + current state), then `SPEC.md`
(full feature/architecture spec). This file is the first-task prompt.

## First task — verify the build

The solution has never been compiled. Before any feature work:

```
dotnet restore
dotnet build
dotnet test
```

Requires the .NET 9 SDK. Fix whatever the build surfaces — likely candidates:
a missing `using`, an AXAML compiled-binding quirk, or a NuGet version that
needs bumping (`Avalonia` is pinned to 11.2.1, `CommunityToolkit.Mvvm` 8.4.0,
`Microsoft.Data.Sqlite` 9.0.0 — adjust if restore complains). Then:

```
dotnet run --project src/ClaudePM.App
```

Confirm the app launches, the sidebar navigates between all seven pages, and
Settings can save an API key. Update `CLAUDE.md`'s "Last Completed Task" once
the build is green.

## What is already built

All five modules plus shell/Home/Settings are implemented (see CLAUDE.md):
1. Documentation Manager  2. Prompt Manager  3. Session Builder
4. AI Notebook  5. Skill Library Manager

> **Historical note (added during doc audit):** This module list reflects the
> original scaffold. Module 5 (Skill Library Manager) was removed in v0.25
> after a persistent layout bug; the Module 5 slot was taken by Bug Tracker
> in v0.26, then reclaimed by a clean-slate Skills rebuild in v0.28 (which
> reshuffled Bug Tracker to Module 6 and Testing Manager to Module 7). The
> app now has eight modules. See [README.md](README.md) and
> [CHANGELOG.md](CHANGELOG.md) for the current numbering.

Layered solution: `ClaudePM.Core` (models + interfaces), `ClaudePM.Services`
(SQLite stores, DPAPI key store, AI service, the five feature services),
`ClaudePM.App` (Avalonia UI), `ClaudePM.Tests` (xUnit).

## Roadmap after the build is green

Deferred v1.1 items, in rough priority order:
- An `IFilePickerService` so Documentation, Session Builder, and Settings get
  native folder/file browse dialogs instead of path textboxes.
- Drag-and-drop file staging in the Session Builder.
- FTS5-backed search in the Prompt Manager (currently in-memory filtering). [SHIPPED: v0.4]
- The redesign diff view and prompt version history (Module 2).
- The full streaming `tool_use` protocol for the Notebook (Module 4 currently
  uses a structured-JSON action proposal).
- Git-aware staleness detection in the Documentation Manager.

## Conventions

See `CLAUDE.md` — strict MVVM, compiled bindings, DI composition root, all AI
calls through `IChatClient`-style abstractions, all agent filesystem actions
gated by the preview/execute/undo flow in `AgentActionService`.

## Open questions

- None blocking. The app targets Windows for v1 (the API key store uses DPAPI);
  confirm before adding macOS/Linux support.
