# ClaudePM User Guide

A walkthrough of the eight sidebar pages, what each one does, and the typical
workflows they support. Pairs with [ARCHITECTURE.md](ARCHITECTURE.md) for the
"how it works under the hood" view.

## First-time setup

1. Launch the app — `dotnet run --project src/ClaudePM.App` from the repo
   root, or run the built executable.
2. Open **Settings** → paste your Anthropic API key → **Save Key**. The key
   is stored encrypted at rest via Windows DPAPI; it never lands in plain
   text on disk and isn't checked into version control.
3. Open **Projects** → **New project** → fill in name, description, and
   click **Browse…** to point at a real folder on your machine. **Save**.

You're set. The AI-driven features (Documentation semantic check, Prompt
Redesign / Generate, Session Builder review, Notebook chat) need the key
and project to be useful.

## Sidebar at a glance

| Tab | Module | Scope |
|---|---|---|
| **Home** | Read-only project list | Global |
| **Projects** | Register / edit / delete projects | Global |
| **Documentation** | Module 1 — scan, reconcile, fix docs | Per project |
| **Prompts** | Module 2 — prompt library + AI redesign | Global |
| **Session Builder** | Module 3 — claude.ai → Claude Code handoff | Per output |
| **Notebook** | Module 4 — chat agent with scoped file actions | Per project |
| **Skill Library** | Module 5 — edit/validate `.skill` files | Global |
| **Settings** | API key, model, default output path | Global |

## Home

Currently a read-only list of registered projects. Each card shows name,
description, and folder path. Coming in v1.0 (M5): per-project health cards
with stale-doc count, recent commits, pending actions, and last activity.

## Projects

CRUD over your project registry. A **project** is the unit of scope for
Modules 1 (Documentation), 3 (Session Builder output location), and 4
(Notebook agent action roots).

- **New project** creates a placeholder; fill in the editor on the right.
- **Name** is the display label.
- **Folder path** is the scope root — the Notebook's `create_file` /
  `create_folder` / `move` actions are *only* allowed under this path (and
  paths of other registered projects). Use **Browse…** to pick natively.
- **Status** is purely informational today (Active / OnHold / Archived).

Changes propagate live to other tabs — adding a project here immediately
shows up in the Documentation project dropdown and the Notebook scoped
roots, no restart needed.

## Documentation (Module 1)

Audits a project's docs for staleness and inconsistency. Two passes:

**Structural pass — local, deterministic, no AI cost:**
- Dead internal links (markdown `[foo](missing.md)`)
- TODO / FIXME / WIP / DRAFT markers
- Orphaned docs (`.md` files not linked from any other doc, except
  README / CLAUDE / AGENTS / INDEX)
- Version-string drift (multiple `version: X.Y.Z` mentions disagreeing)
- Missing canonical docs (no README, no CLAUDE.md/AGENTS.md)
- CLAUDE.md staleness via filesystem mtime relative to other docs
- **Git-aware staleness** (since v0.9): when the folder is a Git repo,
  docs that lag the project's most recent commit by ≥ 60 days raise a
  *Warning*; docs with FS mtime newer than the last commit raise an
  *Info* "Uncommitted changes"; docs with no commits at all raise an
  *Info* "Untracked doc".

**Semantic pass — AI-driven, doc-vs-doc only:**
- Sends a curated bundle of doc content to Claude; surfaces contradictions
  between documents. Doc-vs-code is deferred to v1.1+.

**Outputs:**
- A severity-ranked findings table (Critical / Warning / Info)
- A ready-to-paste **fix prompt** for Claude Code (will get an "Apply
  with AI" path in v1.0)
- A full **markdown report** exportable to `RECONCILIATION_REPORT.md`

Typical workflow: pick a project → **Scan** → review findings → optionally
**Run AI Analysis** for semantic check → **Generate Fix Prompt** → copy
into a Claude Code session.

## Prompts (Module 2)

A searchable, taggable, version-controlled prompt library. Global, not
per-project.

**Browse & search:**
- Left pane lists all prompts, ordered by last modified.
- The search box is FTS5-backed (since v0.4) — tokens become prefix
  matches across title, body, and tags. Operators in your input are
  sanitized; you can't accidentally break the query.
- Category dropdown filters to a subset.

**Edit:**
- Title, Category, comma-separated Tags, Body.
- Body supports `{{variable}}` placeholders — see *Fill Template*.

**Fill Template:**
- Extracts `{{name}}` placeholders from the body, prompts you for each,
  and produces the final filled text in a read-only output box.

**AI Redesign:**
- Sends the current body to Claude with a "make this maximally effective
  for Claude Code" system prompt.
- The result appears as an **inline colored diff** vs the current body
  (green = additions, red = removals).
- **Apply & Save** writes the redesign and persists in one click;
  **Apply to editor only** loads it for further tweaking; **Dismiss**
  throws it away.

**History:**
- Every content-changing save (title / body / category / tags) snapshots
  the *prior* row into a `prompt_versions` table. (Usage-count-only
  updates don't pollute the history.)
- Click **History** → see prior versions newest-first with timestamps and
  a 3-line body excerpt; click **Restore** to load a version back into
  the editor (then Save to commit it as a new version, creating a new
  snapshot of the now-overwritten version).

## Session Builder (Module 3)

Turns a claude.ai web conversation into a Claude Code handoff package — an
organized folder with `CLAUDE.md`, `README.md`, `.gitignore`, `KICKOFF.md`,
your transcripts under `docs/transcripts/`, and any files you staged.

Five-step wizard:

1. **Describe** — project name, description, stack, output location.
   Browse for the output folder natively.
2. **Transcripts** — paste each claude.ai conversation that shaped the
   project. Each becomes a file under `docs/transcripts/`.
3. **Files** — drag and drop, browse, or type paths to stage files
   alongside the generated package. Drop zone shows "Drop files here"
   when empty; status line summarizes added / duplicate / missing.
4. **Review** *(optional)* — runs an AI handoff review against the plan
   so far and flags likely-missing items.
5. **Generate** — writes the package to the output folder.

The app does *not* write the project's code — Claude Code does that, using
the generated `KICKOFF.md` prompt against the new folder.

## Notebook (Module 4)

A conversational agent with scoped filesystem actions, gated by validate /
execute / undo.

**Chat flow:**
- Type a message → assistant text streams in token-by-token via Anthropic
  SSE streaming (since v0.8).
- When the user clearly asks for a filesystem operation, Claude emits
  one or more `tool_use` blocks against the three exposed tools:
  - `create_file(path, content)`
  - `create_folder(path)`
  - `move(path, destination_path)`
- Each tool call lands in the **Proposed actions** pane as a row with a
  human-readable description and a status (Ready / Blocked: …).
- **Execute All** runs each through `AgentActionService` → posts
  `tool_result` blocks back to Claude → continues the turn.
- **Clear** cancels pending actions, synthesizing `is_error=true`
  tool_results so the conversation history stays consistent.
- **Undo Last** reverses the most recent executed action (file delete,
  folder delete, move-back).

**Safety:**
- "Sandbox roots" lists the folder paths of all registered projects.
  Action paths must canonicalize inside one of those roots — anything
  outside raises a Blocked validation.
- `Path.GetFullPath` collapses `.`/`..` traversal. Symlink resolution is
  a v1.1+ hardening step.

**Notes:**
- **Save last response as note** captures the assistant's most recent
  message into a SQLite-backed note (title inferred from the first line).
- **Delete selected note** removes one.

## Skill Library (Module 5)

Browse, edit, validate, and export `.skill` files (the YAML-frontmatter +
markdown-body format used by Claude Code skills).

- **Scan** a folder for `.skill` files; results land in the list.
- Click one → edit Name, Description, Body.
- Validation runs continuously: description must be under 1024 chars,
  must have trigger phrases, name must match conventions, etc.
- Issues appear in the right pane with severity icons.
- **Save** updates in place; **Export .skill** writes a clean `.skill`
  file with normalized frontmatter.

## Settings

- **Anthropic API Key** — paste / save / clear. Encrypted via DPAPI;
  Windows-only for v1.
- **Model** — model ID string (default: `claude-opus-4-7`).
- **Default output path** — used by Session Builder as the initial value
  for "Output location" in Step 1.

## Troubleshooting

**Notebook says "No Anthropic API key is configured."** — open Settings,
save a key. The Notebook reads the key on every call, so no app restart
is needed.

**Notebook action says "Blocked: Path is outside all scoped project roots."** —
the path you asked Claude to write to isn't under any registered project.
Open the Projects tab, add or edit a project so its folder path covers the
location you want. Live-update — no restart needed.

**Documentation Scan shows "Folder not found."** — the path doesn't exist
on disk. Browse to a real folder.

**Build fails with `Grid.RowSpacing` or `Grid.ColumnSpacing` errors** —
make sure the `Avalonia` packages are at 11.3.0 or newer (the Grid spacing
properties were added in 11.3).

**Database migrations seem broken / I want to start fresh** — delete the
file at `%LOCALAPPDATA%\ClaudePM\claudepm.db` and relaunch. The schema
re-creates and the seed prompt + project re-populate.

**I see DPAPI errors on macOS / Linux** — v1 is Windows-only because the
secure key store uses Windows DPAPI. macOS Keychain / Linux libsecret
implementations are on the v1.1+ roadmap.
