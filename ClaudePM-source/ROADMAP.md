# ClaudePM Roadmap — v1.0

> Forward-looking. For what's already shipped, see [CHANGELOG.md](CHANGELOG.md).
> For what exists today, see [docs/USER_GUIDE.md](docs/USER_GUIDE.md) and
> [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
> For the open layout-regression bug blocking M5 #17 closeout, see
> [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md).
> For the regression-testing protocol, see [docs/TESTING.md](docs/TESTING.md).

**v0.32 in-progress state (2026-05-25):** the M3 #10, #11, #14(new),
#15(new), #16(new) and `edit_file` and Redo Last and M5 #17
data+VM-layer items have all shipped (81 uncommitted files; all
tests pass). **M5 #17 View layer is BLOCKED** by an unresolved
layout regression on HomeView + ProjectsView. M3 #12 (AI call log),
M3 #13 (streaming token meter), and M5 #18 (v1.0 polish) are the
remaining roadmap items still to start.

The road from v0.31 (last committed version) to v1.0 is five milestones
(M1–M5), plus a sixth (M6 — Skill Library rewrite) deferred to
post-v1.0. Each milestone stands alone — you can stop after any of
them and have a coherent app — but they compound into the cohesive
v1.0 experience.

Scope tags: **S** = single session, **M** = a couple sessions, **L** = bigger.

## Non-roadmap modules already shipped

These are net-new modules that landed off-roadmap from user-authored
build-prompt specs in [docs/build-prompts/](docs/build-prompts/). They're
listed here so the roadmap reads as a complete picture of what exists vs
what's still ahead.

- **Bug Tracker** (v0.26, Module 6) — project-scoped defect log, severity-
  sorted list (Critical → Major → Minor; Open/Fixing above Fixed/WontFix),
  separate Steps/Expected/Actual reproduction fields, Generate Fix Prompt
  command, fixed-means-tested nudge. Spec: `docs/build-prompts/bug-tracker.md`.
- **Testing Manager** (v0.27, Module 7) — project-scoped strategy chooser.
  Five-question plain-language questionnaire → recommendation with
  reasoning → saved `TestingPlan`. Built-in catalog of 7 frameworks; Claude
  Code setup and regression-test prompt generators. Loosely coupled to the
  Bug Tracker via the shared `IBugFixedNotifier` event.
  Spec: `docs/build-prompts/testing-manager.md`.
- **Skills (rebuilt)** (v0.28, Module 5) — integrated from a user-
  delivered package and then customised: folder-format only
  (`<name>/SKILL.md`), v0.24 features re-added (Browse, Rename, Backup,
  Export, severity chips, global findings filter view, per-finding
  Copy), polish pass (TreeView with nested resources, fixed-size
  editor/viewer, app-wide button style). `SkillSectionViewModel` has an
  optional Builder slot ready for Phase 2.
  Sources: `integration-prompt-skill-module.md` (delivery package) +
  user feedback during integration.

- **Skill Builder** (v0.29, Module 5b) — Phase 2 of the Skills work.
  Stepped wizard inside the Skills section (Manager/Builder in-pane
  toggle): name + rough description + notes → optional AI clarifying
  questions → AI draft → review/edit → emit both `.skill` and
  `<name>/SKILL.md` folder forms. Validation and serialization shared
  with the Manager via `ISkillLibraryService` delegation. Per-stage
  bounded Grid layout (button rows in `Auto`, long content in `*`)
  resolves the measure-pass desync seen with single-outer-ScrollViewer-
  over-IsVisible-stages. Spec: `docs/build-prompts/skill-builder.md`.
- **Vision Audit** (v0.30, Module 8) — project-scoped drift detector.
  Distil vision from docs → approve → choose mode (structural or
  targeted) → run + review. Per-statement OnTrack / AtRisk / OffTrack
  with evidence + recommendation. Persisted audit history per project
  (user-requested addition; originally out-of-scope in the spec).
  Generates a Claude Code deep-dive prompt for line-level verification.
  Spec: `docs/build-prompts/vision-audit.md`.

All four user-authored specs from the working tree have shipped.

## Milestone 1 — Out-of-the-box useful  ✓ SHIPPED

> Commits: `942d864` (M1.2 Open in Claude Code), `3c2c6bc` (M1.3 Cancel),
> `7c83547` (M1.4 read-only tools + Notebook UX overhaul), `8044ea9` (M1.5
> curated prompts + M1 close-out). M1.1 (light theme) deferred to M5 polish
> — proper `DynamicResource` migration is M-scope, not S-scope.

First-launch experience should feel populated, not empty.

1. **Curated prompts library seed** *(M)* — ~30 prompts in 5 categories seeded
   on first DB creation. Categories: Doc/VCS hygiene, Testing & regression,
   Efficient task execution, New session starters, Common dev tasks.
   See appendix at the bottom of this file for the prompt content sketch.
2. **Read-only Notebook tools `read_file` + `list_directory`** *(S)* —
   Auto-execute (no preview gate, they're read-only). Lets Claude inspect
   before proposing edits and kills "Claude guessed wrong because it couldn't
   see what was there."
3. **"Open in Claude Code" button per project** *(S)* — Launch `claude-code`
   if on PATH; copy `cd <path> && claude` to clipboard otherwise.
4. **Cancel button on long AI calls** *(S)* — `CancellationTokenSource` per
   active call wired to a visible Cancel chip in the busy state.
5. **Light theme** *(S)* — `Application.RequestedThemeVariant` driven by a
   new `AppSettings.Theme`, exposed as a toggle in Settings.

## Milestone 2 — Author & maintain docs in-app  ✓ SHIPPED

> Commits: `b9a250d` (M2.6 inline editor + M2.7 watch mode), `5810c49`
> (M2.8 Markdig-backed custom MarkdownPresenter + M2 close-out).

ClaudePM stops being a viewer and becomes an editor.

6. **Inline text editor in Documentation tab** *(M)* — Click a doc in the
   list → loads into an editor pane with save/discard buttons. Saves go
   through scoped roots so the same safety model covers manual edits.
7. **Watch mode for Documentation** *(S)* — `FileSystemWatcher` on the
   project root. Debounce + re-run the structural pass on `.md` / `.txt`
   changes. Toggle in the controls row.
8. **Markdown rendering** *(M)* — custom Markdig-backed `MarkdownPresenter`
   control that walks the AST and emits native Avalonia controls (we tried
   `Markdown.Avalonia` 11.0.2 first; it silently blanked the bubble in every
   binding mode and ships only DLLs with no obvious style-include path).
   Renders headings, paragraphs, fenced code blocks, lists, blockquotes,
   inline code, bold/italic, links, and tables (with header-aware column
   widths). Used in the Notebook chat; can be reused anywhere prose needs
   rendering.

## Milestone 2.5 — Project Audit  ✓ SHIPPED

> Commit: `31424a6` (Project Audit + clipboard service + model picker +
> notes UX upgrade). The clipboard / model / notes work was bundled
> opportunistically since it touched the same UI surface.

The existing Documentation pass only finds doc-vs-doc contradictions.
This milestone adds the missing synthesis pass: ClaudePM reads a project's
docs and answers *"what's the state, what's complete, what's not, where do
the docs disagree?"* — the strategic snapshot you'd otherwise assemble by
hand from six separate files.

9. **Project Audit** *(M)* — New **"Audit Project"** button on the
   Documentation tab, next to "Run AI Analysis." Loads a signal-weighted,
   capped bundle of the project's docs (priority order: `CLAUDE.md` →
   `CHANGELOG.md` → `ROADMAP.md` → `SPEC.md` → `README.md` → `KICKOFF.md`
   → `docs/*.md` → everything else) and asks Claude for structured JSON:
   a design summary, a flat list of roadmap items
   (`title, status, category, source, evidence`), and an inconsistencies
   list (`severity, docs, issue`). Parses into a new `ProjectAuditReport`
   Core type. New full-pane overlay (same Grid pattern as Redesign /
   History) renders five sections:
   - **A) Project Design** — synthesized prose summary
   - **B) Roadmap — all items** — full table with status / category /
     source / evidence
   - **C) Completed** — filtered to status=complete
   - **D) Incomplete** — filtered to status=incomplete/unknown
   - **Inconsistencies** — severity-ranked, docs involved, with a
     "Generate Fix Prompt" button (matching the existing reconciliation
     flow, and wired into the M3 "Apply with AI" path when that lands)

   Bonus git cross-check (cheap, reuses `GitInfo`): for each item Claude
   marks "complete," run `git log --grep` against key terms from the
   title; zero matches in the last year of commit history adds an
   inconsistency *"Marked complete but no matching commit messages —
   verify."* Catches the "I forgot to commit / I lied to my docs" case.

## Milestone 3 — Smarter Notebook + telemetry  (partially SHIPPED in v0.32)

The agent gains memory; the user gains visibility.

10. **Persistent agent action log per project** *(M)* — ✓ SHIPPED v0.32
    (uncommitted). New `agent_actions` SQLite table
    (`project_id, kind, path, original_content, new_content, status,
    executed_at`). Replaces in-memory `UndoHistory`; cross-session undo;
    NEW cross-session **Redo Last** button reads the previously-undone row.
11. **Execute Documentation fix prompts in the Notebook** *(S)* — ✓ SHIPPED
    v0.32 (uncommitted). "Apply with AI" buttons on both the reconciliation
    fix prompt and the audit fix prompt feed the generated prompt into the
    Notebook scoped to the active project, through the existing preview/
    execute/undo gate. New `INotebookOpener` cross-VM coordinator.
12. **AI call log + cost tracking** *(M)* — STILL TO DO. New `ai_calls`
    table (`timestamp, model, module, project_id, input_tokens, output_tokens,
    cost_estimate`). "Activity" view in Settings shows recent calls + a
    running total per period.
13. **Streaming token meter** *(S)* — STILL TO DO. During `AgentChatAsync`,
    count text-delta + input-json-delta length; surface a live token count
    + a running cost estimate in the chat busy chip.

Bonus shipped this session (not in the original roadmap but landed
under the M3 umbrella):

- **`edit_file` agent tool** — fourth approval-gated tool in the Notebook
  (after `create_file`, `create_folder`, `move`). String-based Edit /
  Replace All matching Claude Code's Edit tool. Persisted to the
  agent_actions log + integrated into the Undo / Redo flow.

## Milestone 4 — Real project hub  (SHIPPED in v0.32)

ClaudePM becomes a hub for real Claude Code repos. **All three items
shipped this session as part of v0.32 (uncommitted).**

14. **Import existing project from `.claude/` + git** *(M)* — ✓ SHIPPED
    v0.32. New `IProjectImportService`. Point at a folder → ingests
    `CLAUDE.md` as the Description, seeds `Project.LastActivity` from
    `git log -1`, pulls `.claude/commands/*.md` into the Prompt library
    tagged with the project, auto-detects a logo from common filenames
    (`logo.png`, `icon.png`, `favicon.ico`). The `.claude/skills/` half
    is parked until M6 lands. Surfaces duplicate-prompt skip counts in
    the status message.
15. **Project templates in Session Builder** *(M)* — ✓ SHIPPED v0.32.
    `SessionTemplates` static catalog with 5 templates: PlainMonorepo,
    AvaloniaDotNet, FastApiPython, NextJsTypeScript, PythonCli. Step 0
    of the wizard; selecting a template seeds Step 1's description and
    pre-fills the staged files + kickoff prompt prefix.
16. **Per-project model / output overrides** *(M)* — ✓ SHIPPED v0.32.
    Nullable `Model` + `DefaultOutputPath` on `Project`.
    `AnthropicChatService` resolves `activeProject.Model ??
    settings.Model ?? DefaultModel`. ProjectsView gained a Model dropdown
    (shared `ModelsCatalog` with Settings, "(Use global default)"
    sentinel = null), freeform custom-model-ID textbox, and a Default
    output path Browse row.

## Milestone 5 — Landing dashboard + v1.0 polish

17. **Project health cards on Home** *(M)* — ⚠ **PARTIAL / BLOCKED in v0.32.**
    Data layer + VM layer SHIPPED with 6 new `ProjectHealthServiceTests`:
    `IProjectHealthService` computes stale-doc count (from a fresh
    structural reconciliation pass), commits-in-last-7-days (from
    `git log`), pending agent action count (from the new agent_actions
    log), and LastActivity timestamp; `HomeViewModel` wraps each
    `Project` in a `ProjectHealthCard` and loads metrics in parallel
    with `IsLoading` / `LoadFailed` flags; 5-card pagination; per-project
    logo bitmap loading with folder-glyph fallback. **The View layer
    (`HomeView.axaml`) is BLOCKED by the v0.32 layout regression** —
    see [docs/LAYOUT_REGRESSION.md](docs/LAYOUT_REGRESSION.md). Same
    regression also affects ProjectsView (whose edit form gained the
    Logo / Model dropdown / Default-output rows from M4 #16 and now
    overflows the viewport).
18. **v1.0 polish + bug-fix sweep** *(M)* — STILL TO DO. Pre-release
    tidy. UI consistency pass, error-message audit, end-to-end "first
    5 minutes" walkthrough, hardening on any rough edges discovered
    during M1–M4. The v0.32 layout regression is the largest known bug;
    it must be fixed BEFORE this polish pass begins.

## Milestone 6 — Skill Library rewrite (post-v1.0)

> **Status: deferred to post-v1.0.** Module 5 (Skill Library Manager) was
> implemented through v0.24 and removed wholesale in v0.25 after a
> stubborn Resources/Validation display bug exhausted nine layout
> iterations. The user chose deletion over a tenth attempt. M6 is now
> the *rewrite* of Module 5 from a clean slate, **not** an extension of
> the v0.24 code.

When M6 is picked back up, treat the v0.24 implementation as inspiration
only (the design intent — browse / edit / dedupe / validate / dual-format
export + AI-assisted authoring — is still right). Investigate these
hypotheses for the original rendering bug BEFORE committing to a layout:

- **DPI scaling.** Magic-number heights drift under non-100% DPI.
- **Global theme styles on `ListBox` / `ScrollViewer`.** A repo-wide
  resource dictionary may have been silently overriding sizing.
- **Avalonia 11.3 nested-`ScrollViewer` quirks.** Worth checking the
  upstream tracker before nesting a list inside a scrollable parent.

19–22. **(Original M6 items — kept here as design intent for the
rewrite.)** New Skill wizard with template picker / identity / body /
save steps. AI assist on description and body (rewrite-for-trigger,
generate-from-description, diff view). In-app preview rendering frontmatter
+ body as the skill loader would see it. Bulk import from arbitrary
folders, copying into both `.skill` and `<name>/SKILL.md` formats. All of
this stays the *target*; the *implementation* starts from scratch.

## After v1.0

Deferred to v1.1+:
- Doc-vs-code semantic reconciliation (SPEC.md item)
- macOS / Linux secure key stores (Keychain, libsecret)
- Tray + system integration (one-click Notebook from notification area)
- `edit_file` tool with inline diff preview
- Transcript code-block extractor in Session Builder
- Command palette (Ctrl+K) + global keyboard shortcuts
- Skill testing sandbox
- Recent activity feed across all projects

---

## Appendix — Curated prompts library content sketch

Five categories, six prompts each. Every prompt uses `{{variable}}` placeholders
where useful so the existing Fill Template flow works. Prompts are written from
the perspective of *talking to Claude inside Claude Code* — terse system
guidance for the model, not user-facing prose.

### Category 1 — Doc & VCS hygiene
1. **Initialize doc system** — set up `CLAUDE.md` / `SPEC.md` / `README.md` /
   `CHANGELOG.md` following the ClaudePM convention; populate with whatever
   you can infer from the current codebase, mark unknown sections explicitly.
2. **Audit doc-vs-code drift** — for each `.md` doc in the project, list
   anything that no longer matches the code. Prioritize CRITICAL > WARNING >
   INFO. Don't fix yet, just report.
3. **Update `CHANGELOG.md` for `{{branch}}`** — diff against `main`, group
   commits into Added / Changed / Fixed / Removed, write a single
   release-style entry.
4. **Promote "Last Completed Task" to CHANGELOG.md** — read CLAUDE.md, write
   a versioned entry for the work it describes, clear the marker.
5. **Write an ADR for `{{decision}}`** — new file under `docs/adr/NNNN-…md`,
   structure: Context / Decision / Status / Consequences.
6. **Roadmap entry for `{{feature}}`** — append to ROADMAP.md with scope tag,
   rough work plan, and explicit non-goals.

### Category 2 — Testing & regression
1. **Build a test plan** — survey existing tests, identify gaps, produce a
   prioritized backlog (critical paths first). Don't implement.
2. **Write a regression test for `{{bug}}`** — must fail on the old code,
   pass on the fix. Place in the existing test directory.
3. **Set up test infrastructure from scratch** — pick a framework based on
   stack, add scripts, write 2–3 smoke tests as starting points.
4. **Identify untestable code & propose refactors** — find areas where tests
   are hard to write, suggest the smallest refactor that unlocks coverage.
5. **Generate property-based tests for `{{module}}`** — identify invariants,
   write FsCheck/Hypothesis/Hedgehog tests as appropriate.
6. **Add a golden-path smoke test** — single e2e test of the most common
   user journey. Fast, hermetic, no flake.

### Category 3 — Efficient task execution
1. **Pre-flight before a large change** — list every file you intend to
   touch and the rough nature of the change. Wait for approval before
   editing.
2. **Smallest viable version first** *(system rider)* — implement the
   minimum change that proves the approach works, then iterate. Don't add
   features the task didn't ask for.
3. **Pause-and-plan checkpoint** — every N edits, stop and summarize what's
   done, what's left, and confirm direction.
4. **Stop on uncertainty** *(system rider)* — refuse to guess on unknown
   APIs or unclear requirements. Ask, don't invent.
5. **Constrain blast radius** *(system rider)* — only touch files listed in
   the task description. If you need to touch more, ask first.
6. **Working commit policy** *(system rider)* — every commit must build,
   tests must pass. No "WIP" commits. No "fixed previous commit" follow-ups.

### Category 4 — New session starters
1. **Pick up where I left off** — read `CLAUDE.md` + the latest `CHANGELOG`
   entry; summarize state in 3 bullets and propose the most likely next
   priority.
2. **Continue from PR #`{{n}}`** — read the PR, the diff, and any review
   comments; respond to feedback before adding new work.
3. **Sub-agent kickoff for `{{task}}`** — initialize a focused subagent
   with only the files relevant to `{{task}}` in scope.
4. **Onboarding self-brief** — produce a 5-minute orientation for a new
   contributor, derived from current docs.
5. **Day-N status check** — what changed in the last 24h, what's next,
   what's blocked. Pull from git + CLAUDE.md.
6. **Resume after long break** — re-read everything; flag anything that
   looks stale based on git activity vs doc activity.

### Category 5 — Common essential dev tasks
1. **Code review my last commit** — strict pass: bugs, unclear naming,
   missing tests, security gotchas, style drift.
2. **Refactor `{{module}}` safely** — extract / rename / restructure; tests
   must continue passing at every checkpoint. Stop and report if a refactor
   step breaks anything.
3. **Performance investigation** — profile, identify hot path, propose
   changes. Don't implement yet — propose first.
4. **Write the README for the library you just authored** — quick-start,
   API surface, examples, contributing.
5. **Database migration writer** — write forward + rollback SQL for
   `{{change}}`, include sanity checks (row counts, type compatibility).
6. **API design review** — for `{{endpoint}}`, audit: status codes, error
   shape, versioning, idempotency, auth.
