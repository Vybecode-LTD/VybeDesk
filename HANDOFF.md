# Handoff — ClaudePM

> A handoff package for a new Claude Code session agent picking up
> ClaudePM development cold. If you're that agent: read this all the
> way through *before* touching any other doc.

## TL;DR

ClaudePM is a Windows desktop app (Avalonia 11.3 + .NET 9) that helps
you manage Claude-Code-driven work — documentation reconciliation, a
curated prompt library, claude.ai → Claude Code handoff packages,
a streaming `tool_use` agent for scoped filesystem actions, a
project-scoped Bug Tracker (v0.26), and (as of v0.27) a project-scoped
Testing Manager that picks a strategy and generates Claude Code setup
and regression prompts. **Three of six roadmap milestones (M1, M2,
M2.5) are shipped + a deep non-roadmap polish pass + the new Bug
Tracker and Testing Manager modules**; M3 / M4 / M5 / M6 remain.
Build is green; all tests pass (69 / 69 as of v0.27).

**v0.27 (this version): Testing Manager module landed** — built
from `docs/build-prompts/testing-manager.md`. **Data-driven stepped
wizard (Pattern C)** — one question at a time via ContentControl,
Back/Next/See-recommendation navigation, no ScrollViewer in the
questionnaire path. Pattern decision and two alternatives (A and B)
documented in `docs/design-patterns/testing-manager-wizard-options.md`
so a future agent can pivot without rediscovery. Five-question
questionnaire → recommendation → saved per-project `TestingPlan` →
generated Claude Code setup prompts (from a built-in catalog of 7
frameworks) and regression-test prompts. **One new cross-module
event**: `IBugFixedNotifier` (in Core). Bug Tracker fires on
Open/Fixing → Fixed transition; Testing Manager listens and surfaces
a nudge banner. The event is the ONLY thing the two modules share —
don't reach into either's internals from the other.

**Layout convention reinforced.** The first v0.27 iteration hit the
same family of cut-off bug as the v0.24 Skill Library saga
(`ScrollViewer` content unreachable past the viewport, no scrollbar
appearing). Root cause: a Grid column without explicit RowDefinitions
passes an infinite vertical measure to a ScrollViewer descendant —
see [issue #2701](https://github.com/AvaloniaUI/Avalonia/issues/2701)
and [#3772](https://github.com/AvaloniaUI/Avalonia/issues/3772).
The canonical fix: **outer container = `DockPanel LastChildFill="True"`,
docked Border for the sidebar/rail, ScrollViewer (or stepped wizard)
as the unset fill child.** `NotebookView`, `TestingManagerView` (since
v0.27), and `BugTrackerView` (defensively, also v0.27) all follow this
shape. Do NOT introduce new views with the old
`<Grid ColumnDefinitions="N,*">` shape — copy NotebookView or
TestingManagerView as the reference instead.

**v0.26 backstory: Bug Tracker module landed** — project-scoped
defect log. Severity-sorted list, three separate reproduction fields
(Steps / Expected / Actual), Generate Fix Prompt command. Takes the
Module 5 sidebar slot that v0.25 left empty.

**v0.25 backstory: the previous Module 5 — Skill Library Manager —
was removed wholesale and is scheduled for a clean rewrite post-v1.0.**
The original implementation lived through v0.24 but a stubborn
Resources/Validation display bug exhausted 9 layout iterations.
Rather than land a 10th attempt, the user chose to remove the
module entirely and rebuild it from scratch after the other
modules ship. If you see references to "Skill Library" or "skill
files" in older docs/code-history, treat them as archaeology — the
current Module 5 is the Bug Tracker.

## Read order

In this order, skip nothing:

1. **This file** (you're reading it) — orientation, conventions,
   gotchas, roadmap pointers, starting prompt.
2. **[CLAUDE.md](CLAUDE.md)** — "Last Completed Task" tells you exactly
   what shipped last and what's next.
3. **[ROADMAP.md](ROADMAP.md)** — the full v1.0 plan, with completed
   milestones marked. Items have S/M/L scope tags.
4. **[CHANGELOG.md](CHANGELOG.md)** — versioned history of every commit
   with Added/Changed/Fixed/Removed. v0.24 is current.
5. **[SPEC.md](SPEC.md)** — original product spec for the eight
   modules. Some details have evolved (e.g. AI client now direct HTTPS,
   not the SDK; `save_note` is a user button, not an AI tool); the
   `## Last Completed Task` in CLAUDE.md is authoritative for current
   state when in doubt.
6. **[docs/USER_GUIDE.md](docs/USER_GUIDE.md)** — module-by-module
   walkthrough. Read at least the modules touched by your next task.
7. **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — technical
   reference. Stack, layering, persistence schemas, AI client,
   `MarkdownPresenter`, Project Audit flow, threading.
8. **[KICKOFF.md](KICKOFF.md)** — historical. The original bootstrap
   prompt. Most "deferred" items there have shipped; included for
   archaeology, not for current planning.

If you're being asked to do feature work, **first verify the build
works** (next section). Don't trust that the repo is in a runnable
state just because it builds — there can be data-state issues (DB
schema bumps that haven't been migrated yet, stale `.claude/`
settings, etc.).

## Quick verify

```pwsh
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ClaudePM.App
```

Expect: 69 / 69 tests pass, the app window opens with **nine** sidebar
entries (Home / Projects / Documentation / Prompts / Session Builder
/ Notebook / Bug Tracker / Testing Manager / Settings). Bug Tracker
is Module 5 (v0.26), Testing Manager is Module 6 (v0.27). The v0.24
Skill Library that previously held the Module 5 slot was removed in
v0.25.

If you can't get the app running, **stop and ask the user before
touching anything else**. A broken build is a strong signal that
context has drifted.

## Conventions (NON-NEGOTIABLE)

These are encoded in `CLAUDE.md` and enforced socially via code
review. Violations should be caught early, not at PR time.

- **Layering**: strict one-directional dependency. `Core ← Services ←
  App`. Core has no framework deps (no Avalonia, no SQLite). Services
  knows persistence + AI client. App owns the UI and DI composition
  root.
- **MVVM**: every ViewModel is a `partial` class using
  `CommunityToolkit.Mvvm` source generators. Missing `partial` silently
  breaks `[ObservableProperty]` and `[RelayCommand]` — easy to miss.
- **Compiled bindings everywhere**: every `DataTemplate` has
  `x:DataType="..."`. Without this Avalonia falls back to reflection
  and silently swallows binding errors.
- **AI calls through `IAiService` only.** Never instantiate
  HttpClient against `/v1/messages` from a ViewModel. Never import
  the Anthropic SDK directly.
- **Filesystem actions from AI must use the preview/execute/undo
  gate.** `AgentActionService.ExecuteAsync` is the only path that
  mutates disk on behalf of Claude. Direct writes from VMs to user
  project folders are forbidden (user-driven Save in the doc editor
  is the only exception, and that's user input, not agent action).
- **Scoped roots**: agent file actions must canonicalize inside one
  of `IAgentActionService.ScopedRoots`. Anything outside is rejected
  by the validator.
- **API key**: stored via `DpapiKeyStore` (Windows DPAPI). Never write
  it to disk in plaintext, never to CLAUDE.md, never to git. Validate
  ASCII on save and use (Anthropic rejects non-ASCII header chars).
- **Update CLAUDE.md "Last Completed Task" at the end of every
  session.** New agents read it first to orient.
- **Naming**: Views end in `View`, ViewModels in `ViewModel`, services
  in `Service`. Models are noun phrases.
- **Smoke test after EVERY update (NON-NEGOTIABLE).** After every
  commit that changes user-visible behavior — every view edit,
  every VM-bound property change, every new command, every layout
  tweak, every feature — launch the app and wait for the user to
  visually verify before declaring done OR starting the next
  change. This is the stronger version of an earlier "at milestone
  boundaries" rule; the user explicitly upgraded it because the v0.24
  Resources bug burned nine layout iterations in a row, each one
  passing tests and looking "done" before the user smoke-tested it
  and rejected it. Per-update verification catches a regression at
  iteration 2 instead of iteration 9. Doc-only commits and
  pure test-only commits don't need a launch. Steps: kill any
  running `ClaudePM.App` process first (DLLs lock per the gotchas
  section), rebuild if needed, then launch in the background —
  `dotnet run --project src/ClaudePM.App` — so the window pops on
  the user's screen. Tell the user explicitly *what to verify* in
  THIS specific commit (not a generic "does everything still
  work"). Then wait. Do not queue up the next change in the same
  turn. If the user explicitly says "skip the smoke test" or "just
  keep going" for a specific scope, respect that scope.

## Closed in v0.25 — Skill Library Resources display (by deletion)

The Resources/Validation cut-off bug that consumed nine layout
iterations through v0.24 was resolved in v0.25 by **removing the
Skill Library module entirely**. The user chose the clean-slate
rewrite over a 10th layout iteration. Module 5 is gone from
`main`; it will be redesigned and rebuilt post-v1.0.

If you're picking up after v0.25 and someone asks "what was that
bug about?": the data flow was always correct (tests verified
`GetResources` returned the right items), but the Resources list
and the Validation list below it rendered as "cut off" through
nine different sizing/layout permutations (`MaxHeight`, fixed
`Height`, `ItemsControl`-in-`ScrollViewer`, `ListBox`-in-`Border`,
`Grid RowDefinitions="Auto,*"`, etc.). Hypotheses never tested:
DPI scaling, a global theme style on `ListBox` / `ScrollViewer`,
an Avalonia 11.3-specific nested-`ScrollViewer` bug. When Module 5
is rewritten, treat those as the first three things to rule out
before committing to a layout approach.

**Do not reintroduce `SkillFile`, `SkillResource`, `ISkillLibraryService`,
or anything under `Services/Skills/` without an explicit design
discussion.** The rewrite should not be a port of the old code.

## Gotchas & paper cuts (current state)

Things that bit us in development and might bite you:

- **App locks DLLs while running.** A build during a running session
  hits `MSB3027` / `MSB3021` lock errors. Always stop the app
  (`Get-Process ClaudePM.App | Stop-Process`) before rebuilding if
  it's been launched in this session.
- **Markdown.Avalonia is a no-go.** We tried 11.0.2 in every binding
  mode (always-visible, IsStreaming-toggled, HasText-gated); it
  silently blanks the bubble. Don't reintroduce it. The custom
  `MarkdownPresenter` (Markdig parser + native Avalonia walker) is
  what works.
- **Star columns in Avalonia don't grow to fit content.** They take
  proportional share regardless of what's inside. To make a column
  fit content, use `MinWidth` on the `ColumnDefinition` (see
  `MarkdownPresenter.RenderTable` for the header-width-estimate
  pattern).
- **Run elements don't have `Opacity`.** Use `Foreground="#7F7F8A"`
  for "dim" inline text instead.
- **Static fields aren't visible to compiled bindings.** If you want
  a dropdown bound to a static list, expose it as an instance
  property: `public IReadOnlyList<T> Foo => Catalog;` (see
  `SettingsViewModel.AvailableModels`).
- **Smart-quote / em-dash API keys.** Anthropic rejects non-ASCII
  header values. `DpapiKeyStore.SaveKey` and `AnthropicChatService`
  both validate and surface a clear error. Don't relax that — it
  catches real copy-paste typos.
- **`ToolSearch` for deferred tools.** When the agent needs a tool
  like `WebFetch` that's deferred, load via `ToolSearch` first;
  schemas aren't loaded by default.
- **Iteration cap on the Notebook auto-loop is OFF.** The Cancel
  button is the brake. If a complex audit / inspection task spins
  forever, that's the user's call — don't reintroduce the cap silently.
- **README has user smoke-test edits sometimes.** If `git status`
  shows README dirty, check before committing — the user may have
  added test characters while verifying watch mode. Leave their edits
  alone unless they ask you to clean them up.
- **Anthropic model IDs change.** The dropdown catalog in
  `SettingsViewModel.ModelsCatalog` was caught with a fabricated ID
  (`claude-sonnet-4-7` doesn't exist). When adding new models, verify
  against `https://docs.claude.com/en/docs/about-claude/models/overview`
  — don't guess.

## Where to start

**Top of mind: more user-authored module specs in the working tree.**
At handoff there are three untracked files: `build-prompt-skill-builder.md`
(probably the Module 5 / Skill Library rewrite), `build-prompt-vision-audit.md`
(new module), and `integration-prompt-skill-module.md` + the
companion `ClaudePM-skill-module.zip` (a pre-packaged Skill Library
replacement). Pick one of these as the next module, or check with the
user — they may have a strict order in mind.

**Other reasonable next directions:**

**M3 — Smarter Notebook + telemetry** (the Skill Library bug that was
first-priority through v0.24 was closed by module deletion in v0.25):

> Persistent agent action log per project (move `UndoHistory` from
> in-memory to a SQLite `agent_actions` table), "Apply with AI"
> button on documentation fix prompts (routes through the Notebook),
> AI call log + cost tracking (SQLite `ai_calls` table + Activity
> view in Settings — which would also surface the prompt-caching
> savings that already ship as of v0.23), streaming token meter in
> the busy chip.

The single highest-leverage standalone M3 item is **"Apply with AI"
for fix prompts** — closes the doc-reconciliation loop (audit → fix
prompt → Notebook executes the fixes through the existing safety
gate). Small VM change, no schema migration, big UX win.

Other reasonable directions:

- **M4 — Real project hub**: importing existing `.claude/` directories,
  Session Builder templates, per-project model/output overrides.
- **M5 — Landing dashboard + polish**: Home health cards, light theme
  done properly (every dark hex in the views moves to
  `DynamicResource`), v1.0 release polish.
- **M6 — Skill Library rewrite** (the v0.25 successor to the
  removed Module 5): clean-slate redesign of the Skill Library
  Manager. Treat the v0.24 implementation as inspiration only,
  not a starting point. See ROADMAP.md items 19–22 and the
  "Closed in v0.25" section above for hypotheses worth ruling out
  early (DPI scaling, global theme styles on `ListBox` /
  `ScrollViewer`, Avalonia 11.3 nested-`ScrollViewer` quirks).

**Non-roadmap polish that's still on the table** — see the
"Optimizations / improvements worth considering" section below. The
session-1 audit-driven Tier 1 batch already shipped (`b7ac51f`,
`00e82e4`, `d37aa74`, `9bf2e69`); Tier 2 (theme dictionary →
prereq for M5 light theme, MarkdownPresenter as a reusable style
resource, VM folders) is still available.

## Optimizations / improvements worth considering

Things that aren't on the roadmap explicitly. Items marked ✅ SHIPPED
this session are kept for continuity; the rest are still open.

### Code quality
- **Extract per-module ViewModels into folders.** Currently flat under
  `App/ViewModels/`; once we have ~15 VMs the namespace will get noisy.
- **Pull all hardcoded hex colors into a theme dictionary.** Required
  for the M5 light theme anyway; doing it incrementally now is cheaper
  than a Big Bang.
- **Test coverage for `MarkdownPresenter`.** It renders to Avalonia
  controls so end-to-end testing is awkward, but the parsing layer
  (Markdig configuration, fence handling) is unit-testable in isolation.
- ✅ **SHIPPED v0.19 (`00e82e4`)** — Test coverage for `AuditAsync`
  JSON parsing. 9 golden-input tests across the response shapes Claude
  actually returns.
- **VM-level smoke tests for layout-bound state.** Lesson from the
  v0.24 Resources bug: a unit test asserting `Resources.Count` and
  `ResourcesHeader` update on selection would have proven the VM
  was fine and pointed at the layout earlier. Apply this pattern
  to any future VM whose UI surfaces a list bound to a collection
  property.

### UX
- ✅ **SHIPPED v0.19 (`d37aa74`)** — "thinking…" placeholder in
  empty Notebook bubble.
- ✅ **SHIPPED v0.19 (`d37aa74`)** — Ctrl+Enter to send in Notebook,
  Ctrl+S to save in doc editor.
- **Ctrl+K command palette** is still v1.1+.
- **Copy 📋 icon.** The per-finding 📋 button that shipped in
  v0.23 (`e9f6464`) went away with Module 5 in v0.25. Other Copy
  buttons (Notebook messages, audit prompts, etc.) still use the
  word "Copy" — converting them to 📋 is still open.
- **Per-project conversation history.** Notebook conversation resets
  on app restart; persisting per-project would let users resume mid-
  thought.

### Architecture
- ✅ **SHIPPED v0.23 (`dee7f17`)** — Anthropic prompt caching on
  system + last tool, both streaming and non-streaming paths. See
  ADR-0006. The PARTIAL caveat: there's no in-app surface for
  `cache_creation_input_tokens` / `cache_read_input_tokens` yet —
  M3 #12 (AI call telemetry) is the right home.
- **AI cost tracking visibility** is still missing. The Anthropic
  billing dashboard is the only feedback today.
- **Two-layer system prompt.** The notebook constitution
  (`Assets/notebook-system-prompt.md`) is loaded at startup, but the
  per-turn context substitution happens in code. A more flexible
  system would let users edit per-project system prompts too.
- **Markdown rendering pluggability.** Currently `MarkdownPresenter`
  is used in Notebook + audit Design section. As more places want
  rendered markdown, consider making it a reusable Avalonia style
  resource rather than per-view inclusion.

### Safety
- ✅ **SHIPPED v0.18 (`b7ac51f`)** — Symlink resolution in
  `AgentActionService.TryConfine`. Walks segments, resolves each
  existing prefix via `FileSystemInfo.ResolveLinkTarget`.
- ✅ **SHIPPED v0.18 (`b7ac51f`)** — 429 / 503 / 529 retry backoff
  in `AnthropicChatService`. Up to 3 retries, honors `Retry-After`,
  exponential backoff with jitter capped at 1 minute.
- **Audit + Apply with AI cycle limit.** When M3 ships "Apply with
  AI" for fix prompts, make sure it can't infinitely loop (audit →
  apply → re-audit → ...).
- **Session-3 audit findings (B-bucket) still open:** FileSystemWatcher
  not unsubscribed before Dispose in `DocumentationViewModel`;
  `DocumentationViewModel` isn't `IDisposable` (watcher leaks until
  GC); `_projects.Changed` subscribed in 2+ VMs without
  unsubscription (safe today because VMs are singletons);
  `_debounceCts` cancelled but not disposed; `Task.Run<T>(..., ct)`
  doesn't pass `ct` to the inner loop in `ScanAsync` /
  `AnalyzeStructuralAsync`. See CHANGELOG v0.24 entry and the
  Session 3 audit summary for full list.

### Documentation
- ✅ **SHIPPED v0.19 (`9bf2e69`)** — `docs/adr/` folder with five
  ADRs (Markdig, direct HTTPS, DPAPI, no-iteration-cap,
  audit-as-structured-JSON).
- ✅ **SHIPPED v0.23 (`1e53911`)** — ADR-0006 for the prompt
  caching strategy.
- **CONTRIBUTING.md** still pending — planned for v1.0; the
  conventions section here + Architecture doc + ADRs are a decent
  start when extracted.

## Starting prompt for the next session agent

Paste this into a new Claude Code session running against this repo:

```
You're picking up development on ClaudePM, a Windows desktop app
(Avalonia + .NET 9) for managing Claude-Code-driven work. The repo
has a complete handoff package — read it carefully before doing
anything else.

Read in this order:
1. HANDOFF.md (the orientation package, read it all, INCLUDING the
   "Closed in v0.25" section explaining why Module 5 was deleted)
2. CLAUDE.md (Last Completed Task tells you exactly where we are)
3. ROADMAP.md (what's left for v1.0 — M3, M4, M5, M6 remain;
   M6 is now the Skill Library *rewrite*, not "Builder")
4. CHANGELOG.md (versioned history; v0.25 is current — read v0.18
   through v0.25 for the full recent arc)
5. docs/ARCHITECTURE.md (technical reference for the modules you'll
   touch — also covers prompt caching strategy + retry policy)
6. docs/adr/ — ADRs documenting non-obvious technical decisions

After reading, do these in order:
1. Verify the build: `dotnet restore && dotnet build && dotnet test`.
   All tests should pass.
2. Run the app: `dotnet run --project src/ClaudePM.App`.
   The window opens MAXIMIZED. Confirm it launches with **seven**
   sidebar entries (Skill Library was removed in v0.25).
3. Tell me a one-paragraph summary of: (a) what shipped most recently
   (start at v0.18 — note v0.25 is a removal; v0.26 and v0.27 are
   user-authored-spec-driven module adds), (b) any conventions or
   gotchas from HANDOFF.md you want me to confirm before you touch
   the code.

Then wait for me to direct the next task. Don't start work until I
confirm the direction. **Do NOT scaffold a Skill Library replacement
without an explicit go-ahead** — the v0.24 implementation was removed
on purpose in v0.25, will be rewritten from scratch post-v1.0, and
should not be ported back. **The current Module 5 is the Bug Tracker
(v0.26).**

If anything in the repo looks wrong (build fails, docs contradict
each other, the audit overlay surfaces inconsistencies), tell me
before patching. Don't make documentation changes without showing
me first.

You can run the Project Audit yourself from the Documentation tab
on this very repo as a sanity check — pick the ClaudePM project,
click Scan, then Audit Project. The audit's "Generate Fix Prompt"
output is a good starting point if you need to clean up doc drift.

Conventions you must respect (full list in HANDOFF.md):
- Layered architecture: Core ← Services ← App, strict one-direction.
- MVVM via CommunityToolkit source generators; all ViewModels
  `partial`, all bindings compiled. **For derived properties
  (HasSelection / IsEditorVisible / etc.) you MUST put
  `[NotifyPropertyChangedFor]` on the SOURCE `[ObservableProperty]`
  field — the attribute doesn't inspect what the derived property
  reads.** Skipping this dropped editor visibility in mid-session.
- All AI calls through IAiService; never instantiate HttpClient
  against the Anthropic endpoint from a ViewModel.
- Agent filesystem actions only through AgentActionService — preview
  / execute / undo gate, scoped to registered project roots
  (now symlink-resolved as of v0.18).
- API key validated ASCII-only on save and on use.
- Update CLAUDE.md "Last Completed Task" at the end of every session.
- **Smoke test after EVERY update (NON-NEGOTIABLE)** — see the
  Conventions section for the full protocol. After every commit that
  changes user-visible behavior, launch the app and wait for the
  user to visually verify before declaring done OR starting the
  next change. Doc-only / test-only commits exempt. The v0.24
  Resources bug saga is why this rule is stronger than "milestone
  boundaries" — 9 iterations passed tests and burned the user's
  patience.

If you're unsure about scope on a task, ask. Bigger blast radius
than expected = pause and check, every time.
```

## Repo state at handoff

```
Branch:    main
Latest:    v0.27 — Testing Manager module (new Module 6) +
           Bug Tracker ↔ Testing Manager event
Tag:       AlphaV0.5.0 (end of M1)
Build:     ✓ clean
Tests:     69 / 69 pass
Modules:   9 sidebar pages — Home / Projects / Documentation /
           Prompts / Session Builder / Notebook / Bug Tracker /
           Testing Manager / Settings
Open bug:  none (the v0.24 Skill Library Resources bug was closed
           by module deletion in v0.25)
Recent:    v0.18 safety hardening · v0.19 Tier 1 tests+UX+ADRs ·
           v0.20 smoke-test convention + Notebook bubble fix ·
           v0.21 Skill Library Browse + dual-format scan/export ·
           v0.22 Skill rename + clickable chips + Resources concept ·
           v0.23 prompt caching + Roadmap M6 + per-finding Copy ·
           v0.24 doc maintenance close-out ·
           v0.25 Skill Library module removed pending rewrite ·
           v0.26 Bug Tracker module (new Module 5) ·
           v0.27 Testing Manager module (new Module 6) +
           Bug Tracker ↔ Testing Manager IBugFixedNotifier event.
```

Welcome to ClaudePM. The shape is solid; one module is intentionally
absent and will be rewritten later. The rest is just shipping.
