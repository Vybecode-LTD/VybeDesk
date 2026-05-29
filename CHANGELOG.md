# Changelog

> Reverse-chronological. Versions trail Git tags; commit hashes link to the
> work that landed each entry. Snapshot tag `AlphaV0.5.0` marks the end of
> Milestone 1.

## [v0.32] — IN PROGRESS — M3 + M4 + M5 #17 + persistence fix + layout fix + landing overlays

> **Status: 90+ uncommitted files; build green; 207/207 tests pass.
> No remaining open bugs.**
>
> **HomeView + ProjectsView layout regression — RESOLVED (2026-05-28).**
> Root cause: the Fluent ContentControl defaults
> `VerticalContentAlignment` to `Top`, measuring child UserControls
> with unbounded (infinite) height. Fix:
> `VerticalContentAlignment="Stretch"` on MainWindow's ContentControl
> + ScrollViewer wrappers in ProjectsView and HomeView. See
> [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md).
>
> **Cross-module project persistence — RESOLVED (2026-05-28).** Root
> cause: passive null writes from TwoWay ComboBox bindings flowing
> through `ActiveProjectContext.SetCurrent(null)`. Fix: idempotent
> null-safe `SetCurrent` (null no-ops, same-ID updates reference
> without firing `Changed`) + explicit `ClearCurrent()` + per-module
> project isolation (reload flag, last-selected-ID restore, null-write
> suppression, `OnActivated()` restore from context). See
> [docs/PROJECT_PERSISTENCE_BUG.md](docs/PROJECT_PERSISTENCE_BUG.md).
>
> Nothing in this version has been committed yet. All feature work
> and bug fixes are in final form.

### Added

- **M3 #10 — persistent agent action log per project.** New
  `agent_actions` SQLite table (`project_id`, `kind`, `path`,
  `original_content`, `new_content`, `status`, `executed_at`). Replaces
  the in-memory `UndoHistory`; survives app restarts. `IAgentActionLogStore`
  interface in Core + `SqliteAgentActionLogStore` implementation in
  Services. `AgentActionService` refactored to persist every executed
  action and to query the store for the most-recent undoable / undone
  entries. Cross-session undo and the new cross-session **Redo Last**
  button both work off this log.
- **M3 #11 — "Apply with AI" for fix prompts.** New `INotebookOpener`
  cross-VM coordinator (lazy `IServiceProvider` resolution to avoid the
  Notebook ↔ Documentation DI cycle). DocumentationViewModel gained
  `ApplyReconciliationFixPromptWithAiCommand` +
  `ApplyAuditFixPromptWithAiCommand`; both feed the generated fix
  prompt into the Notebook scoped to the active project and let the
  existing preview/execute/undo gate handle the filesystem changes.
  Closes the doc-reconciliation loop end-to-end.
- **`edit_file` agent tool.** Fourth tool in the Notebook's approval-
  gated set (after `create_file`, `create_folder`, `move`). String-based
  Edit / Replace All semantics matching Claude Code's `Edit` tool.
  Captures `OldString` + `NewString` + `ReplaceAll` on the
  `AgentAction`; preview shows the diff inline; execute writes; undo
  rewrites the original content from the persisted log. 6 new
  `AgentActionServiceEditFileTests`.
- **Redo Last button.** When the most recent action was undone and no
  newer action has been logged, the Notebook's action chip strip shows
  a "Redo Last" button next to Undo. Reads `new_content` from the
  agent_actions log row that was previously marked Undone, restores it
  to disk, and flips the status back to Done. Works across app
  restarts (the row persists; the button shows on next launch as long
  as the redo is still valid).
- **M4 #14 — Import existing project from `.claude/` + git.**
  New `IProjectImportService` + `ProjectImportService`. Points at a
  folder → ingests `CLAUDE.md` as the project Description, seeds
  `Project.LastActivity` from `git log -1`, pulls
  `.claude/commands/*.md` into the Prompt library tagged with the
  project name, auto-detects a logo from common filenames (`logo.png`,
  `icon.png`, `favicon.ico`). UI: "Import existing…" button on the
  Projects tab next to "New project". Skipped-duplicate count is
  surfaced in the StatusMessage.
- **M4 #15 — Project templates in Session Builder.**
  `SessionTemplate` enum + `SessionTemplates` static catalog (5
  templates: PlainMonorepo, AvaloniaDotNet, FastApiPython,
  NextJsTypeScript, PythonCli). Each ships its own CLAUDE.md skeleton,
  README, `.gitignore`, and a stack-tuned kickoff prompt block.
  Session Builder gained a Step 0 template picker; selecting a
  template seeds Step 1's description and pre-fills the staged
  files / prompt prefix. 4 new `SessionTemplatesTests`.
- **M4 #16 — Per-project model + output overrides.** Optional
  `Model` + `DefaultOutputPath` properties on `Project`.
  `IActiveProjectContext` / `ActiveProjectContext` tracks the
  currently-selected project so `AnthropicChatService` can resolve
  `project.Model` → `settings.Model` at request time. ProjectsView
  gained a Model dropdown (`ModelsCatalog` shared with Settings, with
  a "(Use global default)" sentinel that maps to `null` on the
  Project) + a freeform custom-model-ID textbox + a Default output
  path Browse row. New `ModelsCatalog` ViewModel-level singleton
  used by both ProjectsView and SettingsView.
- **M5 #17 (partial) — Project health cards on Home.** New
  `IProjectHealthService` + `ProjectHealthService` computes per-project
  metrics asynchronously (stale-doc count from a fresh structural
  reconciliation pass; commits in the last 7 days from `git log`;
  pending agent action count from the new agent_actions log;
  LastActivity timestamp). New `IHomeNavigator` + `HomeNavigator`
  cross-VM coordinator (same lazy-IServiceProvider pattern as
  INotebookOpener). `HomeViewModel` rebuilt around a per-project
  `ProjectHealthCard` (loads metrics in parallel after card render,
  surfaces `IsLoading` / `LoadFailed` flags); 5-card pagination
  (`PagedCards`, `CurrentPage`, `RebuildPagedCards`); per-project logo
  bitmap loading with folder-glyph fallback. All three layers (data +
  VM + View) are complete (6 new `ProjectHealthServiceTests`). The
  View layer was previously blocked by a layout regression, now
  resolved (2026-05-28) — see
  [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md).
- **`docs/LAYOUT_REGRESSION.md`** — resolved postmortem capturing
  the four layout patterns that failed, the root cause (Fluent
  ContentControl `VerticalContentAlignment` defaulting to `Top`),
  and the fix (`VerticalContentAlignment="Stretch"` on MainWindow's
  ContentControl + ScrollViewer wrappers in both views).
- **`docs/TESTING.md`** — testing & regression framework. Three
  verification layers (build / unit / smoke), unit-test scope and
  conventions, the NON-NEGOTIABLE per-update smoke-test protocol
  (expanded from the HANDOFF.md §Conventions one-liner), the
  layout-regression-specific procedure, and the proposed (not
  yet wired) `Avalonia.Headless.XUnit` layout-regression test rig.
- **"Choose a project" landing overlays on all 6 project-scoped
  modules** (2026-05-28). Every module that depends on a project
  selection (Documentation, Prompts, Bug Tracker, Testing Manager,
  Vision Audit, Notebook) now shows a full-screen landing overlay
  listing registered projects as clickable cards. The overlay renders
  as the LAST child in its Grid (highest z-order in Avalonia) with a
  solid background and `FallbackValue=True` on `IsVisible` so it
  defaults to visible before DataContext propagates. The Notebook
  variant additionally offers an "All Projects" option.
- **Notebook landing pagination** (2026-05-28). The Notebook's
  "Choose a project" overlay uses paginated project cards (4 per page)
  instead of a ScrollViewer. VM properties: `PagedLandingProjects`,
  `LandingCurrentPage`, `LandingTotalPages`, `LandingHasMultiplePages`,
  `CanLandingPrev/Next`, `RefreshLandingPagination()`. XAML: Prev/Next
  buttons + page label, only visible when `LandingHasMultiplePages`.
- **`ActiveProjectContextTests`** (3 tests): `SetCurrent_SameProjectId_
  DoesNotFireChanged`, `SetCurrent_Null_DoesNotClearExistingProject`,
  `ClearCurrent_ExplicitlyClearsAndFiresChanged`.
- **App/UI regression tests** (18 new tests in `AppSmoke/`):
  - `HomeViewLayoutTests` (6 tests) — VM-level pagination regression
    coverage: PagedCards never exceeds PageSize, pagination controls
    correct for card count, all cards have valid Project data,
    navigation round-trip restores full page size.
  - `ProjectsViewLayoutTests` (6 tests) — VM-level form binding
    regression coverage: selecting a project populates ALL edit fields
    (including M4 #16 additions: Model, DefaultOutputPath, LogoPath),
    HasSelection toggles correctly, null model maps to empty edit
    field, deselection clears all fields, Save writes all fields back,
    empty string → null mapping for optional fields.
  - `ProjectSelectionPersistenceTests` (6 tests) — locks in the
    passive-null-write protection rule: SetCurrent(null) after a real
    project preserves it, initial null stays null, different projects
    fire Changed, only ClearCurrent resets to null, multiple passive
    nulls preserve last project, passive null does not fire Changed.
- **`SqliteProjectStoreCascadeDeleteTests`** (10 tests): proves
  `RemoveAsync` cascade-deletes all project-scoped rows across 7
  tables (`bugs`, `testing_plans`, `vision_records`, `audit_history`,
  `agent_actions`, `notes`, `ai_calls`) in a single transaction.
  Includes isolation (other project's rows survive) and Changed event.

### Changed

- **`Project` model** gained `Model`, `DefaultOutputPath`, and
  `LogoPath` properties (all nullable; SQLite migration is additive
  via `pragma_table_info` + `ALTER TABLE ADD COLUMN` so existing
  databases auto-upgrade on load).
- **`AnthropicChatService`** resolves the model at request time as
  `activeProject.Model ?? settings.Model ?? DefaultModel`. Previously
  consulted only `settings.Model`.
- **`NotebookViewModel.BeginFreshConversation()`** — new method that
  clears `_history`, `Messages`, `PendingActions`, and
  `_pendingReadResults`. Called by `NotebookOpener` before populating
  a new conversation, to avoid the "orphan tool_use ids without
  matching tool_result blocks" protocol violation on the next AI call.
- **CanUndo / CanRedo on `AgentActionService`** moved from derived
  `=> _log.HasUndoable` getters to backed `[ObservableProperty]` fields
  set explicitly in `RefreshHistory`. Notification was unreliable
  before — the Notebook's Undo / Redo buttons sometimes didn't
  re-enable after a state change. Backed fields fire the change
  notification deterministically.

### Fixed

- **Notebook protocol violation on "Apply with AI" handoff.** Opening
  the Notebook with a pre-populated prompt sometimes carried over
  `tool_use` IDs from a previous conversation whose `tool_result`
  blocks weren't in the new request body. Anthropic rejected the
  request with a 400. Fixed by `BeginFreshConversation()` (see
  above) — every external entry point (Documentation → Apply with AI,
  Home → Open Project → Notebook) calls it before populating.
- **MVVMTK0034 source-generator warnings** in the new VMs (referencing
  `_field.HasValue` instead of `Field.HasValue` for nullable
  `[ObservableProperty]` fields). Replaced with the property reference.

### Resolved this version (layout)

- **HomeView + ProjectsView layout overflow (RESOLVED 2026-05-28).**
  Root cause: the Fluent ContentControl defaults
  `VerticalContentAlignment` to `Top`, measuring child UserControls
  with unbounded (infinite) height. All four prior layout patterns
  (Plans A-D) failed because they attempted fixes inside the
  UserControls while the problem was at the MainWindow level. Fix:
  `VerticalContentAlignment="Stretch"` on MainWindow's ContentControl
  + ScrollViewer wrappers in ProjectsView and HomeView. Full
  postmortem in
  [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md).

### Resolved this version

- **Cross-module project persistence (RESOLVED 2026-05-28).** Root
  cause: passive null writes from TwoWay ComboBox bindings flowing
  through `ActiveProjectContext.SetCurrent`. Every time a ModuleHeader
  ComboBox initialized (or the Projects collection was cleared/rebuilt),
  a null was written through the TwoWay chain into `SetCurrent(null)`,
  which broadcast a `Changed` event that cleared every other module's
  selection. Fix: two-part rewrite — (1) `ActiveProjectContext.SetCurrent`
  made idempotent and null-safe (null no-ops; same-ID updates reference
  without firing `Changed`; explicit `ClearCurrent()` for intentional
  clears); (2) per-module project isolation hardening in all 6
  project-scoped VMs (`_lastSelectedProjectId` field, `_reloadingProjects`
  flag, null-write suppression, `OnActivated()` restore). See
  [docs/PROJECT_PERSISTENCE_BUG.md](docs/PROJECT_PERSISTENCE_BUG.md).

## [v0.31] — 2026-05-24 — Unified module header + sidebar submenu + Documentation refit + scrollbar polish

App-wide UI cohesion pass. No new modules; no new tests; no schema
changes. Every sidebar page now reads as one design language.

### Added

- **`Controls/ModuleHeader.axaml`** — a single 105px-tall unified
  header `UserControl` shown at the top of every sidebar page.
  3-column 2-row main area + a 25px `#22222A` status sub-bar
  underneath. Left column = home-link title + glyph + breadcrumbs
  + description. Middle column = project picker (when `ShowPicker=True`,
  via a `StyledProperty` so the same control works on project-scoped
  and non-project modules). Right column = Reset and Restart chips,
  always visible, greyed via `IsEnabled` when the VM doesn't expose
  the corresponding command. The module title is itself the
  "back to module home" link (no separate icon).
- **`PageViewModel` virtuals**: `Breadcrumbs` (defaults to empty),
  `GoModuleHomeCommand` / `ResetCommand` / `RestartCommand`
  (defaults to null), and `Children` (default empty — used by the
  sidebar TreeView to render nested sub-pages). Concrete VMs
  override these and ALIAS their `[RelayCommand]` methods to the
  base virtuals (`public override IRelayCommand? GoModuleHomeCommand
  => GoToFirstStageCommand;`) — necessary because the source generator
  emits non-`override` properties and a same-name clash with the base
  virtual breaks CS0506.
- **Sidebar TreeView**: `MainWindow.axaml`'s sidebar `ListBox` is
  replaced by a `TreeView` with a polymorphic `TreeDataTemplate
  DataType="vm:PageViewModel" ItemsSource="{Binding Children}"`.
  Clicking "Skills" expands to reveal "Manager" and "Builder" as
  nested sub-items; selecting either navigates directly. Selecting
  the Skills parent itself triggers `MainWindowViewModel.OnCurrentPageChanged`
  which re-routes via `Dispatcher.UIThread.Post` to the first child
  (Manager) — the user perceives a single click that both expands
  and navigates.
- **Per-VM Reset / Restart semantics**: every wizard VM (VisionAudit,
  SkillBuilder, SessionBuilder, TestingManager) gained per-stage
  Reset (clear the current stage's inputs only) and Restart (clear
  every transient field + return to stage 1) `[RelayCommand]` methods,
  aliased to the base virtuals.
- **Global app-wide ScrollViewer styling** in `App.axaml`: three
  `Style` blocks. (1) `Selector="ScrollViewer"` sets
  `AllowAutoHide="False"` so scrollbars are always-visible classic
  bars that reserve layout space (Fluent's default overlay scrollbars
  float over content and ignore Padding-based gaps). (2)
  `Selector="ScrollBar:vertical|:horizontal"` sets Width/Height to
  8px (half of Fluent's ~16px default) plus MinWidth/Height + MaxWidth/
  Height to lock that size against pointer-over expansion. (3)
  `Selector="ScrollViewer /template/ ScrollContentPresenter#PART_ContentPresenter"`
  sets `Margin="0,0,50,0"` — that's the actual mechanism that
  produces the visible 50px gap between content and scrollbar.
  `ScrollViewer.Padding` does NOT produce the gap in Avalonia 11.3
  Fluent (template absorbs it into the inner presenter without
  offsetting the scrollbar).

### Changed

- **DocumentationView restructured** to match the SkillManagerView
  layout: outer DockPanel → ModuleHeader (top) → 320px `#22222A`
  left rail with folder path + 📁 Browse + 🔄 Scan icon buttons +
  Watch-mode toggle + Documents ListBox → right pane with action
  toolbar (severity chips + Run AI Analysis / Audit Project /
  Generate Fix Prompt / Export Report + their Cancel buttons) over
  the existing three-state workspace (default findings / audit
  overlay / inline editor). Internal workspace markup is unchanged
  byte-for-byte; only the outer container shape moved.
- **`MainWindowViewModel` gained an `OnCurrentPageChanged`
  interceptor** that re-routes group-node selections to their
  first child via `Dispatcher.UIThread.Post`. Prevents the
  ContentControl from trying to render a bare group node.
- **`SkillSectionViewModel` refit** from a toggle-host with
  `ActivePage` / `IsManagerActive` / `IsBuilderActive` /
  `ShowManager|Builder` commands into a pure sidebar group node.
  It exposes `Children = [Manager, Builder]` and otherwise does
  nothing — the navigation now flows through the sidebar TreeView.
- **Per-view ScrollViewer Padding right-values** were also set
  during the investigation but are NOT what produces the gap —
  the global Style on the ScrollContentPresenter is. The Padding
  values are harmless (they just shrink inner content area by 50px
  on the right inside the presenter) and could be cleaned up later.

### Removed

- **`Controls/ModuleSubHeader.axaml(.cs)`** — existed briefly in
  the v2 patch as a separate 74px sub-band, then merged into the
  single-band `ModuleHeader` per user direction.
- **`Views/SkillSectionView.axaml(.cs)`** — the old in-pane
  Manager/Builder toggle host. The sidebar TreeView now provides
  the navigation, so the section view has no role. The
  SkillSectionViewModel survives (as the sidebar group node), but
  it has no view of its own — clicking it routes straight to Manager.

### Fixed

- **Scrollbar overlap on every view with scrollable content.**
  Initial fix attempt added `Padding="0,0,N,0"` to each ScrollViewer
  — silently ineffective because Avalonia 11.3 Fluent absorbs that
  Padding into the inner ScrollContentPresenter without offsetting
  the scrollbar from content. The root cause was diagnosed by
  reviewing the `avalonia-layout-patterns` skill (Margin/Padding
  adjust a child within its allocation, never the allocation itself),
  and resolved by setting `Margin` on the inner `ScrollContentPresenter`
  via a global Style. Documented in `App.axaml`.

### Build / test

- Build green, 92/92 tests pass (same count as v0.30 — no new tests
  this turn; reset/restart VM tests still parked).
- App launches with 11 sidebar entries; Skills now shown as a
  parent with Manager + Builder children.

## [v0.30] — 2026-05-24 — Vision Audit module + persisted audit history

The eighth user-spec-driven module. Externalises drift detection — the one
discipline no other VybeDesk module touches. Built from
`docs/build-prompts/vision-audit.md` applying the `vision-drift-detection`
skill. **Persisted audit history was added at user request** (the spec
itself marked it out-of-scope for v1; the user opted in).

- **Added** `VybeDesk.Core.Models.VisionRecord` + `VisionStatement`
  (vision is a LIST of testable claims, not one block of prose, because
  the audit operates statement-by-statement). `StatementVerdict` record
  + `AlignmentRank` enum (OnTrack / AtRisk / OffTrack — visualised via
  `SeverityToBrushConverter` with the same red/amber/blue palette as
  Finding/Bug severity). `AuditMode` enum (Structural / Targeted) +
  `AuditReport` record + `AuditHistoryEntry`.
- **Added** `VybeDesk.Core.Services.IVisionStore` (one VisionRecord per
  project, upsert) + `IVisionAuditService` (`ExtractVisionAsync`,
  `AuditAsync`, `BuildReportMarkdown`, `BuildDeepDivePrompt`) +
  `IAuditHistoryStore` (per-project append-only, newest-first ordering,
  single-delete + clear-all).
- **Added** `vision_records` + `audit_history` tables in `Database.cs`.
  Statements + verdicts stored as JSON TEXT consistent with how prompt
  tags and questionnaire answers are stored. `audit_history` has
  `idx_audit_history_project (project_id, generated_at DESC)`.
- **Added** `SqliteVisionStore` and `SqliteAuditHistoryStore` in
  `Services/Storage/`.
- **Added** `VybeDesk.Services.Vision.VisionAuditService` orchestrating
  the four jobs from the spec: extract (reuses
  `IDocReconciliationService.ScanAsync` — single source of truth for
  doc scanning), structural audit (gathers folder/file shape + dep
  manifest + docs, bounded by depth/size caps), targeted audit
  (two-phase: AI picks ≤ 10 relevant files, then verdicts with file
  contents), build outputs (markdown report leads with off-track items
  per the skill; deep-dive prompt names flagged statements for
  Claude Code line-level verification). JSON parser surfaces clear
  errors when the AI replies in prose instead of structured JSON.
- **Added** `VybeDesk.App.ViewModels.VisionAuditViewModel` (project-scoped)
  with a `VisionAuditStage` enum state machine — Extract → Approve →
  ChooseMode → RunReview. The **Approve gate is mandatory** — the
  audit refuses to run against an unapproved vision; an audit against
  the wrong measuring stick is worse than no audit.
- **Added** `VybeDesk.App.Views.VisionAuditView` following the
  per-stage bounded `Grid` wizard pattern from
  `memory/bounded-wizard-stages.md` — each stage's flex content (draft
  statements list, mode picker, audit report) sits in a bounded `*` row
  with its own ScrollViewer; the button rows live in dedicated `Auto`
  rows that are always reachable.
- **Added** audit history feature (user-requested addition): every
  successful audit run is persisted to `audit_history` with its report
  markdown + deep-dive prompt as text. The RunReview stage shows the
  per-project history as a list of cards with timestamp + mode +
  summary counts. Open loads an entry's saved content into the report
  panels; 🗑 deletes a single entry; Clear all wipes the project's
  history. Entries persist across app restarts.
- **Changed** `SeverityToBrushConverter` to also map `AlignmentRank` to
  the existing red / amber / blue palette.
- **Changed** `Program.cs` DI: registered `IVisionStore`,
  `IAuditHistoryStore`, `IVisionAuditService`, `VisionAuditViewModel`.
- **Changed** `MainWindowViewModel`: added `VisionAuditViewModel`
  constructor parameter, placed in `Pages` between Testing Manager and
  Settings. Sidebar now has **11 entries**.
- **Added** `SqliteVisionStoreTests` (6 cases), `VisionAuditServiceTests`
  (6 cases — approval-gate enforced, missing statements fabricated as
  OffTrack, report leads with off-track, deep-dive names flagged items),
  `SqliteAuditHistoryStoreTests` (7 cases — add + newest-first ordering
  + project-scoping + remove single + clear-all + Changed event).

Build green; tests 92/92 (73 prior + 6 vision store + 6 audit service +
7 history store).

## [v0.29] — 2026-05-24 — Skill Builder module (Phase 2 of the Skills work)

The Skills section's Builder sub-page is live, slotted alongside the
Skill Manager. Follows the user-authored `docs/build-prompts/skill-builder.md`
spec (now at the same path under build-prompts) and applies two loaded
skills: `skill-design-workflow` (the end-to-end process) and
`skill-file-authoring` (the routing-description + imperative-body craft).
The Builder shares validation + serialization with the Skill Manager so
anything it produces, the Manager can browse identically — proven at
runtime by `SkillBuilderServiceTests`.

- **Added** `VybeDesk.Core.Services.ISkillBuilderService` with
  `GenerateClarifyingQuestionsAsync`, `DraftAsync`, `Validate`, `EmitAsync`,
  plus three DTOs (`SkillBuilderInputs`, `QuestionAnswer`,
  `SkillEmitResult`). Process-oriented — no new database table or store.
- **Added** `VybeDesk.Services.Skills.SkillBuilderService` orchestrating
  the workflow. Two AI calls (questions, draft) via `IAiService`.
  Validation and serialization delegate to `ISkillLibraryService` —
  one source of truth across the two halves of the skill lifecycle.
  `EmitAsync` writes both `<name>.skill` flat file and `<name>/SKILL.md`
  folder forms beneath the target folder; refuses to overwrite either.
  JSON parsing recovers from fenced (` ```json ```) responses and surfaces
  user-actionable error messages when the AI replies conversationally
  (no more opaque `'I' is an invalid start of a value` JSON parser
  errors leaking through).
- **Added** `VybeDesk.App.ViewModels.SkillBuilderViewModel` with a
  `BuilderStage` enum state machine (Inputs / Questions / Review /
  Emitted). Three new RelayCommands take `IncludeCancelCommand = true`
  so the user can abort the AI mid-call. Stage transitions handled via
  partial methods; Findings refresh on every Validate call.
- **Added** `VybeDesk.App.Views.SkillBuilderView` using the canonical
  per-stage bounded wizard layout: outer is `DockPanel LastChildFill`
  with the header docked Top; the fill area hosts four overlaid
  IsVisible-controlled stage `Grid`s. Each stage uses its own
  `RowDefinitions="Auto,*,Auto"` (Step 2) or `"*,Auto"` (Steps 1/3/4)
  so the long content (questions ItemsControl, review form) lives in a
  bounded `*` row with its own ScrollViewer and the buttons live in a
  dedicated `Auto` row that's always reachable. **This shape replaces
  a single-outer-ScrollViewer-over-IsVisible-toggled-stages
  arrangement that hit a measure-pass desync** — see the
  `docs/design-patterns/testing-manager-wizard-options.md` deep-dive.
- **Added** Stage-1 pre-flight input validation: name must be ≥ 3 chars,
  lowercase-hyphen, no "claude"; rough description must be ≥ 40 chars.
  Vague inputs are blocked with a status message before any AI call.
- **Added** Stage-2 all-blank-answers soft warning: if every clarifying
  answer is empty when the user clicks Draft, the first click surfaces
  a warning; the second click proceeds. Resets on Back / StartOver.
- **Changed** `SkillSectionViewModel`'s DI registration in `Program.cs`
  to a factory that resolves and injects `SkillBuilderViewModel` into
  the optional `builder` constructor parameter. The Section's in-pane
  Manager/Builder toggle bar automatically lights up the Builder tab.
- **Added** `SkillBuilderServiceTests` (4 cases):
  - `Validate_ReportsRuleViolations_ViaSharedLibraryValidation` — proves
    the Builder's `Validate` results are byte-identical to the Manager's.
  - `EmitAsync_ProducesBothFlatFileAndFolderForm` — confirms both output
    forms are written and contain identical text.
  - `EmittedFolderForm_PassesLibraryScan_AndValidatesIdentically` — the
    Builder's output is something the Manager scans and validates
    identically to the in-memory draft.
  - `EmitAsync_RefusesToOverwriteExistingTarget` — second emit to the
    same target fails clearly.

Build green; tests 73/73 (69 prior + 4 new).

## [v0.28] — 2026-05-24 — Skills module rebuilt (folder-format only + v0.24 features + UI polish)

The Skill area returns as Module 5 after its v0.25 deletion. Built from
the 12 user-delivered files in `VybeDesk-skill-module/` per
`integration-prompt-skill-module.md`, then immediately customised in
two directions per user feedback: redirected to scan only folder-format
skills (`<name>/SKILL.md`), and the v0.24 feature set (Browse / Rename /
Backup / Export / severity-filtered findings view / per-finding Copy)
re-added. A polish pass replaced the skill list with a TreeView,
fixed editor/viewer textbox heights, and introduced an app-wide button
style.

- **Added** `VybeDesk.Core.Models.SkillFile` (with `Resources` list +
  `HasResources` flag) and `VybeDesk.Core.Models.SkillResource`.
- **Added** `VybeDesk.Core.Services.ISkillLibraryService` with
  `PopulateResources`, `ReadResourceAsync`, `BackupAsync`, `RenameAsync`.
  `ExportAsync` re-purposed for folder duplication (not flat `.skill`
  write-out).
- **Added** `VybeDesk.Services.Skills.SkillLibraryService` implementing
  the above. `ScanAsync` enumerates `*.md` recursively and keeps only
  files literally named `SKILL.md` (case-insensitive) — flat `.skill`
  archives are no longer parsed (they were rendering as `PK…` garbage
  bodies with "(no name)" headers because they are ZIP files).
- **Added** `VybeDesk.App.ViewModels.SkillSectionViewModel` — a thin
  container that hosts an in-pane Manager/Builder toggle. Today it
  hosts only the Manager; the Builder slot is optional and will be
  filled in Phase 2.
- **Added** `VybeDesk.App.ViewModels.SkillManagerViewModel` with
  Browse / Scan / Save / Rename / Backup / Export / FilterCritical /
  FilterWarning / FilterInfo / NavigateToFindingSkill / CopyFinding /
  CopyAsync commands. Selection driven by a single
  `SelectedTreeItem` (object?); `SelectedSkill` and `SelectedResource`
  are derived from it so the TreeView can carry both node types.
- **Added** `VybeDesk.App.Views.SkillSectionView` (in-pane toggle bar)
  and `SkillManagerView`. SkillManagerView uses a `TreeView` for the
  skill list — each skill node carries a 📁 folder icon, expands to
  reveal nested 📄 resource children. The separate Supporting Resources
  panel from the integration-prompt delivery is **removed** — that
  surface is now part of the tree.
- **Added** app-wide `Button` style in `App.axaml`:
  `CornerRadius=6`, `Padding=12,5`, `FontSize=12`. Applies everywhere
  (Skills, Bug Tracker, Testing Manager, Notebook, Documentation,
  Projects, Settings). Individual views can still override via class
  selectors (e.g. `Button.chip` for severity pills).
- **Changed** `Program.cs` DI: registered `ISkillLibraryService`,
  `SkillManagerViewModel`, `SkillSectionViewModel`.
- **Changed** `MainWindowViewModel`: added `SkillSectionViewModel`
  constructor parameter, slotted between `NotebookViewModel` and
  `BugTrackerViewModel` in `Pages`. Sidebar now has **10 entries**.
- **Fixed** folder row: TextBox + 📁 Browse + 🔄 Scan are inline on one
  row; both buttons are icon-only with tooltips for affordance.
- **Fixed** editor/viewer heights: description TextBox = 150px,
  skill/resource viewer = 300px, regardless of content length.
- **Layout note** (HANDOFF reinforced): SkillManagerView outer chain
  is `DockPanel LastChildFill="True"` per the v0.27 bounded-
  ScrollViewer convention; the fixed-height children + ScrollViewer-as-
  fill pattern scrolls correctly.

Tests 69/69 (unchanged; no new tests added with this commit —
SkillLibraryServiceTests was deleted in v0.25 and is not yet restored
for the rebuilt service).

## [v0.27] — 2026-05-24 — Testing Manager module (Pattern C stepped wizard) + Bug Tracker ↔ Testing Manager event

A new project-scoped Testing Manager module joins as Module 6, the second
build off a user-authored spec in `docs/build-prompts/`. The module
externalizes "what kind of testing should this project have" as a
five-question plain-language questionnaire (deliberately NOT a framework
picker — that would presume knowledge the target user doesn't have). A
pure-function `StrategySelector` turns the answers into a recommendation
with reasoning; accepting saves a `TestingPlan` (one per project). Two
generators feed off the saved plan: framework setup prompts (drawn from a
built-in seven-entry catalog) and regression-test prompts (driven by a
loosely-coupled event from the Bug Tracker).

- **Added** `VybeDesk.Core.Models.TestingPlan` with `TestKind` enum
  (Unit / Integration / Component / EndToEnd / ManualChecklist) and
  `QuestionnaireAnswers` record. Answers are stored alongside the
  conclusion so the user can re-run the questionnaire later and see why
  the strategy was chosen.
- **Added** `VybeDesk.Core.Services.ITestingPlanStore` (one plan per
  project, upsert semantics) and the cross-module
  `VybeDesk.Core.Services.IBugFixedNotifier` + `BugFixedEvent` record —
  the only thing the Bug Tracker and Testing Manager share.
- **Added** `testing_plans` table to `Database.cs` with
  `UNIQUE(project_id)`. Lists and answers stored as JSON TEXT (consistent
  with how prompt tags are stored).
- **Added** `VybeDesk.Services.Storage.SqliteTestingPlanStore` using
  `ON CONFLICT(project_id) DO UPDATE` for upsert semantics.
- **Added** `VybeDesk.Services.Testing.TestingFrameworkCatalog` —
  ships-with-the-app framework catalog, NOT user data. Seven seed entries:
  xUnit (.NET), GoogleTest (C++), pytest (Python), Vitest (JS/TS), Jest
  (JS/TS, established alternative), React Testing Library (React
  components), Playwright (web E2E). Each entry is a self-contained
  record so adding a framework later is one data line, not a logic edit.
  Setup prompts instruct Claude Code to establish folder layout AND
  write one example test that establishes the pattern.
- **Added** `VybeDesk.Services.Testing.StrategySelector` — pure function
  from `QuestionnaireAnswers` → `StrategyRecommendation`. Always
  recommends unit tests in the language's framework; adds integration
  when external systems are flagged; adds Component + Playwright for
  high-stakes React frontends; adds ManualChecklist for personal solo
  pure-logic projects. Database testing is folded into Integration —
  never its own framework.
- **Added** `VybeDesk.Services.Testing.BugFixedNotifier` — minimal
  in-memory pub/sub singleton implementing `IBugFixedNotifier`.
- **Added** `TestingManagerViewModel` + `TestingManagerView` as a
  **data-driven stepped wizard** (Pattern C — see
  `docs/design-patterns/testing-manager-wizard-options.md`). The
  questionnaire renders ONE question at a time via a `ContentControl`
  bound to `CurrentQuestion`, with `Back` / `Next` /
  `See recommendation` navigation. Three view states: questionnaire,
  recommendation review (with Accept-and-save / Re-answer), and the saved
  plan view. **No ScrollViewer in the questionnaire path** — eliminates
  dependency on the Avalonia ScrollViewer-in-Grid-column height-constraint
  bug that surfaced in the first iteration. (Patterns A and B are
  documented as alternatives if the wizard ever needs to pivot.)
- **Added** `ViewModels/QuestionViewModel.cs` (+ nested `QuestionOption`)
  — reusable across any future wizard. Each option's IsSelected is a
  per-option bool the RadioButton binds one-way; the parent VM's
  `PickCommand` sets the selected token and syncs option flags.
- **Added** `Converters/BoolToBrushConverter.cs` — pipe-separated colour
  spec in `ConverterParameter` for the wizard progress dots. Registered
  in `App.axaml` as `BoolToBrush`.
- **Changed** `BugTrackerView.axaml` outer container from
  `<Grid ColumnDefinitions="340,*">` to `<DockPanel LastChildFill="True">`
  preemptively. BugTracker's content was bounded enough to avoid the
  scroll bug in practice, but the same Grid-with-columns shape was a
  latent risk. DockPanel pattern matches NotebookView.
- **Changed** `BugTrackerViewModel` to take `IBugFixedNotifier`. On
  Open/Fixing → Fixed transition during Save, fires
  `BugFixedEvent(ProjectId, BugId, Title)`. The Testing Manager listens
  and surfaces a nudge banner when the event's project matches the
  currently-selected project.
- **Changed** `Program.cs` DI: `ITestingPlanStore`,
  `ITestingFrameworkCatalog`, `IBugFixedNotifier`,
  `TestingManagerViewModel` registered as singletons.
- **Changed** `MainWindowViewModel`: added `TestingManagerViewModel`
  constructor parameter and Pages entry between Bug Tracker and Settings.
  Sidebar now has 9 entries.
- **Added** `SqliteTestingPlanStoreTests` (6 cases: null-for-unsaved,
  round-trip-all-fields, project-scoped retrieval, upsert behaviour,
  remove, Changed event).
- **Added** `TestingFrameworkCatalogTests` (7 cases: seven seed entries
  present, every entry has required fields, language lookup for .NET /
  JavaScript / Other, name lookup, database-testing-not-a-separate-
  framework drift guard).
- **Added** `StrategySelectorTests` (7 cases: .NET API → xUnit
  unit+integration, critical React → Vitest+RTL+Playwright, personal
  React → omits Playwright, personal solo pure logic → ManualChecklist,
  no external systems → omits Integration, Other language → empty
  Frameworks but kinds still recommended, friendly-language phrase in
  summary).

Build green; tests 69 / 69 (49 prior + 20 new).

## [v0.26] — 2026-05-24 — Bug Tracker module

A new project-scoped Bug Tracker takes the Module 5 sidebar slot (which v0.25
left empty). Built from the `docs/build-prompts/bug-tracker.md` spec the user
authored. Severity-sorted list, separate Steps / Expected / Actual fields by
design, Generate Fix Prompt command that packs selected bugs into a Claude
Code prompt, fixed-means-tested nudge on status transition to Fixed.

- **Added** `VybeDesk.Core.Models.Bug` entity with `BugSeverity`
  (Critical / Major / Minor) and `BugStatus` (Open / Fixing / Fixed / WontFix)
  enums. Three reproduction fields are deliberately separate to teach
  reproducible reporting.
- **Added** `VybeDesk.Core.Services.IBugStore` with `GetByProjectAsync`,
  `AddAsync`, `UpdateAsync`, `RemoveAsync`, and a `Changed` event.
- **Added** `bugs` table to `Database.cs` schema with `idx_bugs_project` index;
  enums stored as INTEGER, Guids as TEXT, timestamps as Unix INTEGER per the
  existing convention.
- **Added** `SqliteBugStore : IBugStore` in `Services/Storage/`, mirroring
  `SqliteProjectStore`'s explicit column-mapped shape. Single-statement writes
  serialized via the existing writer lock.
- **Added** `BugTrackerViewModel : PageViewModel` with project picker, severity-
  weighted sort (Critical → Major → Minor; within severity Open/Fixing above
  Fixed/WontFix; newest-first tiebreaker), per-severity summary chips, full
  CRUD, Generate Fix Prompt command (multi-select-aware; falls back to all
  open bugs), and fixed-means-tested nudge.
- **Added** `BugTrackerView.axaml` (+ `.cs`) master-detail like
  `PromptManagerView`. Severity dot in each list row uses
  `SeverityToBrushConverter`. Editor uses generously tall multi-line
  TextBoxes for the three reproduction fields. Fix-prompt output panel with
  Copy button.
- **Changed** `SeverityToBrushConverter` to also recognize `BugSeverity`
  (Critical → red, Major → amber, Minor → blue) so Documentation findings
  and Bug Tracker speak the same colour language.
- **Changed** `Program.cs`: added `IBugStore → SqliteBugStore` DI registration
  and `BugTrackerViewModel` singleton registration.
- **Changed** `MainWindowViewModel`: added `BugTrackerViewModel` ctor param
  and its `Pages` entry; sidebar now has 8 entries.
- **Added** `SqliteBugStoreTests` (5 cases): add-then-get-by-project,
  project-scoped retrieval (project A's bugs do not appear under project B),
  update round-trip, remove, Changed event fires on every mutating call.
- **Moved** the three user-authored build-prompt files
  (`build-prompt-bug-tracker.md`, `build-prompt-testing-manager.md`,
  `build-prompt-template.md`) from the repo root into
  `docs/build-prompts/` so the design intent rides with the implementation.

Build green; tests 49 / 49 (44 prior + 5 new).

## [v0.25] — 2026-05-24 — Skill Library module removed pending rewrite

Wholesale removal of Module 5 (Skill Library Manager). The v0.24
Resources/Validation display bug — nine layout iterations rejected
in a row — closed by deletion rather than a tenth attempt. Module 5
will be redesigned and rebuilt from scratch post-v1.0 as Roadmap M6.
The v0.24 implementation lives in git history through commit
`16f9468`; the rewrite should treat that code as inspiration only,
not a starting point.

- **Removed** `src/VybeDesk.Core/Models/SkillFile.cs`,
  `src/VybeDesk.Core/Models/SkillResource.cs`,
  `src/VybeDesk.Core/Services/ISkillLibraryService.cs`,
  `src/VybeDesk.Services/Skills/SkillLibraryService.cs`
  (entire `Services/Skills/` folder),
  `src/VybeDesk.App/ViewModels/SkillLibraryViewModel.cs`,
  `src/VybeDesk.App/Views/SkillLibraryView.axaml` (+ `.cs`).
- **Removed** `tests/VybeDesk.Tests/SkillLibraryServiceTests.cs`
  and all `Skill*` test cases. Test count drops accordingly.
- **Changed** `src/VybeDesk.App/Program.cs` — removed
  `using VybeDesk.Services.Skills;`, the
  `ISkillLibraryService -> SkillLibraryService` DI registration,
  and the `SkillLibraryViewModel` registration.
- **Changed** `src/VybeDesk.App/ViewModels/MainWindowViewModel.cs`
  — removed `SkillLibraryViewModel skills` constructor parameter
  and its entry in the `Pages` collection. Sidebar now has 7
  entries.
- **Changed** SPEC.md, CLAUDE.md, HANDOFF.md, ROADMAP.md, README.md,
  docs/USER_GUIDE.md, docs/ARCHITECTURE.md to mark Module 5 as
  removed-pending-rewrite and update sidebar / test counts.
  CHANGELOG history preserved.
- **Closed** the v0.24 open bug — the Skill Library
  Resources/Validation display issue is moot now that the module
  is gone.

## [v0.24] — 2026-05-24 — Skill Library Resources (incomplete) + handoff close-out

Doc maintenance pass. The Skill Library feature gained a "resource files"
listing inside folder-format skills (alongside SKILL.md), but the layout
of the inner scrollable box could not be made to display reliably across
nine distinct iterations — every attempt the user smoke-tested showed
either "items cut off", "Validation cut off", or made things worse. The
bug is documented in HANDOFF.md as a critical open issue with the
complete pattern catalog. The most recent attempt
(`16f9468` Auto+\* Grid header/body split) is the current state.

- **Open bug** (in `src/VybeDesk.App/Views/SkillLibraryView.axaml`):
  Resources box renders the bound data but the user reports content as
  cut off in the visible area. Nine layout patterns tried; see HANDOFF
  for the catalog and the suggested next-investigation steps.

## [v0.23] — 2026-05-24 — Prompt caching + Roadmap M6 + per-finding Copy

Anthropic prompt caching enabled on every API call: `cache_control` on
the system block (both streaming + non-streaming) and on the last tool
in the streaming path. Estimated ~70% savings on the system+tools
prefix across multi-turn Notebook sessions once history crosses the
4096-token threshold. Roadmap gains Milestone 6 "Skill Library Builder"
(wizard + AI-assist + preview + bulk import) sitting before v1.0 polish.
Per-finding 📋 Copy button in the Skill Library's filtered global
findings view yanks "\[SEVERITY\] file (category): message" to the
clipboard for one-paste fixes in Claude Code. (`dee7f17`, `1e53911`,
`e9f6464`)

- **Added** `cache_control: { type: "ephemeral" }` on the system block
  (array-form) in both `AgentChatAsync` and `CompleteAsync`, plus on
  the last tool in the streaming payload. Three wire-format tests.
- **Added** ADR-0006 documenting the caching strategy: hierarchical
  order, breakpoint budget, silent no-op below model minimum, the
  cache-invalidation footgun.
- **Added** ROADMAP.md Milestone 6 (items 19–22) for Skill Library
  Builder: New Skill wizard with template picker, AI assist on
  description + body, in-app preview, bulk import.
- **Added** Per-finding Copy button in `SkillLibraryView`'s filtered
  view; `CopyFindingCommand` formats one-line text.

## [v0.22] — 2026-05-24 — Skill Library polish: rename + clickable chips + Copy

Rename button on the Skill Library editor (handles both `.skill` and
folder/SKILL.md formats with collision check). Severity chips
(Critical/Warning/Info) become clickable Buttons that filter the right
pane to every finding of that severity across every scanned skill, each
row labeled with its source skill. App opens Maximized on startup;
"Export .skill" relabeled "Export" (it writes both formats now).
(`829e09b`, `21826cf`, `13ed1c2`)

- **Added** `RenameCommand` in `SkillLibraryViewModel`: File.Move for
  flat `.skill`, Directory.Move on the containing folder for SKILL.md.
  Refuses no-op renames and target-already-exists collisions, then
  re-saves so the on-disk frontmatter `name:` matches.
- **Added** `FilterBySeverity` parameterized command + `SeverityFilter`
  state + `IsEditorVisible` derived. The Issues collection rebuilds
  based on filter mode: per-skill when no filter, global-by-severity
  when active.
- **Added** "Clear Filter" button (only visible when filter active).
  Selecting a skill in the list also clears the filter.
- **Added** `SkillResource` Core record (RelativePath, FullPath,
  SizeBytes) + `ISkillLibraryService.GetResources(SkillFile)`. Scans
  the skill's folder recursively, excludes SKILL.md, capped at 200
  entries. ResourcesHeader shows truncation hint when capped.
- **Added** `MainWindow` opens with `WindowState=Maximized`.
- **Changed** "Export .skill" button label → "Export" (it now writes
  both formats per v0.21).

## [v0.21] — 2026-05-24 — Skill Library: Browse + dual-format scan/export

The Skill Library was the last module without a native folder picker —
fixed with a Browse… button using the existing `IFilePickerService`.
`ScanAsync` now finds both legacy flat `.skill` files AND modern
Claude Code `<name>/SKILL.md` folders (the layout under
`~/.claude/skills/`). `ExportAsync` writes BOTH formats side-by-side
so the same skill loads in Claude Code (folder) and Claude web (flat)
with no manual conversion. (`abfe26a`, `be11670`)

- **Added** Browse… button in Skill Library; constructor takes
  `IFilePickerService` (already registered, used by 4 other VMs).
- **Changed** `SkillLibraryService.ScanAsync` enumerates both
  `*.skill` and `SKILL.md` under the chosen root. Folder-format
  skills get `FileName = "<folder>/SKILL.md"` for list display.
- **Changed** `ExportAsync` writes both `<name>.skill` (flat) AND
  `<name>/SKILL.md` (folder); returns both paths joined.
- **Changed** SPEC.md Module 5 and `docs/USER_GUIDE.md` Skill Library
  section updated to describe both formats.
- **Added** Three new tests for folder-format scan, both-formats
  coexistence, and dual-format export.

## [v0.20] — 2026-05-24 — Smoke-test convention + Notebook bubble fix

Encoded a non-negotiable convention into HANDOFF + CLAUDE: at the
close of every roadmap milestone (or any batch the user agreed on as
a unit), the working agent launches the app and waits for the user
to visually verify before declaring done. Build-green proves code
correctness, not feature correctness. Plus a Notebook bubble fix:
the stale "Claude ended without producing a final response" note
was firing against well-formed responses too because the
`bubble.Text.Length` check raced with dispatcher-posted text deltas.
(`e1dcf18`, `bcf0f3d`)

- **Added** End-of-milestone smoke-test bullet in HANDOFF "Conventions
  (NON-NEGOTIABLE)" + a tighter summary in CLAUDE.md.
- **Fixed** Notebook auto-loop end-of-turn note: drop it entirely
  (per user feedback — not necessary to surface), and use
  `response.TextOutput` (synchronous, race-free) instead of the
  dispatcher-mutated `bubble.Text.Length` to decide when to set
  `StatusMessage = "(no response)"`.
- **Changed** `NotebookMessage.ShowThinkingPlaceholder` now gates on
  `IsStreaming` so the placeholder vanishes when a turn ends with no
  text instead of sticking forever.

## [v0.19] — 2026-05-24 — Tier 1 non-roadmap close-out (tests + UX + ADRs)

Three commits closing out Tier 1 of the post-M2.5 optimization
bucket: nine golden-input tests for `AuditAsync` JSON parsing,
three Notebook UX micros, and five ADRs capturing the non-obvious
technical decisions accumulated over M1–M2.5. (`00e82e4`, `d37aa74`,
`9bf2e69`)

- **Added** `DocReconciliationServiceTests` (9 cases): clean JSON,
  ```json``` fenced JSON, leading prose, trailing prose, mixed
  casing + trailing commas, malformed JSON, no JSON at all, blank-
  titled items, severity-sorted inconsistencies. Tests go through
  the public `AuditAsync` method (mocked `IAiService`).
- **Added** `NotebookMessage.ShowThinkingPlaceholder` → italic
  "thinking…" placeholder in the empty assistant bubble between
  Send and the first streamed character.
- **Added** Ctrl+Enter KeyBinding to `SendCommand` on the Notebook
  input; Ctrl+S KeyBinding to `SaveEditorCommand` on the doc editor.
- **Added** `docs/adr/` folder with five ADRs:
  - 0001 Markdig over Markdown.Avalonia (the blanking history)
  - 0002 Direct HTTPS over the Anthropic SDK
  - 0003 DPAPI for API key storage (Windows-first)
  - 0004 No iteration cap on the Notebook auto-loop
  - 0005 Project Audit as structured-JSON, not tool_use
- **Added** `docs/adr/README.md` index + "when to write a new ADR"
  guidance; ARCHITECTURE.md links to the folder so the orphan-doc
  check doesn't flag it.

## [v0.18] — 2026-05-24 — Safety hardening: symlinks + retry backoff

`AgentActionService.TryConfine` now walks each segment of the
requested path and resolves any existing segment that is a symlink/
junction to its final target — same treatment for the roots passed
to `SetScopedRoots`. A symlink planted under a scoped root that
points outside can no longer be used as an escape hatch.
`AnthropicChatService` gains exponential backoff with jitter on
429 / 503 / 529 responses, honoring `Retry-After` when present.
(`b7ac51f`)

- **Added** `ResolveSymlinks` helper in `AgentActionService` (walks
  segments, calls `FileSystemInfo.ResolveLinkTarget(returnFinalTarget:
  true)` on each existing prefix). Applied in `SetScopedRoots` and
  `TryConfine`.
- **Added** Symlink-escape test that creates a real junction and
  asserts the validator rejects writes through it (graceful skip if
  the test process lacks the symlink privilege on Windows).
- **Added** `SendWithRetryAsync` helper in `AnthropicChatService`:
  up to 3 retries, exponential backoff from 1s with jitter capped at
  1 minute, request rebuilt each attempt because `HttpRequestMessage`
  is single-use.
- **Added** Three retry tests via a new `ScriptedHandler` that
  returns a queued sequence of responses; `BuildService` generalized
  to take any `HttpMessageHandler`.

## [v0.17] — 2026-05-24 — M2.5 Project Audit + UX bundle

The Documentation Manager gains a synthesis pass (`AuditAsync`) that reads
a signal-weighted bundle of project docs and produces a structured
`ProjectAuditReport` (design summary, roadmap items tagged complete /
incomplete / unknown, severity-ranked inconsistencies). Shipped together
with a real clipboard service + Copy buttons across the app, a Claude
model picker in Settings, and a major notes-section upgrade. (`31424a6`)

- **Added** `ProjectAuditReport`, `AuditRoadmapItem`, `AuditInconsistency`
  Core records. `IDocReconciliationService.AuditAsync` +
  `BuildAuditFixPrompt`.
- **Added** Full-pane "Audit Project" overlay in the Documentation tab —
  five sections (Design, all items, Completed, Incomplete, fix prompt,
  Inconsistencies) with a "Generate Fix Prompt" action wired to its own
  `AuditFixPrompt` state.
- **Added** `IClipboardService` + `AvaloniaClipboardService` (lazy
  TopLevel resolution). Copy buttons in 8 places: structural fix prompt,
  AI semantic result, audit fix prompt, prompt library rows (per-row
  Body copy), Fill Template result, Generated Prompt, per-Notebook
  assistant message, Session Builder review result.
- **Added** Notes section in Notebook now reveals the selected note's
  body in a preview pane with three buttons: **Insert into chat**
  (prepends as reference for next message), **Copy**, **Delete**.
- **Added** Claude model dropdown in Settings — Opus 4.7 / Sonnet 4.6 /
  Haiku 4.5 (latest) + previous-gen Opus 4.6 / Sonnet 4.5 / Opus 4.5 +
  legacy Opus 4.1. Each tagged with tier + pricing hint. Custom-ID
  textbox kept for preview models the dropdown hasn't caught up with.
- **Fixed** Model catalog initially shipped with fake `claude-sonnet-4-7`
  ID (no such model exists — latest Sonnet is 4.6); corrected against
  Anthropic's official model overview.

## [v0.16] — 2026-05-24 — M2.8 custom Markdown renderer

Custom `MarkdownPresenter` Avalonia control backed by Markdig (parser only,
no Avalonia coupling). Replaces the third-party `Markdown.Avalonia` attempt
that silently blanked the chat bubble in every binding mode we tried. Used
for assistant prose in the Notebook + the audit's Design section.
(`5810c49`)

- **Added** Markdig 0.42.0 package; `App/Controls/MarkdownPresenter.cs`
  walks the AST and emits native Avalonia controls.
- **Added** Block support: H1–H4, paragraphs, fenced code blocks, ordered
  + unordered lists, blockquotes, thematic breaks, tables. Tables use
  star-weighted columns by body content length AND a per-column `MinWidth`
  sized to the header so headers stay single-line while body wraps.
- **Added** Inline support: literal text, inline code (monospace pill),
  strong/emphasis, links (styled), autolinks, line breaks.
- **Removed** Earlier `Markdown.Avalonia` dependency attempts and the
  `IsStreaming`-toggled SelectableTextBlock / MarkdownScrollViewer pair
  that came with them.

## [v0.15] — 2026-05-24 — M2.6 + M2.7: inline doc editor + watch mode

Click a doc in the Documentation list → the right column swaps from
Findings to a monospace text editor with Save / Revert / Close. Watch
mode adds a `FileSystemWatcher` on the project folder that debounces
`.md` / `.txt` changes and re-runs the structural pass. (`b9a250d`)

- **Added** `SelectedDoc` + editor state on `DocumentationViewModel`;
  `IsDefaultViewVisible` gates the findings view vs editor.
- **Added** Watch-mode checkbox; 750 ms debounce via swap-and-cancel
  `CancellationTokenSource`; rebuild watcher on toggle or folder change.

## [v0.14] — 2026-05-24 — M1.5 curated prompts seed + M1 close-out

30 prompts across 5 categories (Doc & VCS hygiene, Testing & regression,
Efficient task execution, New session starters, Common dev tasks) land in
a new `SeedPromptsData.cs`. `Database.SeedPrompts` now upserts by title
diff instead of "only run on empty table" so existing DBs pick up the
curated set without losing user-created prompts. (`8044ea9`)

- **Added** `SeedPromptsData.All` with 30 `SeedPrompt` records.
- **Changed** `Database.SeedPrompts` from one-time seed to idempotent
  by-title diff.
- **Fixed** Two FTS5 tests made durable by inserting their own fixtures
  instead of depending on seed contents.

## [v0.13] — 2026-05-24 — M1.4 read-only Notebook tools + UX overhaul

The biggest M1 commit. Adds `read_file` + `list_directory` as
auto-executed read-only tools alongside the three approval-gated write
tools. Active-project dropdown in the sidebar narrows scope from "all
registered" to "one chosen". Full constitution-style system prompt
loaded from `Assets/notebook-system-prompt.md`. One bubble per user
turn (chips above prose, accumulating across iterations). Empty-response
fallback. Catches non-ASCII API keys on save and use. (`7c83547`)

- **Added** `ReadFile` / `ListDirectory` on `IAgentActionService` (scoped-
  roots-confined). Two read-only tool schemas declared on every
  `AgentChatAsync` call.
- **Added** `ToolActivity` chip type, `NotebookMessage.Activities`
  collection, `BoolToToolActivityBrushConverter`.
- **Added** `Assets/notebook-system-prompt.md` loaded at startup with
  `{{scoped_roots}}` / `{{active_project}}` / `{{provided_files}}`
  substitution per turn.
- **Added** Active-project dropdown bound to registered projects.
- **Changed** `RunAssistantTurnAsync` accumulates one bubble across all
  auto-loop iterations; iteration cap removed (Cancel button is the
  brake).
- **Changed** `DpapiKeyStore.SaveKey` + `AnthropicChatService.BuildRequest`
  reject non-ASCII characters in the API key with a clear message.
- **Added** Five `AgentActionServiceTests` covering ReadFile /
  ListDirectory scoping + truncation. Tests: 27 → 32.

## [v0.12] — 2026-05-24 — M1.3 Cancel on long AI calls

Every async `[RelayCommand]` that hits the Anthropic API gets
`IncludeCancelCommand = true` plus an `OperationCanceledException` catch
that surfaces "Cancelled." rather than an error. Cancel button next to
each action, `IsVisible` bound to `IsBusy`. (`3c2c6bc`)

- **Added** Cancel buttons for: Notebook Send + ExecuteActions,
  PromptManager Redesign + Generate, Documentation RunSemantic,
  SessionBuilder RunReview.

## [v0.11] — 2026-05-24 — M1.2 Open in Claude Code button

New `IClaudeCodeLauncher` in Core + `ClaudeCodeLauncher` in App that
probes the PATH for `claude`, launches it in a new cmd window with the
project as cwd, or falls back to copying `cd "<path>" && claude` to the
clipboard. Button on the Projects tab editor. (`942d864`)

## [v0.10] — 2026-05-23 — Full project documentation + v1.0 roadmap

Five docs land together to give the project a real documentation surface
before more code piles up. (`8ef8075`)

- **Added** `ROADMAP.md` (forward-looking v1.0 plan across five
  milestones, with a content sketch for the curated prompts seed).
- **Added** `CHANGELOG.md` (reverse-chrono history, this file).
- **Added** `docs/USER_GUIDE.md` (module-by-module walkthrough).
- **Added** `docs/ARCHITECTURE.md` (technical overview for contributors).
- **Changed** `README.md` rewritten as a landing page with a doc index.

## [v0.9] — 2026-05-23 — Git-aware staleness

Documentation Manager's structural pass now layers Git history on top of
filesystem mtime. (`a31c59f`)

- **Added** `GitInfo` helper that shells out to `git log -1 --format=%ct`
  with safe argument quoting and a 5-second timeout; silently no-ops when
  git is missing, the folder isn't a repo, or a file has no commits.
- **Added** new findings: `Stale doc (Git)` (Warning) when a doc's last
  commit lags the project's most recent commit by ≥ 60 days,
  `Uncommitted changes` (Info) when FS mtime is newer than the last
  commit, `Untracked doc` (Info) when a doc has no commits.
- **Changed** `IDocReconciliationService.AnalyzeStructuralAsync` signature
  to take a `projectRoot` parameter.

## [v0.8] — 2026-05-23 — Real `tool_use` Notebook + Projects tab

Notebook switched from a structured-JSON shim onto Anthropic streaming
`tool_use`, and a Projects tab landed alongside so the new flow is testable
from the UI. (`3ec12f3`)

- **Added** Projects tab with full CRUD (Name / Description / FolderPath
  via picker / Status).
- **Added** `IProjectStore.Changed` event; Home, Documentation, and
  Notebook subscribe and live-update.
- **Changed** Notebook uses `AgentChatAsync` with three real tools
  (`create_file`, `create_folder`, `move`) declared with explicit JSON
  Schemas. Streaming text deltas appear live via a new `NotebookMessage`
  class with an observable `Text`.
- **Changed** `tool_use` blocks land in PendingActions; Execute posts
  `tool_result` blocks back and re-calls `AgentChatAsync` to continue the
  turn. Clear injects `is_error=true` results so history stays valid.
- **Removed** the JSON-regex shim and its system-prompt instructions.

## [v0.7] — 2026-05-23 — Streaming + tool_use plumbing

Foundation for v0.8 — touched the AI client without changing user-facing
behavior. (`0e23423`)

- **Added** `AgentTurn` / `AgentContentBlock` (Text / ToolUse / ToolResult),
  `AgentTool`, `AgentChatResponse` in Core.
- **Added** `IAiService.AgentChatAsync` (streaming + tool_use).
- **Added** SSE parsing in `AnthropicChatService` over an injectable
  `HttpClient`. Test-only constructor accepts a pre-configured client.
- **Added** five `AnthropicChatServiceTests` covering text-delta streaming,
  tool_use reassembly, request body shape, error events, and
  non-streaming fallback.
- **Kept** existing `CompleteAsync` / `ChatAsync` for non-streaming
  callers (DocReconciliation, PromptManager, SessionBuilder).

## [v0.6] — 2026-05-23 — Prompt version history

Every content-changing save now snapshots the prior row for restore.
(`bd9354c`)

- **Added** `PromptVersion` Core model + `IPromptStore.GetVersionsAsync`.
- **Added** STRICT `prompt_versions` table with `FK ON DELETE CASCADE`
  and a `(prompt_id, captured DESC)` index.
- **Added** `SqlitePromptStore.UpdateAsync` runs in a transaction:
  conditional `INSERT … SELECT … WHERE content changed`, then the UPDATE.
- **Added** History view in Prompt Manager (full-pane, same DockPanel
  pattern as the redesign view); Restore loads a version into the editor.
- **Added** four tests for snapshot-on-content-change, no-snapshot-on-
  usage-count-only, descending order, and FK cascade.

## [v0.5] — 2026-05-23 — Inline colored diff for AI Redesign

(`0fa7690`)

- **Added** `DiffPlex` 1.7.2 dependency.
- **Added** `DiffLine` record + `DiffLineKind` enum +
  `DiffLineKindToBrushConverter` (translucent green / red / transparent).
- **Changed** Redesign panel renders an `ItemsControl` over diff lines
  instead of a plain readonly TextBox.
- **Added** `Apply & Save` command alongside the existing "Apply to
  editor only" and "Dismiss".
- **Changed** Right pane restructured to a `Grid` with mutually-exclusive
  children; redesign view docks header top, action buttons bottom, diff
  fills middle and scrolls internally.
- **Changed** `EditBody` capped at `MaxHeight=320` to keep the action row
  visible when bodies grow long.

## [v0.4] — 2026-05-23 — SQLite FTS5 search in Prompt Manager

(`be760ab`)

- **Added** FTS5 external-content virtual table `prompts_fts` over
  `prompts.rowid`, plus AFTER INSERT / UPDATE / DELETE triggers.
- **Added** first-time backfill so an existing user DB migrates without
  losing prompts.
- **Added** `IPromptStore.SearchAsync`; sanitizes user input via token
  regex (`[\p{L}\p{N}_]+`) → double-quoted + prefix-matched tokens.
- **Changed** `PromptManagerViewModel.ApplyFilter` is async; routes
  through the store. Category filter still applied in-memory.
- **Added** `SqlitePromptStoreTests` (7) covering empty query, title /
  tag match, INSERT / UPDATE / DELETE trigger sync, FTS5 operator
  sanitization.

## [v0.3] — 2026-05-23 — Drag-and-drop file staging

Session Builder Step 3 accepts dropped files. (`aa957d9`)

- **Added** named `FileDropZone` Border with `DragDrop.AllowDrop=True`
  and a faded "Drop files here" hint visible while the staged list is
  empty.
- **Added** `SessionBuilderViewModel.AddFiles(IEnumerable<string>)` —
  bulk version of `AddFile`; reports added / duplicate / missing counts.
- **Added** code-behind handlers for `DragOverEvent` / `DropEvent`;
  resolves files via `IStorageItem.TryGetLocalPath`.

## [v0.2] — 2026-05-23 — `IFilePickerService` + Browse buttons

Native folder/file pickers in three modules. (`7f696b7`)

- **Added** `IFilePickerService` in Core (framework-free) with
  `PickFolderAsync` and `PickFileAsync`; `FilePickerFileType` record.
- **Added** `AvaloniaFilePickerService` resolving the active `MainWindow`
  lazily via `IClassicDesktopStyleApplicationLifetime`.
- **Added** Browse buttons in Documentation (folder), Session Builder
  (output folder + new file), Settings (output folder).
- **Changed** Marked `DpapiKeyStore`, `Program`, and `App` with
  `[SupportedOSPlatform("windows")]` — CA1416 warnings went from 4 to 0.
- **Fixed** Stale `README.md` status sentence ("navigable stubs" →
  "feature-complete v1").

## [v0.1] — 2026-05-23 — Initial scaffold + build

First real commit. (`4220bf0`)

- **Added** Four-project solution: `Core`, `Services`, `App`, `Tests`.
  Layered Core ← Services ← App.
- **Added** All five modules (Documentation, Prompts, Session Builder,
  Notebook, Skill Library) plus shell / Home / Settings.
- **Added** SQLite stores (Project / Prompt / Note), JSON settings,
  DPAPI key store, stub + Anthropic AI services, AgentActionService
  with scoped-roots + undo.
- **Fixed** Bumped Avalonia 11.2.1 → 11.3.0 so `Grid.RowSpacing` and
  `Grid.ColumnSpacing` (used in four views) resolve.
- 11 / 11 xUnit tests green, `dotnet run` launches.

---

<!-- Commit SHAs for each version (use `git show <sha>` to inspect):
     v0.1:  4220bf0    v0.7:  0e23423    v0.13: 7c83547    v0.19: 9bf2e69
     v0.2:  7f696b7    v0.8:  3ec12f3    v0.14: 8044ea9    v0.20: bcf0f3d
     v0.3:  aa957d9    v0.9:  a31c59f    v0.15: b9a250d    v0.21: be11670
     v0.4:  be760ab    v0.10: 8ef8075    v0.16: 5810c49    v0.22: 13ed1c2
     v0.5:  0fa7690    v0.11: 942d864    v0.17: 31424a6    v0.23: 1e53911
     v0.6:  bd9354c    v0.12: 3c2c6bc    v0.18: b7ac51f    v0.24: 16f9468
     v0.25: 36a3b08    v0.26: 935aff0    v0.27: 4cf2772    v0.28: d71e7a9
     v0.29: 44d3ec4    v0.30: 2611b9c    v0.31: 844dd05    v0.32: 568a636
-->
