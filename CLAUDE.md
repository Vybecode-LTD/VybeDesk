# CLAUDE.md — ClaudePM (Claude Project Manager)

> Context file. New sessions read this first. Keep "Last Completed Task" current.

## Last Completed Task
**v0.28: Skills module rebuilt (new Module 5 slot) — folder-format
only, with full v0.24 feature parity + UI polish.** Integrated the
12 user-delivered files from `ClaudePM-skill-module/` via
`integration-prompt-skill-module.md`, then redirected the scanner
to ONLY scan `<name>/SKILL.md` folder-format skills (no more flat
`*.skill` files — those are ZIP archives in the wild and were
rendering as `PK…` garbage). Re-added v0.24 features: Browse,
Rename, Backup (folder copy with timestamp), Export (folder
duplication), severity chips with counts, per-finding 📋 Copy,
global severity-filter overlay view with click-to-jump. Polish
pass: skill list became a `TreeView` (folder/file icons, expand to
see resources inline — removed the separate Supporting Resources
panel), description and viewer textboxes are fixed at 150px / 300px
regardless of content, app-wide button style (CornerRadius=6,
Padding=12,5, FontSize=12) in App.axaml. Folder row redesigned to
TextBox + 📁 Browse icon + 🔄 Scan icon inline. Sidebar now has
**10 entries**: Skills sits between Notebook and Bug Tracker (the
v0.24 Module 5 slot). New `SkillSectionViewModel` hosts an
in-pane Manager/Builder toggle — the Builder tab is hidden until
Phase 2 wires it in. Build green; tests 69/69 unchanged.

### What shipped this turn

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

**Next:** **Phase 2 — Skill Builder.** Following
`build-prompt-skill-builder.md`, design and build the Builder
sub-page of the Skills section. `SkillSectionViewModel` already
accepts an optional `Builder` PageViewModel — wire the new VM
through DI and the Builder tab lights up automatically. Untouched
specs in the working tree: `build-prompt-vision-audit.md` (another
new module). Also still on the table from the original roadmap:
**M3 #11 "Apply with AI"** for documentation fix prompts, the rest
of M3 (persistent agent action log, AI call log + cost tracking).
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
