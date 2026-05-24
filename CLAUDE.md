# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**v0.30: Vision Audit module (Module 8) + persisted audit history.**
The eighth user-spec-driven module ships. Externalises drift
detection: distil a project's vision from its docs, get the user
to approve it, then audit the project against that vision in either
a quick structural mode (shape only — folder/file names + dep
manifests + docs) or a deeper targeted mode (shape PLUS a bounded
set of the most relevant source files). Per-statement report ranks
each vision statement OnTrack / AtRisk / OffTrack with evidence and
a recommendation. Hands the deep code review off to Claude Code via
a generated deep-dive prompt — the structural audit cannot catch
behavioural drift inside correctly-named files.

Four-stage wizard (Extract → Approve → ChooseMode → RunReview)
using the per-stage bounded Grid pattern from
`memory/bounded-wizard-stages.md`. The Approve gate is **mandatory** —
nothing audits against an unapproved vision because an audit against
the wrong measuring stick is worse than no audit at all.

**Audit history** (user-requested addition, originally out-of-scope
in the spec): every successful audit run is persisted to
`audit_history` and surfaces as a list of cards on Stage 4 with
timestamp + mode + summary counts. Each card has **Open** (loads the
stored report markdown + deep-dive prompt verbatim into the report
panels above) and **🗑** (deletes that entry). **Clear all** wipes
the project's history. Entries are per-project; switching projects
reloads the right history. Reports/prompts are stored as text — no
re-paying for the AI call to re-read an old audit.

Mode picker uses plain-language phrasing per spec — the target
user shouldn't have to know "Structural" vs "Targeted" as bare
labels.

Build green; sidebar now 11 entries; tests 92/92 (73 prior +
6 SqliteVisionStoreTests + 6 VisionAuditServiceTests + 7 new
SqliteAuditHistoryStoreTests).

### What shipped this turn

Single-commit Vision Audit module + persisted audit history.

**Core:** `VisionRecord` + `VisionStatement` + `StatementVerdict`
record + `AlignmentRank` enum (OnTrack / AtRisk / OffTrack) +
`AuditMode` enum (Structural / Targeted) + `AuditReport` record +
`AuditHistoryEntry`. Two interfaces: `IVisionStore`
(one-record-per-project, upsert) and `IAuditHistoryStore` (per-project
append-only list, newest-first ordering, single-delete + clear-all).
Plus `IVisionAuditService` for the orchestration.

**Services:** `vision_records` + `audit_history` tables in
`Database.cs` (statements as JSON TEXT, verdicts as JSON TEXT for
faithful history re-render). `SqliteVisionStore` upserting via
`ON CONFLICT(project_id) DO UPDATE`. `SqliteAuditHistoryStore`
ordered by `generated_at DESC` with a `idx_audit_history_project`
index. `VisionAuditService` (in `Services/Vision/`) orchestrates the
four jobs: extract vision from docs (reusing
`IDocReconciliationService.ScanAsync`), structural audit (gathers
project shape — bounded depth + size caps), targeted audit
(two-phase: AI picks relevant files, capped at 10; then verdicts
with file contents), and `BuildReportMarkdown` / `BuildDeepDivePrompt`
for outputs. JSON parser surfaces actionable errors when Claude
replies conversationally.

**App:** `VisionAuditViewModel` (a `PageViewModel`) with a
`VisionAuditStage` enum state machine. Project picker, draft
statements collection (each wrapped in a `StatementEditViewModel`
for per-row Text + Remove), persisted-history collection that
reloads on project change, commands for every stage transition
plus history Open / Delete / ClearAll. `VisionAuditView` uses the
per-stage bounded Grid pattern from
`memory/bounded-wizard-stages.md`: each stage gets its own bounded
host with the long content in a `*` row and action buttons in
`Auto` rows. `SeverityToBrushConverter` extended to map
`AlignmentRank` to the same red / amber / blue palette as
`FindingSeverity` and `BugSeverity`. Sidebar entry between Testing
Manager and Settings; sidebar now 11 entries.

**Tests:** 6 `SqliteVisionStoreTests` (CRUD + project-scoping + upsert
+ nullable-ApprovedAt round-trip), 6 `VisionAuditServiceTests`
(approval-gate, no-statements-throws, extract parses, structural
audit fills missing verdicts with OffTrack, report markdown leads
with off-track items, deep-dive prompt names flagged items),
7 `SqliteAuditHistoryStoreTests` (add + newest-first ordering +
project-scoping + remove single + clear all + Changed event +
verdict-JSON round-trip).

### Prior turn retrospective (v0.18 → v0.29)

Kept in CHANGELOG.md; recent arc was: v0.27 Testing Manager (Pattern
C wizard + IBugFixedNotifier), v0.28 Skills module rebuilt
(folder-only + v0.24 features re-added + UI polish: TreeView, fixed-
size editor/viewer, app-wide button style), v0.29 Skill Builder
module (Phase 2 of Skills work, shared validation/serialization with
the Manager, fourth iteration of the bounded-wizard-stages layout
fix). The handoff section below covers what's still ahead.

### What shipped in the v0.29 turn that this commit builds on

Single-commit Skill Builder module (Phase 2). Three layers + one
late polish pass landed together as v0.29.

**Core:** `ISkillBuilderService` + DTOs (`SkillBuilderInputs`,
`QuestionAnswer`, `SkillEmitResult`). Process-oriented — no new
database table or store interface, as the spec explicitly called
out.
**Services:** `SkillBuilderService` (in `Services/Skills/`)
orchestrates the workflow. Two AI calls: one for clarifying
questions (when research toggle is ON), one for the draft.
Validation and serialization delegate to `ISkillLibraryService`
so the Builder and Manager share one source of truth. `EmitAsync`
writes both `.skill` and `<name>/SKILL.md` folder forms; refuses
to overwrite. JSON parsing now surfaces user-friendly errors
when the AI replies conversationally instead of in JSON.
**App:** `SkillBuilderViewModel` (a `PageViewModel`) with a
`BuilderStage` enum state machine (Inputs / Questions / Review /
Emitted) and `IncludeCancelCommand` on every async command.
`SkillBuilderView` uses per-stage bounded `Grid`s with the
canonical "long content in `*`, action row in `Auto`" wizard
pattern. The Builder is wired into `SkillSectionViewModel` via a
factory DI registration, so the Section's in-pane toggle bar
automatically lights up the Builder tab.
**Validation guardrails:** Stage 1 pre-flight (name format,
description length); Stage 2 all-blank-answers soft warning;
friendlier non-JSON AI-error messages in the service.
**Tests:** 4 new `SkillBuilderServiceTests` proving the shared-
validation requirement actually holds at runtime — anything the
Builder emits passes the Manager's validation identically.

### Prior session retrospective (v0.18 → v0.28)

Kept in CHANGELOG.md; the headline arc was: v0.18 safety hardening
(symlink resolution + 429/503/529 retry backoff), v0.19 Tier 1
close-out (audit JSON tests + UX micros + 5 ADRs), v0.20
smoke-test convention + Notebook bubble fix, v0.21–v0.22 Skill
Library Browse + dual-format export + Rename + chips + Resources
concept, v0.23 Anthropic prompt caching + M6 added to roadmap +
per-finding Copy, v0.24 doc maintenance + the bug catalog, v0.25
Skill Library module removed pending rewrite, v0.26 Bug Tracker
module, v0.27 Testing Manager module + Pattern C stepped wizard +
IBugFixedNotifier event, v0.28 Skills module rebuilt (folder-
format only, v0.24 features re-added, UI polish: TreeView with
nested resources, fixed-size editor/viewer, app-wide button style).
The Resources display bug consumed 9 layout iterations and was
the trigger for upgrading the smoke-test rule from "milestone
boundaries" to "every update", and ultimately drove the v0.25
delete-and-rewrite decision. v0.29 hit one more variation of the
same family (single-outer-ScrollViewer over IsVisible-toggled
stages → measure desync) and resolved it by giving each stage its
own bounded host.

### What shipped in the v0.28 turn that this commit builds on

Single-commit Skills module rebuild. Three layers — integration of
the delivered files, redirection to folder-format scanning, and a UI
polish pass — landed together as v0.28.

**Integration (Phase 1).** Copied the 12 delivered files from
`ClaudePM-skill-module/` into place: `SkillFile` (with `Resources`
list + `HasResources` flag), new `SkillResource` model,
`ISkillLibraryService` (added `PopulateResources` +
`ReadResourceAsync`), `SkillLibraryService` implementing them,
`SkillManagerViewModel` + `SkillSectionViewModel`,
`SkillManagerView` + `SkillSectionView`. The Section is the sidebar
page; the Manager is its first sub-page; an optional Builder slot
exists for Phase 2. Merged Program.cs DI registrations and
MainWindowViewModel constructor + Pages collection without
disturbing Bug Tracker / Testing Manager / Projects.

**Folder-only redirect + v0.24 features re-added (Phase 1.5).**
Replaced `ScanAsync`'s `*.skill` enumeration with a `*.md` walk that
keeps only files literally named `SKILL.md` (case-insensitive) —
modern Claude skill format. Flat `.skill` archives now ignored
entirely, fixing the `PK…` garbage / "(no name)" body symptoms.
Service gained `BackupAsync` (folder copy with timestamp suffix)
and `RenameAsync` (folder + frontmatter `name:` rename, with
collision check and format validation); `ExportAsync` rewritten to
duplicate the entire skill folder. VM gained Browse / Backup /
Export / Rename commands, per-skill issues inline, a global
findings filter view triggered by severity chips with per-finding
📋 Copy + click-to-jump-to-skill.

**UI polish (Phase 1.6).** Skill list replaced with a `TreeView`
(folder icons on skill nodes, file icons on resource children) —
the separate Supporting Resources panel is gone. Description box
fixed at 150px; viewer fixed at 300px (regardless of content).
Folder row collapsed to a single inline row: TextBox + 📁 Browse
button + 🔄 Scan button. App-wide button style added to
`App.axaml`: CornerRadius=6, Padding=12,5, FontSize=12 — applies
to every Button across every module. Outer container of
SkillManagerView is `DockPanel LastChildFill="True"` per the v0.27
bounded-ScrollViewer convention.
**Core:** new `TestingPlan` + `TestKind` (Unit / Integration /
Component / EndToEnd / ManualChecklist) + `QuestionnaireAnswers`
record + `ITestingPlanStore` (one-plan-per-project, upsert
semantics), plus the loose-coupling `IBugFixedNotifier` +
`BugFixedEvent` record.
**Services:** `testing_plans` table in `Database.cs` with
`UNIQUE(project_id)`; `SqliteTestingPlanStore` upserting via
`ON CONFLICT(project_id) DO UPDATE`; built-in
`TestingFrameworkCatalog` (7 starter entries: xUnit, GoogleTest,
pytest, Vitest, Jest, React Testing Library, Playwright) — each
entry self-contained so adding a framework later is one data
record, not a logic edit; `BugFixedNotifier` (simple in-memory
pub/sub); `StrategySelector` (pure function mapping
`QuestionnaireAnswers` → recommended `TestKind`s + framework
names + summary prose). Database testing IS integration testing
inside the language framework — never its own framework, by
design.
**App:** new `TestingManagerViewModel` + `TestingManagerView` as a
**data-driven stepped wizard (Pattern C)** — one question at a
time via a `ContentControl` bound to `CurrentQuestion`, with
Back / Next / See-recommendation navigation. New
`QuestionViewModel` + `QuestionOption` (reusable across future
wizards); each option's IsSelected is a per-option bool the
RadioButton binds one-way; the parent `PickCommand` sets the
selected token and syncs option flags. Three view states:
questionnaire / recommendation-review (Accept-and-save or
Re-answer) / saved-plan. **No ScrollViewer in the
questionnaire path** — eliminates the Avalonia
ScrollViewer-in-Grid-column height-constraint bug surfaced in the
first iteration of this module. (Patterns A and B documented as
alternatives in `docs/design-patterns/testing-manager-wizard-options.md`
if the wizard ever needs to pivot.) `BoolToBrushConverter` added
for progress-dot tinting. `BugTrackerView` outer Grid migrated to
DockPanel preemptively (same latent risk). DI registered, sidebar
entry between Bug Tracker and Settings.
**Cross-module hook:** `BugTrackerViewModel` now takes
`IBugFixedNotifier` and calls `Notify` on Open/Fixing→Fixed
transition. Testing Manager subscribes and surfaces a nudge banner
on the matching project's plan view. The two modules share ONLY
this event — nothing of each other's internals.
**Tests:** 6 `SqliteTestingPlanStoreTests` + 7
`TestingFrameworkCatalogTests` + 7 `StrategySelectorTests`.

### Prior session retrospective (v0.18 → v0.27)

Kept in CHANGELOG.md; the headline arc was: v0.18 safety hardening
(symlink resolution + 429/503/529 retry backoff), v0.19 Tier 1
close-out (audit JSON tests + UX micros + 5 ADRs), v0.20
smoke-test convention + Notebook bubble fix, v0.21–v0.22 Skill
Library Browse + dual-format export + Rename + chips + Resources
concept, v0.23 Anthropic prompt caching + M6 added to roadmap +
per-finding Copy, v0.24 doc maintenance + the bug catalog, v0.25
Skill Library module removed pending rewrite. The Resources
display bug consumed 9 layout iterations and was the trigger for
upgrading the smoke-test rule from "milestone boundaries" to
"every update", and ultimately for the v0.25 delete-and-rewrite
decision.

**Next:** All four user-authored specs from the working tree have
shipped (Bug Tracker → Testing Manager → Skill Builder → Vision
Audit). The remaining roadmap items are:

- **M3** (Smarter Notebook + telemetry) — #11 "Apply with AI" for
  documentation fix prompts is the smallest-scope highest-leverage
  item per HANDOFF; #10 persistent agent action log, #12 AI call
  log + cost tracking, #13 streaming token meter.
- **M4** (Real project hub) — import existing `.claude/` + git,
  Session Builder templates, per-project model/output overrides.
- **M5** (Landing dashboard + v1.0 polish) — Home health cards,
  light theme (proper `DynamicResource` migration), bug-fix sweep.
- **M6** (Skill Library Builder roadmap entry) — partially
  delivered by the v0.29 Skill Builder; remaining items there
  are bulk import + AI assist on description/body (Roadmap items
  19–22).

Tier 2 of the non-roadmap bucket (theme dictionary,
`MarkdownPresenter` style resource, VM folders) still available.
NOTE: the handoff skill is named `cc-handoff` ("claude" is
reserved in skill names).

## Overview
ClaudePM is a cross-platform desktop app that acts as an AI-driven project
manager for Claude-based work. It helps keep project documentation reconciled,
manages reusable prompts, builds Claude Code repos from claude.ai web sessions,
and provides an AI notebook that can take file/folder actions. Currently
single-user; designed so it can become a commercial product.

## Architecture
Layered, strict one-directional dependencies (Core ← Services ← App):
- **ClaudePM.Core** — domain models, interfaces. No framework dependencies.
- **ClaudePM.Services** — AI client (Microsoft.Extensions.AI / Anthropic SDK),
  file scanning, doc analysis/reconciliation, repo generation, prompt store.
- **ClaudePM.App** — Avalonia 11 UI (Views/ViewModels), DI composition root in
  App.axaml.cs, system-tray integration.
- **ClaudePM.Tests** — xUnit + NSubstitute.

MVVM via CommunityToolkit.Mvvm source generators. Compiled bindings (x:DataType)
everywhere. No INotifyPropertyChanged by hand; no new-ing ViewModels in
code-behind.

### Modules
1. Documentation Manager — scan/list/analyze project docs. Structural pass
   (local, no AI) + semantic pass (AI, doc-vs-doc only in v1). Emits a
   reconciliation report + a ready-to-paste Claude Code fix prompt; can draft
   missing docs via the preview/execute/undo flow.
2. Prompt Manager — store/tag/categorize prompts (SQLite + FTS5); `{{variable}}`
   templates; AI redesigns prompts (shown as diff, versioned); AI generates new
   prompts from a description.
3. Claude -> Claude Code Session Builder — wizard collects description,
   transcripts, and files; generates a HANDOFF PACKAGE (organized folder,
   CLAUDE.md, README, .gitignore, staged files) + a kickoff prompt. Does NOT
   write the app's code itself — Claude Code does that.
4. AI Notebook — conversational advice; saves notes; performs filesystem
   actions (create/move files & folders) via tool-calling, gated by
   preview/execute/undo and scoped to registered project roots.
5. Skills (rebuilt v0.28) — folder-format skill library (`<name>/SKILL.md`
   only; flat `.skill` ZIPs are explicitly unsupported). `SkillSectionViewModel`
   hosts an in-pane Manager/Builder toggle. Manager: TreeView with skill +
   nested resources, fixed-size editor and viewer, Browse/Scan icon row,
   Save/Rename/Backup/Export commands, severity chips with global findings
   filter view, per-finding 📋 Copy. Builder is the Phase 2 deliverable.
6. Bug Tracker — project-scoped defect log with severity-sorted list,
   three-field reproduction structure, and a Generate Fix Prompt command
   that packs selected bugs into a Claude Code prompt.
7. Testing Manager — project-scoped strategy chooser. Five-question
   plain-language questionnaire → `StrategySelector` recommendation →
   saved `TestingPlan`. Generates framework setup prompts (from a
   built-in 7-entry catalog) and regression-test prompts. Listens for
   `IBugFixedNotifier` events from the Bug Tracker to nudge regression
   testing after fixes.
8. Vision Audit — project-scoped drift detector. Distil a vision from
   docs, approve it, audit structurally (shape-only) or in targeted
   mode (shape + bounded set of source files). Per-statement
   OnTrack / AtRisk / OffTrack ranks with evidence + recommendation.
   Generates a Claude Code deep-dive prompt for line-level
   verification. Persisted audit history per project — every run is
   stored with its markdown report + deep-dive prompt verbatim.

## Build, Test, Run
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project ClaudePM.App`
- Publish (per platform): see `dotnet-installer-publishing` skill.

## Conventions
- ALWAYS keep this file's "Last Completed Task" current at the end of a session.
- All AI calls go through an `IChatClient` abstraction — never call the SDK
  directly from a ViewModel.
- Any AI-initiated filesystem action MUST go through the Preview → Execute →
  Undo pattern. No direct writes from an agent action.
- API keys are stored via OS-native secure storage (DPAPI / Keychain /
  libsecret). NEVER write keys to disk in plaintext or into this file.
- Long-running work (scans, generation) runs off the UI thread.
- Naming: Views end in `View`, ViewModels in `ViewModel`, services in `Service`.
- **Smoke test after EVERY update.** After every commit that changes
  user-visible behavior — every view edit, every VM-bound property,
  every new command, every layout tweak, every feature — launch the
  app (`dotnet run --project src/ClaudePM.App`, background) and wait
  for the user to visually verify before declaring done OR starting
  the next change. Doc-only and test-only commits exempt. Build-green
  + tests-green prove code correctness, not feature correctness. Tell
  the user explicitly *what to verify in THIS commit* (not generic
  "does everything still work"), then wait — don't queue the next
  change in the same turn. The v0.24 Resources bug saga is the
  cautionary tale: 9 layout iterations passed tests and burned the
  user's patience before the smoke-test rule was tightened from
  "milestone boundaries" to "every update" (and ultimately drove
  the v0.25 decision to delete and rewrite the module). See
  HANDOFF.md for the full protocol.

## Gotchas / Do Not Touch
- CommunityToolkit.Mvvm source generators require `partial` classes — missing
  `partial` silently breaks `[ObservableProperty]` / `[RelayCommand]`.
- Agent filesystem actions are scoped to user-configured project roots only;
  do not widen this scope without an explicit decision.

## Reference Docs
See @SPEC.md for the full feature and architecture spec.
See @KICKOFF.md for the first-task prompt (verify the build first).
<add further @imports here as deeper docs are created next to their code>
