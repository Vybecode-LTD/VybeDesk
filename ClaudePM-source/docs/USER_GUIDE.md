# ClaudePM User Guide

A walkthrough of the eleven sidebar pages, what each one does, and the typical
workflows they support. Pairs with [ARCHITECTURE.md](ARCHITECTURE.md) for the
"how it works under the hood" view. (Module numbers shuffled when Skills was
rebuilt in v0.28: Skills is Module 5, Bug Tracker is Module 6, Testing
Manager is Module 7, Vision Audit is Module 8.)

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
| **Skills** | Module 5 — folder-format Claude skill manager (rebuilt v0.28) | Global |
| **Bug Tracker** | Module 6 — project-scoped defect log + fix prompt | Per project |
| **Testing Manager** | Module 7 — testing strategy + setup/regression prompts | Per project |
| **Vision Audit** | Module 8 — drift detector + persisted history | Per project |
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

**Project Audit (synthesis pass, v0.17+):**
A third button — **Audit Project** — runs a Markdig-aware structured-JSON
call against a signal-weighted bundle of docs (CLAUDE.md → CHANGELOG →
ROADMAP → SPEC → README → KICKOFF → docs/ → rest). The result is a
**ProjectAuditReport** rendered as a full-pane overlay with five sections:
A) Project Design (synthesized prose, markdown-rendered), B) Roadmap —
all items (status / category / source / evidence), C) Completed,
D) Incomplete, plus an Inconsistencies block. The **Generate Fix Prompt**
button in the audit toolbar produces a Claude Code prompt covering the
inconsistencies (visible inline above the inconsistencies block — copy
it with the Copy button).

**Inline doc editor (v0.15+):**
Click any doc in the Documents list → the right pane swaps from findings
to a monospace text editor. **Save** writes back to disk; **Revert**
reloads from disk; **Close** returns to findings.

**Watch mode (v0.15+):**
Toggle the checkbox in the controls row to attach a `FileSystemWatcher`
to the project folder. Edits to `.md` / `.txt` (in any tool) trigger an
automatic debounced rescan ~750 ms after the file settles.

**Outputs:**
- A severity-ranked findings table (Critical / Warning / Info)
- A ready-to-paste **fix prompt** for Claude Code (Copy button on its
  header)
- An audit overlay with its own fix prompt for inconsistencies
- A full **markdown report** exportable to `RECONCILIATION_REPORT.md`

Typical workflow: pick a project → **Scan** → review findings → optionally
**Run AI Analysis** for doc-vs-doc semantic check → optionally **Audit
Project** for full state synthesis → **Generate Fix Prompt** → Copy →
paste into a Claude Code session.

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
- Responses render as Markdown (headings, fenced code blocks, tables,
  lists, bold / italic, inline code) via the custom `MarkdownPresenter`
  (since v0.16).
- Tool actions appear as small italic chips above the prose — green for
  success / read, red for blocked / failed.
- A **Copy** button at the top-right of each assistant bubble copies the
  full prose to clipboard.
- When the user clearly asks for a filesystem operation, Claude emits
  one or more `tool_use` blocks against five exposed tools:
  - Read-only, auto-executed (no preview gate):
    - `read_file(path)`
    - `list_directory(path)`
  - Approval-gated (preview / execute / undo):
    - `create_file(path, content)`
    - `create_folder(path)`
    - `move(path, destination_path)`
- The **Active project** dropdown in the sidebar narrows the agent's
  scope from "any registered project" to one chosen folder. Switching
  re-applies the scope without restarting.
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

**Notes (v0.17+):**
- **Save last response as note** captures the assistant's most recent
  message into a SQLite-backed note (title inferred from the first line).
- Click a note in the list → the body appears in a preview pane with
  three actions: **Insert into chat** (prepends the note as a `Reference
  (from saved note "X"):` block + `---` separator into the ChatInput,
  so your next message can build on the saved context), **Copy** (to
  clipboard), **Delete**.

## Bug Tracker (Module 5)

A project-scoped defect log. Every bug belongs to exactly one project, and
the tracker shows the currently-selected project's bugs only. The structure
of the form is the teaching: separate Steps to Reproduce / Expected /
Actual fields make you write down a reproducible report instead of a vague
description.

How to use:

- **Pick a project** from the dropdown at the top of the left rail. The
  three severity chips below it show how many Critical / Major / Minor
  bugs that project has.
- **New bug** creates a placeholder titled "Untitled bug" with Severity
  Major and Status Open. Fill in the editor on the right.
- **Severity** — Critical, Major, or Minor. The list sorts by severity
  (Critical first), and within a severity puts Open and Fixing bugs above
  Fixed and WontFix. Reading the list top-down answers "what should I
  fix next?".
- **Status** — Open → Fixing → Fixed / WontFix. When you save a status
  change to Fixed, the status bar nudges you: "Is there a test that would
  catch this bug if it returned?" — no action is taken; the prompt is a
  teaching nudge.
- **Steps to Reproduce / Expected / Actual** — three separate fields with
  generous vertical space. Don't paraphrase: a teammate (or the AI) must
  be able to follow the steps and see the discrepancy without asking
  follow-up questions.
- **Area** — short free-text saying *which screen or part of the app* the
  bug lives in (e.g. "Documentation tab", "Notebook chat"). Appears in
  the list row and the generated fix prompt.
- **Generate Fix Prompt** (button on the left rail) packs the selected
  bugs into a Claude Code prompt. If you've multi-selected bugs in the
  list, only those go in; otherwise the prompt includes every open bug.
  The output panel appears below the editor on the right with a Copy
  button — paste into a fresh Claude Code session against the project.

The fix prompt asks Claude Code to make the smallest correct change per
bug and to flag rather than guess if it can't reproduce — so the agent
won't ship a speculative fix.

## Testing Manager (Module 6)

A project-scoped testing strategy chooser. It externalizes a discipline
that lives invisibly in experienced developers' heads: knowing what kind
of testing your project needs, how to wire it in, and how to keep it
running. The questionnaire is deliberately NOT a framework picker —
picking "which framework?" before "what kind of testing?" puts the cart
before the horse.

How to use:

- **Pick a project** in the left rail. If the project has no saved
  strategy yet, the right pane shows a five-question questionnaire.
  If a strategy is already saved, you see a calmer "plan view" instead.
- **Answer all five questions.** Each is multiple-choice on purpose
  (your stack and circumstances, not a framework name). The
  recommendation appears below the questionnaire as soon as the last
  question is picked — no submit button needed.
- **Read the reasoning, then click "Accept and save".** The
  recommendation panel shows prose explaining *why* the strategy was
  chosen, the kinds of tests it calls for, and which frameworks the
  built-in catalog recommends. The full answers are stored alongside
  the conclusion so you can revisit them later.
- **Plan view (after Accept)** shows the saved strategy. Three buttons:
  - **Generate setup prompt** — builds a Claude Code prompt that wires
    the recommended frameworks into your project: folder layout, an
    example test that establishes the pattern, and (where relevant) a
    reminder that database testing belongs inside the language's
    integration tests, not as its own framework.
  - **Generate regression-test prompt** — builds a prompt asking Claude
    Code to write a regression test for a bug. If the Bug Tracker has
    recently fired a "bug fixed" event for this project, the prompt
    names that specific bug; otherwise it's a generic "for the most
    recent fixes" prompt that points at the Bug Tracker.
  - **Re-run questionnaire** — when your project's needs change. Your
    previous answers are pre-filled so you can tweak instead of
    starting over.
- **Bug-fixed nudge.** When a bug is marked Fixed in the Bug Tracker
  on this project, a blue banner appears on the Testing Manager's plan
  view: "A bug was just marked Fixed in this project: \"X\". Click
  'Generate regression-test prompt' to draft a test that would catch
  it if it returned." This is the only thing the Bug Tracker and the
  Testing Manager share — a single shared event in Core.

The setup prompts instruct Claude Code to establish the test folder
layout AND write one example test (not just install the framework) —
the example test is what establishes the pattern for everything that
follows.

The built-in framework catalog (v0.27): xUnit (.NET), GoogleTest (C++),
pytest (Python), Vitest (JS/TS), Jest (JS/TS, established alternative),
React Testing Library (React), Playwright (web E2E). Adding a framework
is one data record in
`src/ClaudePM.Services/Testing/TestingFrameworkCatalog.cs`, not a logic
edit.

**Out of scope for v1:** test execution and result dashboards. The
`TestingPlan` is the foundation; running tests is the planned v2
flagship feature.

## Skills (Module 5 — Manager rebuilt v0.28, Builder added v0.29)

Browse, view, edit, rename, back up, and export Claude skills — and
**design new ones from scratch** with AI assistance.

Skills must be in the modern folder format (`<name>/SKILL.md`) — flat
`*.skill` archive files are ignored on purpose because they're usually
ZIP packages that wouldn't parse as text.

The Skills section has an in-pane toggle at the top: **Skill Manager**
(browse/edit existing) and **Skill Builder** (design a new one). Both
halves share validation and serialization so a skill created in the
Builder is something the Manager understands identically.

### Skill Manager

How to use:

- **📁 Browse** to pick a folder containing your skills (e.g.
  `~/.claude/skills/`). The path TextBox accepts a pasted path too.
- **🔄 Scan** finds every `SKILL.md` file recursively under that folder
  and lists them as a tree on the left.
- Each skill node has a folder icon (📁) and is expandable — opening
  it reveals the skill's supporting resource files as nested 📄 children.
  Clicking a resource swaps the right-pane viewer to that resource's
  contents; click the parent skill (or hit **Show skill file**) to
  return to the skill body.
- **Edit** Name / Description in the right pane. Description has a
  fixed 150px height and shows a `N / 1024` budget counter. **Save**
  rewrites the SKILL.md frontmatter.
- **Rename** changes the skill folder name AND the `name:` field in
  the frontmatter, in sync. Format-validated (lowercase, hyphens, no
  "claude"), collision-checked.
- **Backup…** copies the entire skill folder to a destination you
  pick, named `<skillName>-backup-<timestamp>/`. Never overwrites.
- **Export…** duplicates the skill folder into a destination as
  `<skillName>/`. Fails clearly if that destination folder already
  exists — pick a different target or move the old one first.
- **Severity chips** at the top of the right pane show counts across
  every scanned skill (Critical / Warning / Info). Click any chip to
  swap the workspace to a global findings filter view: every finding
  of that severity, with **Open** (jump to owning skill) and **📋**
  (copy finding to clipboard) buttons per row. **← Back to skill
  editor** returns.

Per-skill validation findings render inline below the editor —
frontmatter present, name conventions, description trigger guidance,
body non-empty. Each finding has a colour-coded badge and its own 📋
Copy button.

### Skill Builder

Walks through designing a new skill in four stages — only one is
visible at a time, with a small progress strip in the header.

1. **Step 1 — Inputs.** Pick a name (lowercase-hyphen, e.g.
   `csv-import-helper`), write a rough description of what the skill
   does, optional notes. A checkbox below — **"Ask me clarifying
   questions first"** — toggles the optional Q&A pass. The app has
   no internet access, so this is interactive clarification, not web
   research.
   - **Pre-flight validation**: name must be ≥ 3 chars in the
     lowercase-hyphen format and not contain "claude" (reserved);
     description must be at least 40 chars. Below those thresholds
     the page shows what's missing instead of sending vague text to
     the AI.
2. **Step 2 — Clarifying questions** (only when the toggle is on).
   The AI returns 3–5 focused questions about intended triggers,
   scope, and what the skill should NOT do. Each question has a
   bounded answer box; the buttons (Back / Draft / Cancel) sit in a
   row that's always reachable at the bottom of the stage.
   - If you click **Draft the skill →** with every answer blank, the
     page warns you once — drafting from blank Q&A only uses your
     Stage 1 inputs and won't be sharper. Click Draft again to
     proceed anyway, or go back and fill some answers.
3. **Step 3 — Review and edit the draft.** The AI returns a polished
   routing description and a Markdown body. Both fields are editable.
   The validation findings panel uses the same colour-coded badges
   as the Manager — that's the shared validation contract. **Re-draft**
   asks the AI for another pass; **Apply edits** re-validates whatever
   you've typed.
4. **Step 4 — Emitted.** Click **Emit skill files…** to pick a target
   folder. The Builder writes BOTH a flat `<name>.skill` file
   (one-click add) AND a `<name>/SKILL.md` folder (the form that
   scales when you add resources later). Both contain byte-identical
   text. The page shows both paths with 📋 Copy buttons. **Build
   another skill** clears state and returns to Step 1.

If the AI replies in conversational prose instead of the expected
structured JSON (which can happen with vague inputs), the page
surfaces a user-actionable error — *"Make your description more
specific (what problem does the skill solve? what should trigger
it?), then click Re-draft."* — instead of an opaque JSON parse error.

## Vision Audit (Module 8 — v0.30)

Catches **drift** — the slow, invisible divergence where every individual
prompt-and-generate step seemed fine and the project quietly isn't what
you set out to build any more. Project-scoped: pick a project from the
header dropdown, then walk through the four stages.

### Step 1 — Extract a vision from the docs

The AI reads the project's docs (README, CLAUDE.md, SPEC.md, ROADMAP,
CHANGELOG, docs/*.md) and distils a draft vision — a list of concrete,
testable statements about what the project must do or be. This step
does NOT read source code, does NOT save anything, and does NOT audit
yet.

### Step 2 — Approve

Edit, add, or remove statements. Aim for **concrete, testable claims**
("users can save a project") — vague aspirations ("it should be good")
won't audit usefully. ✕ removes a statement; **+ Add statement** appends
a blank row.

**Approval is mandatory.** The audit refuses to run against an
unapproved vision — an audit against the wrong measuring stick is
worse than no audit at all. Once you click **Approve →**, the vision
saves to SQLite (one per project) and you advance to Step 3.

### Step 3 — Choose how deep the audit should go

Two cards with plain-language trade-offs:

- **Quick structural check** (recommended first time) — looks at the
  project's shape (folder/file names and dependency manifests) and the
  documentation. Fast, cheap, works at any project size. Catches the
  big drift (a vision promising user accounts in a project with no
  auth code anywhere). Cannot catch subtle behavioural drift inside
  correctly-named files.
- **Deeper targeted check** — does everything the quick check does,
  plus reads a bounded set (up to 10) of the source files the AI deems
  most relevant to your vision. More thorough, takes longer, costs
  more API budget.

### Step 4 — Run & review

Three summary chips (Off-track N / At-risk N / On-track N). Verdict
cards listed below — **off-track items lead**, those are the real
drift. Each card shows the rank badge, the statement text, the
factual evidence, and the recommendation. Below the cards:

- **Markdown report** — copyable, exportable verbatim.
- **Claude Code deep-dive prompt** — paste into a fresh Claude Code
  session against this project to verify the flagged areas at the
  code level. The structural audit cannot catch behavioural drift
  inside correctly-named files; the deep-dive is the line-level
  follow-up.
- **Audit history** (v0.30) — every successful audit run is
  persisted per-project with its markdown report + deep-dive prompt
  stored verbatim. Each entry card shows timestamp + mode + counts
  with **Open** (loads that entry's content back into the report
  panels) and **🗑** (deletes a single entry). **Clear all** at the
  top wipes the project's history. Entries persist across app
  restarts.

Run again with a different mode via **Run again (different mode)** —
the button keeps the saved vision and just re-runs. **Re-extract from
docs** restarts the whole flow if the project has fundamentally
changed since the vision was approved.

## Settings

- **Anthropic API Key** — paste / save / clear. Encrypted via DPAPI;
  Windows-only for v1. Rejects non-ASCII characters on save (catches
  rich-text paste typos that break Anthropic's header validator).
- **Model** — dropdown picker with current models (Opus 4.7, Sonnet 4.6,
  Haiku 4.5) + previous-gen (Opus 4.6, Sonnet 4.5, Opus 4.5) + legacy
  (Opus 4.1). Each tagged with tier + pricing per million tokens.
  Cost note: Opus 4.7 is `$5 / $25` per MTok; Sonnet 4.6 is `$3 / $15`
  (~1.7× cheaper); Haiku 4.5 is `$1 / $5` (~5× cheaper than Opus). A
  freeform textbox below the dropdown accepts custom IDs for preview
  models the dropdown hasn't been updated for.
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
