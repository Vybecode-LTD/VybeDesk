# ClaudePM — Claude Project Manager

An AI-driven Windows desktop app that manages the full lifecycle of
Claude-Code-driven work: keeps project documentation reconciled with a
deterministic + AI-assisted audit, ships a searchable prompt library with
version history and AI redesign, builds Claude Code handoff packages from
claude.ai conversations, drives scoped filesystem actions through a
streaming `tool_use` agent, and manages `.skill` files for the wider
Claude ecosystem.

**Status:** v0.9 — feature-complete v0 with eight v1.1 polish items
already landed. Roadmap to v1.0 is in [ROADMAP.md](ROADMAP.md).

## Stack

Avalonia 11.3 · .NET 9 · CommunityToolkit.Mvvm · Microsoft.Data.Sqlite
(WAL + FTS5) · DiffPlex · DPAPI · Anthropic Messages API (direct, with
SSE streaming for the agent path)

## Solution layout

```
ClaudePM.sln
├── src/
│   ├── ClaudePM.Core/      Domain models + service interfaces. No framework deps.
│   ├── ClaudePM.Services/  SQLite stores, secure key store, AI service, agent.
│   └── ClaudePM.App/       Avalonia UI — Views, ViewModels, DI composition root.
└── tests/
    └── ClaudePM.Tests/     xUnit + NSubstitute (27 tests).
```

## Build & run

```pwsh
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ClaudePM.App
```

Requires the .NET 9 SDK (or 10, which still targets net9.0). Windows-only
for v0/v1 — the secure key store uses DPAPI. macOS / Linux key stores are
on the v1.1+ list.

First-time setup: launch the app → **Settings** → save your Anthropic API
key → **Projects** → register a folder. See [docs/USER_GUIDE.md](docs/USER_GUIDE.md)
for the full walkthrough.

## Documentation

| Document | Purpose |
|---|---|
| [docs/USER_GUIDE.md](docs/USER_GUIDE.md) | Module-by-module walkthrough — start here as a user. |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Technical overview — start here as a contributor. |
| [SPEC.md](SPEC.md) | Original product spec for the eight modules. |
| [ROADMAP.md](ROADMAP.md) | Forward-looking plan for v1.0. |
| [CHANGELOG.md](CHANGELOG.md) | Reverse-chronological history of what's shipped. |
| [CLAUDE.md](CLAUDE.md) | Running session context — read first when starting a new Claude Code session against this repo. |
| [KICKOFF.md](KICKOFF.md) | The original first-task prompt that bootstrapped this repo. |

## What's in v0.9

Eight modules in the sidebar:
- **Home** — read-only project list.
- **Projects** — register / edit / delete projects. Folder path is the scope
  root for AI agent actions.
- **Documentation** — scan project docs, run a structural pass (dead links,
  TODO markers, orphans, version drift, CLAUDE.md staleness, **Git-aware
  staleness**), an AI-driven doc-vs-doc semantic check, and a generated fix
  prompt for Claude Code.
- **Prompts** — searchable (SQLite FTS5) prompt library with `{{variable}}`
  templates, **AI Redesign with an inline colored diff**, and **version
  history** with restore.
- **Session Builder** — five-step wizard that turns a claude.ai
  conversation into a Claude Code handoff package, with drag-and-drop file
  staging.
- **AI Notebook** — streaming agent chat using Anthropic `tool_use`;
  three real tools (`create_file`, `create_folder`, `move`); all actions
  gated by preview / execute / undo within scoped project roots.
- **Skill Library** — browse / edit / validate / export `.skill` files.
- **Settings** — DPAPI-encrypted API key, model, default output path.

## Conventions

See [CLAUDE.md](CLAUDE.md) for the short version, [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
for the full version. Highlights:
- Strict layered architecture: Core ← Services ← App.
- MVVM via CommunityToolkit source generators; compiled bindings everywhere.
- AI calls go through `IAiService` — never the SDK or HTTP directly from
  a ViewModel.
- Any AI-initiated filesystem action MUST pass through preview / execute /
  undo and stay within scoped roots.
- API key stored via DPAPI (Windows-native); never on disk in plaintext;
  never in CLAUDE.md or git.

## Contributing

For now: read CLAUDE.md, read SPEC.md, then docs/ARCHITECTURE.md. Pick an
item off ROADMAP.md, follow the existing conventions, keep tests green,
update CLAUDE.md "Last Completed Task" at the end of the session.

A proper CONTRIBUTING.md lands with v1.0.
