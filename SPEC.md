# ClaudePM — Specification

> Companion to CLAUDE.md. This is the feature + architecture spec for the
> Claude Project Manager desktop app.

## 1. Overview

ClaudePM is a cross-platform-capable (Windows-first) desktop app that acts as an
AI-driven project manager for Claude-based work. It keeps project documentation
reconciled, manages a reusable prompt library, builds Claude Code handoff
packages from claude.ai web sessions, provides an AI notebook that can take
filesystem actions, and manages the user's `.skill` file library. Single-user
today; architected so it can become a commercial product.

## 2. Stack & Architecture

- **UI**: Avalonia 11, .NET 9, CommunityToolkit.Mvvm (source generators),
  compiled bindings (`x:DataType`) everywhere.
- **AI**: Direct HTTPS to the Anthropic Messages API (no SDK) behind an
  `IAiService` abstraction. ViewModels never touch HTTP directly. SSE streaming
  + `tool_use` for Module 4; non-streaming `CompleteAsync` for everything else.
- **Persistence**: SQLite (projects, prompts, notes, AI-call log; FTS5 for
  search). App settings in JSON. API key in OS-native secure storage (DPAPI).
- **Layering** (strict one-directional: Core <- Services <- App):
  - `ClaudePM.Core` — domain models, interfaces. No framework deps.
  - `ClaudePM.Services` — AI client, file scanning, doc analysis, repo/handoff
    generation, prompt store, skill parsing.
  - `ClaudePM.App` — Avalonia Views/ViewModels, DI composition root, tray.
  - `ClaudePM.Tests` — xUnit + NSubstitute.

## 3. Central Model: Project

Top-level **Project** entity = a folder path + metadata (name, description,
status, last-activity). Modules 1, 3, 4 operate within a selected project.
Module 2 (Prompts) and Module 5 (Skills) are **global**. Home screen = project
list with health indicators.

## 4. Modules

### Module 1 — Documentation Manager
- Doc set: `.md`, `.txt`, `README*`, `CLAUDE.md`, `AGENTS.md`, `/docs` contents,
  ADRs. Configurable globs.
- File tree view + inferred logical ordering (README -> architecture -> setup ->
  API -> ADRs -> misc).
- **Structural pass** (local, no AI, free): dead internal links, broken file
  references, stale dates, version-string drift, orphaned docs, TODO/FIXME
  markers, CLAUDE.md "Last Completed Task" staleness.
- **Semantic pass** (AI, v1 = doc-vs-doc only): contradictions between docs.
  Chunked + summarized to control token cost. Doc-vs-code is deferred to v2.
- Output: in-app severity-ranked reconciliation report + markdown export + a
  generated ready-to-paste Claude Code fix prompt with file/line refs.
- AI can draft missing docs; all writes go through preview/execute/undo.

### Module 2 — Prompt Manager
- Schema: title, body, tags, category, notes, usage count, created/modified,
  favorite. Global library, FTS5 search + tag/category filter.
- `{{variable}}` templates — fill placeholders on use.
- AI redesign: optimize an existing prompt for Claude Code; shown as a diff;
  accept/reject; version history retained.
- AI prompt generator: user describes the need; AI produces a prompt.
  ASSUMED PENDING CONFIRMATION: interactive mode — AI asks clarifying questions
  before producing the final prompt.

### Module 3 — Claude -> Claude Code Session Builder
Wizard: Describe -> Transcripts (paste 1+ claude.ai conversations) -> Files
(drag-drop/picker into staging) -> Review checklist (AI flags likely-missing
items) -> Generate.
- **Decision B1**: "Generate" produces a HANDOFF PACKAGE — organized folder,
  CLAUDE.md, README, `.gitignore`, staged files placed sensibly, plus a
  generated kickoff prompt. The app does NOT write the project's code itself;
  Claude Code does the building. Output location from user preferences.

### Module 4 — AI Notebook
- Chat panel + saved notes (markdown, SQLite, tagged, optionally project-linked).
- Agent filesystem tools via tool-calling (`tool_use` blocks):
  - **Read-only, auto-executed**: `read_file`, `list_directory`.
  - **Approval-gated** (preview → user confirm → execute → log for undo):
    `create_file`, `create_folder`, `move`.
- All actions are scoped to registered project roots only.
- "Save last response as note" is a user-driven button on the Notebook UI, not
  an AI tool — saved notes can later be inserted back into a chat turn as
  grounded reference.

### Module 5 — Skill Library Manager
- Browse/edit/dedupe/validate skills in either format: legacy flat
  `<name>.skill` files OR Claude Code's modern `<name>/SKILL.md` folder
  layout (the structure under `~/.claude/skills/`). Validates frontmatter,
  description length (<1024 chars), trigger quality. Export writes both
  formats side-by-side so the same skill loads in Claude Code (folder) and
  Claude web (flat file) without manual conversion.

## 5. Cross-Cutting

- Settings: API key, model picker, default output path, project roots, theme.
- AI-call cost/token log.
- Dark mode default. System-tray presence (one-click Notebook).
- Project health dashboard; CLAUDE.md auto-maintainer (updates "Last Completed
  Task" across projects).

## 6. Supporting Skills (to build before scaffolding)

- `cc-handoff` — turning a claude.ai web project into a Claude Code
  repo/handoff package. Powers Module 3. (Named `cc-handoff` because "claude"
  is a reserved word in skill names.)
- `doc-reconciliation` — heuristics for detecting stale/contradictory docs.
  Powers Module 1.
- `desktop-ai-agent-actions` — safe pattern for AI-initiated filesystem actions
  (scoped roots, allow-lists, dry-run, undo log). Powers Module 4.
- `skill-file-authoring` — writing well-formed `.skill` files with good trigger
  descriptions. Powers Module 5.

## 7. Build Order

1. Build the four supporting skills (`.skill` files).
2. Scaffold the 4-project solution (DI, navigation, stub modules).
3. Implement modules in order: Settings/Project shell -> 2 -> 1 -> 4 -> 3 -> 5.
