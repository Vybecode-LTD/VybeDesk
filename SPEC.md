# ClaudePM — Specification

> Companion to CLAUDE.md. This is the feature + architecture spec for the
> Claude Project Manager desktop app.

## 1. Overview

ClaudePM is a cross-platform-capable (Windows-first) desktop app that acts as an
AI-driven project manager for Claude-based work. It keeps project documentation
reconciled, manages a reusable prompt library, builds Claude Code handoff
packages from claude.ai web sessions, provides an AI notebook that can take
filesystem actions, tracks bugs scoped to each registered project, and helps
pick a per-project testing strategy with generated Claude Code setup prompts.
(A Skill Library Manager existed through v0.24 and was removed in v0.25; it
will be rewritten from scratch post-v1.0.) Single-user today; architected so
it can become a commercial product.

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
    generation, prompt store.
  - `ClaudePM.App` — Avalonia Views/ViewModels, DI composition root, tray.
  - `ClaudePM.Tests` — xUnit + NSubstitute.

## 3. Central Model: Project

Top-level **Project** entity = a folder path + metadata (name, description,
status, last-activity). Modules 1, 3, 4, 5 (Bug Tracker), and 6 (Testing
Manager) operate within a selected project. Module 2 (Prompts) is **global**.
Home screen = project list with health indicators.

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

### Module 5 — Bug Tracker
- Project-scoped: every `Bug` belongs to exactly one `Project`; there is no
  global bug list. The tracker shows bugs for the currently-selected project.
- `Bug` entity fields: `Id`, `ProjectId`, `Title`, `Severity`
  (Critical/Major/Minor), `Status` (Open/Fixing/Fixed/WontFix),
  `StepsToReproduce`, `ExpectedResult`, `ActualResult`, `Area`, `Created`.
  The three reproduction fields are deliberately separate — the form
  structure teaches reproducible reporting.
- List sorts by severity (Critical → Major → Minor); within a severity,
  Open and Fixing rise above Fixed and WontFix; ties broken newest-first.
  The list answers "what should I fix next?" by reading top-down.
- Severity colour language reuses `SeverityToBrushConverter` (red/amber/blue)
  so the Documentation findings and the Bug Tracker speak the same visual
  language.
- **Generate Fix Prompt** command packs the multi-selected bugs (or all
  open bugs if none multi-selected) into a Claude Code prompt: each bug with
  its full reproduction trio, ordered by severity, with explicit instructions
  to make the smallest correct change per bug and to flag rather than guess
  if a bug cannot be reproduced.
- **Fixed-means-tested nudge**: when a bug's status transitions to Fixed,
  the status bar asks "Is there a test that would catch this bug if it
  returned?" The nudge performs no action — it is a teaching prompt and a
  lightweight stand-in for the future Testing Manager module.
- Out of scope for v0.26: screenshot attachments, per-bug activity history,
  direct bug-to-test linking.

### Module 6 — Testing Manager
- Project-scoped: each project has at most one `TestingPlan`. Absence means
  the questionnaire hasn't been run yet for that project.
- Externalizes a discipline that lives invisibly in an experienced developer's
  head: knowing what kind of testing a project needs, how to get it set up,
  and how to keep it running. The target user has no development background.
- **Strategy questionnaire** (5 plain-language multiple-choice questions:
  what are you building, what language/ecosystem, how important is
  correctness, who works on the code, does it touch external systems).
  Picks are RadioButton groups, not free text. Deliberately NOT a framework
  picker — that would presume knowledge the target user doesn't have.
- **Recommendation engine** (`StrategySelector`): pure function from
  `QuestionnaireAnswers` to a `StrategyRecommendation` (summary prose +
  ordered list of `TestKind`s + ordered list of framework names from the
  catalog). Includes reasoning the user can read before accepting.
- **Framework catalog** (built-in, NOT user data, NOT in DB): seven starter
  entries — xUnit (.NET), GoogleTest (C++), pytest (Python), Vitest (JS/TS),
  Jest (JS/TS, established alternative), React Testing Library (React
  components), Playwright (web E2E). Each catalog entry is a self-contained
  record with name, language token, supported `TestKind`s, and a Claude
  Code setup-prompt template. Adding a framework later means appending one
  data record — no logic edit.
- **Database testing is integration testing**, NOT a separate framework. The
  catalog, the strategy summary, and the setup-prompt templates all
  reinforce this convention. The module gently corrects the user if they
  expect a standalone "database test framework".
- **Setup prompt generation**: parameterized template per framework, with
  `{{ProjectName}}` and `{{ProjectPath}}` filled from the active project.
  Output to a read-only copyable panel.
- **Regression prompt generation**: takes the most recently fixed bug (from
  the Bug Tracker → Testing Manager event), or a generic prompt if no bug
  event is pending. Instructs Claude Code to write a test that fails on the
  pre-fix code and passes on the current code.
- **Bug Tracker ↔ Testing Manager coupling** is one tiny shared event —
  `IBugFixedNotifier` (in Core). Bug Tracker fires on Open/Fixing→Fixed
  transition. Testing Manager listens. Nothing else of either module's
  internals crosses the boundary.
- **Out of scope for v1**: test execution (no running `dotnet test` or
  parsing results), no red/green dashboard, no coverage metrics. Execution
  is the planned v2 flagship feature; the `TestingPlan` data model is the
  foundation it'll stand on. The strategy-selection skill explicitly warns
  against chasing coverage numbers — don't add them.

### (Deferred) Skill Library Manager
The original Module 5 shipped through v0.24 (browse/edit/dedupe/validate
skills in both flat `<name>.skill` and folder `<name>/SKILL.md` formats,
with dual-format export) and was removed in v0.25 after a stubborn
Resources/Validation display bug. The slot was reused for the Bug Tracker
in v0.26. A new Skill Library Manager will be designed and built from
scratch post-v1.0; see ROADMAP.md M6 and CHANGELOG.md v0.25 for context.

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
  descriptions. Originally written to power the removed Module 5; will be
  reused when Module 5 is rewritten post-v1.0.

## 7. Build Order

1. Build the four supporting skills (`.skill` files).
2. Scaffold the 4-project solution (DI, navigation, stub modules).
3. Implement modules in order: Settings/Project shell -> 2 -> 1 -> 4 -> 3 -> 5
   (Bug Tracker, landed v0.26) -> 6 (Testing Manager, landed v0.27).
   The previous Module 5 (Skill Library) was removed in v0.25; its rewrite
   is post-v1.0 work.
