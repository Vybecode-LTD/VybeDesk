# ClaudePM — Claude Project Manager

An AI-driven Windows desktop app that manages the full lifecycle of
Claude-Code-driven work: keeps project documentation reconciled with a
deterministic + AI-assisted audit, ships a searchable prompt library with
version history and AI redesign, builds Claude Code handoff packages from
claude.ai conversations, drives scoped filesystem actions through a
streaming `tool_use` agent, and manages `.skill` files for the wider
Claude ecosystem.

**Status:** v0.24 — Milestones 1, 2, and 2.5 of the v1.0 roadmap shipped,
plus a wide non-roadmap polish pass: safety hardening (symlink-safe
agent scope, 429/503/529 retry backoff), Anthropic prompt caching on
system + tools, end-to-end Skill Library overhaul (Browse picker, scan
both `.skill` files and `<name>/SKILL.md` folders, rename, dual-format
export, clickable severity chips, per-finding Copy), Notebook UX micros
(thinking placeholder, Ctrl+Enter send, Ctrl+S save), and six ADRs
under [docs/adr/](docs/adr/README.md). M3 / M4 / M5 / M6 still planned.
Snapshot tag `AlphaV0.5.0` marks the end of Milestone 1. See
[CHANGELOG.md](CHANGELOG.md) for the versioned history,
[ROADMAP.md](ROADMAP.md) for what's still ahead, and
[HANDOFF.md](HANDOFF.md) for a known critical bug in the Skill Library
Resources display.

## Stack

Avalonia 11.3 · .NET 9 · CommunityToolkit.Mvvm · Microsoft.Data.Sqlite
(WAL + FTS5) · DiffPlex · Markdig · DPAPI · Anthropic Messages API
(direct, with SSE streaming for the agent path)

## Solution layout

```
ClaudePM.sln
├── src/
│   ├── ClaudePM.Core/      Domain models + service interfaces. No framework deps.
│   ├── ClaudePM.Services/  SQLite stores, secure key store, AI service, agent.
│   └── ClaudePM.App/       Avalonia UI — Views, ViewModels, DI composition root.
└── tests/
    └── ClaudePM.Tests/     xUnit + NSubstitute (53 tests).
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
key + pick a model (Sonnet 4.6 is a good cost/quality default) → **Projects**
→ register a folder. See [docs/USER_GUIDE.md](docs/USER_GUIDE.md) for the
full walkthrough.

## Documentation

| Document | Purpose |
|---|---|
| [docs/USER_GUIDE.md](docs/USER_GUIDE.md) | Module-by-module walkthrough — start here as a user. |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Technical overview — start here as a contributor. |
| [SPEC.md](SPEC.md) | Original product spec for the eight modules. |
| [ROADMAP.md](ROADMAP.md) | Forward-looking plan for v1.0. |
| [CHANGELOG.md](CHANGELOG.md) | Reverse-chronological history of what's shipped. |
| [HANDOFF.md](HANDOFF.md) | Handoff package for a new Claude Code session agent picking this up cold. |
| [CLAUDE.md](CLAUDE.md) | Running session context — read first when starting a new Claude Code session against this repo. |
| [KICKOFF.md](KICKOFF.md) | Historical: the original first-task prompt that bootstrapped this repo. |

## What's currently in v0.17

Eight modules in the sidebar:

- **Home** — read-only project list.
- **Projects** — register / edit / delete projects with native folder
  picker. Each project has an **Open in Claude Code** button that launches
  the CLI with the folder as cwd (or copies the command to clipboard if
  `claude` isn't on PATH).
- **Documentation** — scan a project's docs, run a structural pass
  (dead links, TODO markers, orphans, version drift, CLAUDE.md staleness,
  **Git-aware staleness**), an AI-driven doc-vs-doc semantic check, and
  the new **Project Audit** synthesis pass (design summary + roadmap
  items complete / incomplete + cross-doc inconsistencies, with its own
  fix-prompt generator). Inline doc editor in the right pane when you
  click a doc. **Watch mode** auto-rescans on file changes.
- **Prompts** — searchable (SQLite FTS5) library of 30+ curated prompts
  across 5 categories (doc/VCS hygiene, testing, efficient task
  execution, session starters, common dev tasks). `{{variable}}`
  templates, AI Redesign with inline colored diff, version history with
  restore, per-row Copy button.
- **Session Builder** — five-step wizard that turns a claude.ai
  conversation into a Claude Code handoff package, with drag-and-drop
  file staging and an optional AI review step.
- **AI Notebook** — streaming agent chat using Anthropic `tool_use`.
  Five tools: `read_file` + `list_directory` (auto-executed, read-only)
  plus `create_file` + `create_folder` + `move` (preview / execute /
  undo, scoped to the active project's folder). One-bubble-per-turn
  with tool-activity chips above prose; markdown-rendered responses
  (headings, code blocks, tables); per-message Copy button. Notes
  sidebar lets you save a Claude response and later **Insert into chat**
  as grounded reference.
- **Skill Library** — browse / edit / validate / export `.skill` files.
- **Settings** — DPAPI-encrypted API key (rejects non-ASCII paste typos),
  Claude model picker (Opus 4.7 / Sonnet 4.6 / Haiku 4.5 + legacy
  models, each with tier + pricing hint), default output path, Cancel
  button on every long-running AI call.

## Conventions

See [CLAUDE.md](CLAUDE.md) for the short version,
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full version.
Highlights:

- Strict layered architecture: Core ← Services ← App.
- MVVM via CommunityToolkit source generators; compiled bindings everywhere.
- AI calls go through `IAiService` — never the SDK or HTTP directly from
  a ViewModel.
- Any AI-initiated filesystem action MUST pass through preview / execute /
  undo and stay within scoped project roots.
- API key stored via DPAPI (Windows-native); never on disk in plaintext;
  never in CLAUDE.md or git.

## Contributing

For now: read [HANDOFF.md](HANDOFF.md), then [CLAUDE.md](CLAUDE.md),
[SPEC.md](SPEC.md), then [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
Pick an item off [ROADMAP.md](ROADMAP.md), follow the existing
conventions, keep tests green, update CLAUDE.md "Last Completed Task" at
the end of the session.

A proper CONTRIBUTING.md lands with v1.0.
