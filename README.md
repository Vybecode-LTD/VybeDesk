# ClaudePM — Claude Project Manager

An AI-driven Windows desktop app that manages the full lifecycle of
Claude-Code-driven work: keeps project documentation reconciled with a
deterministic + AI-assisted audit, ships a searchable prompt library with
version history and AI redesign, builds Claude Code handoff packages from
claude.ai conversations, drives scoped filesystem actions through a
streaming `tool_use` agent, tracks project-scoped bugs with a severity-
sorted list and a Generate Fix Prompt command, and picks per-project
testing strategies with generated Claude Code setup and regression
prompts.

**Status:** v0.29 — Milestones 1, 2, and 2.5 of the v1.0 roadmap shipped,
plus a wide non-roadmap polish pass through v0.24, plus four user-
authored-spec-driven modules: Bug Tracker (v0.26), Testing Manager
(v0.27), the rebuilt Skills Manager (v0.28), and the Skill Builder
(v0.29 — Phase 2 of the Skills work, lives inside the Skills section
as a Manager/Builder in-pane toggle). Highlights: safety
hardening (symlink-safe agent scope, 429/503/529 retry backoff),
Anthropic prompt caching on system + tools, Notebook UX micros (thinking
placeholder, Ctrl+Enter send, Ctrl+S save), and six ADRs under
[docs/adr/](docs/adr/README.md). **v0.25 removed the previous Module 5
(Skill Library Manager)** pending a clean-slate rewrite; **v0.26 took the
Module 5 slot for the Bug Tracker**; **v0.27 added the Testing Manager**
with a loosely-coupled bug-fixed event; **v0.28 brought the Skills module
back** (folder-format only, v0.24 features re-added, TreeView + fixed-
size editor + app-wide button style) — reshuffling the modules so
Skills is Module 5 again, Bug Tracker is Module 6, Testing Manager is
Module 7. M3 / M4 / M5 / M6 still planned. Snapshot tag `AlphaV0.5.0`
marks the end of Milestone 1. See [CHANGELOG.md](CHANGELOG.md) for the
versioned history, [ROADMAP.md](ROADMAP.md) for what's still ahead.

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
    └── ClaudePM.Tests/     xUnit + NSubstitute.
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

## What's currently in v0.29

Ten modules in the sidebar (Skills as Module 5, rebuilt v0.28; Skill Builder
sub-page added v0.29; Bug Tracker as Module 6 since v0.26; Testing Manager
as Module 7 since v0.27). App opens **Maximized** on startup.
Anthropic prompt caching is enabled on every API call (see
[ADR-0006](docs/adr/0006-prompt-caching-on-system-and-last-tool.md)).

- **Home** — read-only project list.
- **Projects** — register / edit / delete projects with native folder
  picker. Each project has an **Open in Claude Code** button that launches
  the CLI with the folder as cwd (or copies the command to clipboard if
  `claude` isn't on PATH).
- **Documentation** — scan a project's docs, run a structural pass
  (dead links, TODO markers, orphans, version drift, CLAUDE.md staleness,
  **Git-aware staleness**), an AI-driven doc-vs-doc semantic check, and
  the **Project Audit** synthesis pass (design summary + roadmap items
  complete / incomplete + cross-doc inconsistencies, with its own
  fix-prompt generator). Inline doc editor in the right pane (`Ctrl+S` to
  save). **Watch mode** auto-rescans on file changes.
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
  undo, scoped to the active project's folder, symlink-resolved).
  One-bubble-per-turn with tool-activity chips above prose;
  markdown-rendered responses (headings, code blocks, tables);
  per-message Copy button; italic "thinking…" placeholder while
  awaiting first delta; `Ctrl+Enter` to send. Notes sidebar lets you
  save a Claude response and later **Insert into chat** as grounded
  reference.
- **Skills** — folder-format Claude skill manager (rebuilt v0.28; the
  v0.24 Skill Library lived through commit `16f9468` then was deleted
  in v0.25 — current implementation is a clean rebuild from a user-
  delivered package + customisations). Scans recursively for
  `<name>/SKILL.md` files; flat `.skill` archives are deliberately
  ignored (they're usually ZIPs). Left rail has a TreeView of skills
  with expandable nested resource files. Right pane has fixed-size
  editor (150px description, 300px viewer), Save / Rename (folder +
  frontmatter in sync) / Backup (timestamped folder copy) / Export
  (folder duplication). Severity chips at the top show
  Critical/Warning/Info counts; clicking a chip opens a global
  findings filter view with per-finding 📋 Copy and click-to-jump-to-
  skill. The Section's in-pane toggle is set up for a forthcoming
  **Skill Builder** sub-page (Phase 2).
- **Bug Tracker** — project-scoped defect log built from
  [docs/build-prompts/bug-tracker.md](docs/build-prompts/bug-tracker.md).
  Severity-sorted list (Critical → Major → Minor; within a severity,
  Open and Fixing rise above Fixed and WontFix) so the list answers
  "what should I fix next?" by reading top-down. Editor keeps Steps to
  Reproduce / Expected / Actual as three distinct labeled fields so the
  form teaches reproducible reporting. Per-severity summary chips on top.
  **Generate Fix Prompt** packs selected bugs (or all open bugs if none
  selected) into a Claude Code prompt — full reproduction trio per bug,
  severity-ordered, with explicit instructions to make the smallest
  correct change and to flag-rather-than-guess if a bug can't be
  reproduced. Fixed-means-tested nudge on status transition to Fixed.
  (Skill Library — the previous Module 5 — was removed in v0.25; see
  [CHANGELOG.md](CHANGELOG.md) v0.25 and [ROADMAP.md](ROADMAP.md) M6.)
- **Testing Manager** — project-scoped testing strategy chooser built from
  [docs/build-prompts/testing-manager.md](docs/build-prompts/testing-manager.md).
  Five-question plain-language questionnaire (deliberately NOT a framework
  picker) → recommendation with reasoning → saved `TestingPlan`. The
  plan view generates two kinds of Claude Code prompts: framework setup
  (drawn from a built-in catalog of 7 frameworks — xUnit, GoogleTest,
  pytest, Vitest, Jest, React Testing Library, Playwright) and regression
  tests (driven by a loosely-coupled `IBugFixedNotifier` event from the
  Bug Tracker — fire-and-forget when a bug is marked Fixed). Database
  testing is folded into integration tests within the language framework
  by design, not its own framework.
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
