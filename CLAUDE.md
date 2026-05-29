# CLAUDE.md — VybeDesk

> Context file. New sessions read this first. Keep "Last Completed Task" current.
>
> **STATE 2026-05-28 — v0.32 IN PROGRESS, NO OPEN BUGS.**
> 90+ uncommitted files; build green; 207/207 tests pass. Both the
> **cross-module project persistence bug** and the **layout regression
> bug** are RESOLVED (2026-05-28). See
> [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md) for the
> layout postmortem and
> [docs/PROJECT_PERSISTENCE_BUG.md](docs/PROJECT_PERSISTENCE_BUG.md)
> for the persistence postmortem.

## Last Completed Task
**VybeDesk rebrand (2026-05-28): Full project rename from ClaudePM
to VybeDesk — solution, all 4 projects (Core/Services/App/Tests),
namespaces, avares:// URIs, XAML, docs, design-system references,
DB/settings paths, brand assets (ICO, wordmark, brandmark). Build
green, 207/207 tests pass. Both bugs resolved (persistence + layout).**

Prior: v0.32 (in progress — 2026-05-28): Persistence bug FIXED,
"Choose a project" landing screens on all modules, Notebook
pagination, 7 new tests (168 total).

The 2026-05-25 session shipped M3 #10 / #11 + `edit_file` + Redo +
M4 #14 / #15 / #16 + M5 #17 (data + VM layers). The 2026-05-26
session attempted three persistence-bug fixes (all confirmed still
broken). The **2026-05-28 session** (Codex-audit-driven fix plan)
resolved the persistence bug and added UX improvements:

**Persistence bug — RESOLVED:**

The root cause was passive null writes from TwoWay ComboBox bindings
flowing through `ActiveProjectContext.SetCurrent(null)`. Every time
a ModuleHeader ComboBox initialized (or Projects collection was
cleared/rebuilt), null propagated through the binding chain → called
`SetCurrent(null)` → fired `Changed` → cleared every other module's
`SelectedProject`. The fix was a two-part rewrite:

1. **`ActiveProjectContext` rewritten** with idempotent, null-safe
   `SetCurrent`: `SetCurrent(null)` now no-ops (returns without
   clearing), `SetCurrent` with same project ID updates the reference
   but doesn't fire `Changed`, and explicit `ClearCurrent()` added
   for intentional clears. 3 new `ActiveProjectContextTests`.
2. **Per-module project isolation** — every project-scoped VM
   (`BugTracker`, `TestingManager`, `VisionAudit`, `Documentation`)
   retains `_reloadingProjects` flag + `_lastSelectedProjectId` for
   post-reload restore + null-write suppression in
   `OnSelectedProjectChanged` + `OnActivated()` override to restore
   from `_activeProjectContext.Current` on navigation.

See [docs/PROJECT_PERSISTENCE_BUG.md](docs/PROJECT_PERSISTENCE_BUG.md)
for the full postmortem (now marked RESOLVED).

**"Choose a project" landing overlays — all 6 modules:**

Every project-scoped module (Documentation, Prompts, Bug Tracker,
Testing Manager, Vision Audit, Notebook) now shows a full-screen
"Choose a project" overlay as the initial view before any project is
selected. Each overlay lists registered projects as clickable cards.
The Notebook variant includes an "All Projects" option. Z-order fix:
overlays render as the LAST child in their Grid (highest z-order)
with solid backgrounds and `FallbackValue=True` on `IsVisible`.

**Notebook pagination:**

The Notebook's landing overlay uses paginated project cards (4 per
page) instead of a ScrollViewer. Prev/Next buttons, page label,
automatic page clamping on project list changes.

### What was BLOCKED — Layout regression (RESOLVED 2026-05-28)

[docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md) is the
full postmortem. Root cause was H1: the Fluent theme's ContentControl
defaults `VerticalContentAlignment` to `Top`, measuring child
UserControls with unbounded (infinite) height. Fix:
`VerticalContentAlignment="Stretch"` on MainWindow's ContentControl
+ ScrollViewer wrappers in ProjectsView and HomeView. All four
prior failed patterns (Plans A-D) were attempting fixes inside the
UserControls, but the problem was always at the MainWindow level.

### v0.31 retrospective (kept for context)

**v0.31: Unified module header + sidebar submenu + Documentation
refit + app-wide scrollbar polish.** App-wide UI cohesion pass with
no functional/module additions. Every sidebar page now shares one
105px-tall header band, a sidebar TreeView replaces the flat ListBox
so the Skills row reveals Manager + Builder as nested children, the
Documentation tab is restructured to match the Skill Manager layout
shape (left rail + chips/actions toolbar + workspace), and every
ScrollViewer in the app is given a thin always-visible scrollbar
with a guaranteed 50px gap between content and the bar.

The header design landed in two passes (per user iteration). v1 was
a 78px ModuleHeader stacked over a separate 74px ModuleSubHeader; v2
collapsed both into a single 105px `ModuleHeader` with a 3-column
2-row layout — left = glyph + clickable-title + breadcrumbs +
description, middle = project picker (when applicable, `ShowPicker`
StyledProperty), right = Reset + Restart chips — plus a 25px
`#22222A` status sub-bar underneath. Module title IS the home link
(no separate icon); Reset + Restart are always visible but greyed
via `IsEnabled` when the VM hasn't overridden the corresponding
virtual. `ModuleSubHeader` was deleted in the v2 pass.

The scrollbar fix was the longest-iterated piece. `ScrollViewer.Padding`
doesn't produce a content-to-scrollbar gap in Avalonia 11.3 Fluent —
the template absorbs it into the content presenter without offsetting
the scrollbar. The working fix is `Margin="0,0,50,0"` on the
ScrollContentPresenter, set globally via a `Style Selector="ScrollViewer
/template/ ScrollContentPresenter#PART_ContentPresenter"` in
`App.axaml`. Combined with `AllowAutoHide="False"` (also a global
Style in App.axaml — Fluent's overlay-scrollbar default doesn't
reserve layout space) + `ScrollBar` Width/Height set to 8px, every
scrollable view now has the same thin always-visible scrollbar with
the same 50px content gap. Per-view ScrollViewer Padding was set as
part of the investigation and is now harmless / could be cleaned up
later; the gap comes from the global Style alone.

Build green; tests 92/92 (unchanged); sidebar now shows Skills as
an expandable parent with Manager + Builder children. No new
modules; no new VMs; no schema migrations.

### What shipped this turn

UI refresh, one logical change set across two layers.

**Core (PageViewModel):** added four `virtual` surfaces for the
unified header — `IReadOnlyList<string> Breadcrumbs` (default empty),
`IRelayCommand? GoModuleHomeCommand` / `ResetCommand` / `RestartCommand`
(default null), plus `IReadOnlyList<PageViewModel> Children` (default
empty) for the sidebar TreeView's hierarchical templates. All
overrides on concrete VMs alias existing `[RelayCommand]` methods to
avoid the source-generator collision rule (e.g. `public override
IRelayCommand? GoModuleHomeCommand => GoToFirstStageCommand;`).

**Controls:** new `Controls/ModuleHeader.axaml` (UserControl, 105px
fixed height, `x:DataType="vm:PageViewModel"` for the polymorphic
title/glyph/description/breadcrumbs/command bindings, plus
StyledProperties `ShowPicker` / `ProjectsSource` / `PickerSelectedItem`
/ `StatusMessage` for per-view picker + status binding via
`{Binding #Root.X}` ElementName syntax — `$parent[UserControl]`
resolves to the base UserControl type and fails AVLN2000). The
ephemeral `Controls/ModuleSubHeader.axaml(.cs)` that existed in v2
was deleted along with `Views/SkillSectionView.axaml(.cs)` (the old
in-pane Manager/Builder toggle).

**App (views):** all 11 sidebar pages reworked to use the unified
header. Project-dependent modules (Documentation, Notebook,
BugTracker, TestingManager, VisionAudit) set `ShowPicker="True"`
and bind the picker properties. Non-project modules omit those.
Notebook is the binding-name exception (`ActiveProject`, not
`SelectedProject`); Settings uses `Status` instead of `StatusMessage`.
DocumentationView was restructured to match SkillManagerView's
rail/workspace pattern. MainWindow's sidebar `ListBox` became a
`TreeView` with a polymorphic `TreeDataTemplate` rendering each
PageViewModel's `Children`. SkillSectionViewModel was refit to a
group-node role (no `ActivePage` / no toggle commands; just exposes
`Children = [Manager, Builder]`). `MainWindowViewModel` gained an
`OnCurrentPageChanged` interceptor that re-routes group-node
selections to their first child via `Dispatcher.UIThread.Post` —
clicking Skills auto-navigates to Manager.

**App-wide styles** (in `App.axaml`): `Style Selector="ScrollViewer"`
sets `AllowAutoHide="False"`; `Style Selector="ScrollBar:vertical"`
sets Width=8 + MinWidth=8 + MaxWidth=8 (matching `:horizontal`);
`Style Selector="ScrollViewer /template/ ScrollContentPresenter#PART_ContentPresenter"`
sets `Margin="0,0,50,0"`. These three Styles together produce the
uniform thin-always-visible-scrollbar-with-50px-gap behaviour.

**Tests:** unchanged 92/92. No new tests in this turn — the work is
visual/structural with no new logic to cover. The wizard reset/
restart semantics are reasonable VM tests for a future turn
(originally tracked as a Phase 8 item; deferred).

### Prior turn retrospective (v0.18 → v0.30)

Kept in CHANGELOG.md. Recent arc: v0.26 Bug Tracker → v0.27 Testing
Manager (Pattern C wizard + IBugFixedNotifier) → v0.28 Skills
module rebuilt (folder-only, v0.24 features re-added, UI polish) →
v0.29 Skill Builder module (Phase 2, shared validation, fourth
iteration of bounded-wizard-stages fix) → v0.30 Vision Audit +
persisted audit history. v0.31 is the cosmetic / chrome consolidation
that follows naturally after the eighth module — every page in the
app now reads as one design language.

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
`VybeDesk-skill-module/` into place: `SkillFile` (with `Resources`
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
Audit) AND the v0.31 chrome consolidation. **Next on deck: M3 #11
"Apply with AI" for fix prompts** — closes the
audit → fix-prompt → Notebook loop. The doc tab generates a fix
prompt today (in either the reconciliation or the project-audit
flow); the "Apply with AI" button feeds that prompt into the
Notebook against the current project root and lets the existing
preview/execute/undo gate handle the actual filesystem changes.
Small VM/view change, no schema migration, big UX win per ROADMAP.

Other remaining roadmap items (in rough priority):

- **M3** rest of the milestone — #10 persistent agent action log,
  #12 AI call log + cost tracking, #13 streaming token meter.
- **M4** (Real project hub) — import existing `.claude/` + git,
  Session Builder templates, per-project model/output overrides.
- **M5** (Landing dashboard + v1.0 polish) — Home health cards,
  light theme (proper `DynamicResource` migration — now easier
  given v0.31's app-wide style consolidation), bug-fix sweep.
- **M6** (Skill Library Builder roadmap entry) — partially
  delivered by the v0.29 Skill Builder; remaining items are bulk
  import + AI assist on description/body (Roadmap items 19–22).

Tier 2 of the non-roadmap bucket (theme dictionary,
`MarkdownPresenter` style resource, VM folders) still available.
NOTE: the handoff skill is named `cc-handoff` ("claude" is
reserved in skill names).

## Overview
VybeDesk is a cross-platform desktop app that acts as an AI-driven project
manager for Claude-based work. It helps keep project documentation reconciled,
manages reusable prompts, builds Claude Code repos from claude.ai web sessions,
and provides an AI notebook that can take file/folder actions. Currently
single-user; designed so it can become a commercial product.

## Architecture
Layered, strict one-directional dependencies (Core ← Services ← App):
- **VybeDesk.Core** — domain models, interfaces. No framework dependencies.
- **VybeDesk.Services** — AI client (Microsoft.Extensions.AI / Anthropic SDK),
  file scanning, doc analysis/reconciliation, repo generation, prompt store.
- **VybeDesk.App** — Avalonia 11 UI (Views/ViewModels), DI composition root in
  App.axaml.cs, system-tray integration.
- **VybeDesk.Tests** — xUnit + NSubstitute.

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
- Run: `dotnet run --project VybeDesk.App`
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
  app (`dotnet run --project src/VybeDesk.App`, background) and wait
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
- **HomeView + ProjectsView layout is RESOLVED (2026-05-28).** See
  [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md) for the
  postmortem. Root cause was the Fluent ContentControl defaulting
  `VerticalContentAlignment` to `Top` (infinite height measure).
  Fix: `VerticalContentAlignment="Stretch"` on MainWindow's
  ContentControl + ScrollViewer wrappers in both views.
- **Cross-module project persistence is RESOLVED (2026-05-28).** The
  `ActiveProjectContext` was rewritten with null-safe `SetCurrent` +
  explicit `ClearCurrent`. See
  [docs/PROJECT_PERSISTENCE_BUG.md](docs/PROJECT_PERSISTENCE_BUG.md)
  for the full postmortem. The fix is in place and smoke-tested.
- CommunityToolkit.Mvvm source generators require `partial` classes — missing
  `partial` silently breaks `[ObservableProperty]` / `[RelayCommand]`.
- Agent filesystem actions are scoped to user-configured project roots only;
  do not widen this scope without an explicit decision.

## Reference Docs
- @SPEC.md — full feature and architecture spec.
- @KICKOFF.md — historical bootstrap prompt (kept for archaeology).
- @HANDOFF.md — orientation packet for a new session (read second, after this).
- @ROADMAP.md — forward-looking milestones; v0.32-blocked items marked.
- @CHANGELOG.md — versioned history; v0.32 IN PROGRESS entry at top.
- @docs/PROJECT_PERSISTENCE_BUG.md — **RESOLVED postmortem (2026-05-28)**;
  cross-module project selection persistence. Documents root cause
  (passive null writes through TwoWay bindings) and the
  `ActiveProjectContext` rewrite that fixed it.
- @docs/LAYOUT_REGRESSION.md — **OPEN BUG postmortem**; required reading
  before editing any .axaml file affecting HomeView, ProjectsView, or any
  scrollable surface.
- @docs/TESTING.md — three-layer testing framework + smoke-test protocol
  + layout-regression-specific procedure + proposed headless test rig.
- @docs/USER_GUIDE.md — module-by-module walkthrough for users.
- @docs/ARCHITECTURE.md — technical reference (stack, layering, schemas).
- @docs/adr/ — architecture decision records (6 ADRs as of v0.31).
