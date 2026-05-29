# Testing & Regression Framework — VybeDesk

> The testing contract for VybeDesk. Read this when adding a feature,
> fixing a bug, or before declaring any change done.
>
> Companion docs:
> - [LAYOUT_REGRESSION.md](LAYOUT_REGRESSION.md) — the specific
>   layout bug this protocol is designed to prevent another version of
> - [HANDOFF.md](../HANDOFF.md) §Conventions — the NON-NEGOTIABLE
>   smoke-test rule (this doc expands on it)
> - [memory/feedback-milestone-smoke-test.md](...) — short-form
>   feedback memory loaded into every session

## 1. The three layers

VybeDesk has three layers of verification. Each catches a different
class of bug. **All three are required.** A change isn't done until
all three pass.

| Layer | Catches | Authority | Time |
|---|---|---|---|
| 1. Build | Type errors, missing usings, broken XAML | `dotnet build` | 5–10s |
| 2. Unit tests | Logic regressions in Core + Services | `dotnet test` (207 tests today) | 1s |
| 3. Smoke test | UI behavior, layout, binding semantics | User visually verifies in the running app | 30–120s |

**Layer 3 is the one that catches the bugs the user actually cares
about.** Layers 1 and 2 are necessary but not sufficient — the v0.24
Skill Library saga (9 layout iterations, every one passing layers 1
and 2) is the cautionary tale that drove this protocol.

## 2. Unit tests — what's in scope

**Location:** `tests/VybeDesk.Tests/`
**Stack:** xUnit 2.9.2 + NSubstitute 5.3.0
**Run:** `dotnet test` from the repo root
**Target count today:** 207/207 passing

### What gets a unit test

- **Every SQLite store** (`*StoreTests.cs`) — CRUD round-trips,
  project-scoping, ordering, upsert semantics, JSON column round-trips,
  Changed-event firing, schema migration safety.
- **Every Service that has branching logic** (`*ServiceTests.cs`) —
  AnthropicChatService request shape + retry behaviour,
  DocReconciliationService finding generation, ProjectImportService
  file pickup + duplicate detection, SkillBuilderService
  validation-delegation, SessionBuilderService template selection,
  ProjectHealthService metric aggregation, AgentActionService preview/
  execute/undo state machine + the new EditFile path, StrategySelector
  pure-function recommendation logic, TestingFrameworkCatalog static
  data shape.
- **Anything called from multiple modules** — for example
  `SeverityToBrushConverter` (one set of brush mappings reused by
  Documentation findings + Bug Tracker + Vision Audit) must round-trip
  every enum value correctly.

### What does NOT get a unit test (deliberately)

- **ViewModels.** They're tested *implicitly* via the smoke test (the
  user sees the bound data). Direct VM unit tests proved low-leverage
  in early development (most VM logic is plumbing) and they're a
  maintenance burden — every property add bumps the test. The cost
  isn't paid back.
- **Views (.axaml).** No headless rendering rig is wired up. We rely
  on smoke tests for these.
- **Trivial getters / pass-throughs.** A `public string Name =>
  _project.Name` doesn't need its own test.

### What SHOULD get a unit test that doesn't today (gap list)

- **VMs with branching state machines** — `VisionAuditViewModel`,
  `SkillBuilderViewModel`, `TestingManagerViewModel`. Each has stage
  enums + transition commands; a unit test could verify
  "transitioning from Stage A via Command X lands in Stage B with
  Command Y enabled". Currently relies on smoke test.
- **NotebookViewModel.BeginFreshConversation()** — clears _history,
  Messages, PendingActions, _pendingReadResults. Wired up to fix the
  Notebook protocol-violation bug (orphan tool_use ids); should have
  a test asserting all four collections are empty afterwards.
- ~~**HomeViewModel.RebuildPagedCards()** — pure pagination logic;
  trivially testable. No test today.~~ **COVERED** — 6 VM-level
  pagination tests in `AppSmoke/HomeViewLayoutTests.cs` (v0.32).

### Regression tests added (v0.32 audit)

Three new test files under `AppSmoke/` lock in the invariants that
the layout regression and persistence bug fixes depend on:

- **`HomeViewLayoutTests.cs`** (6 tests) — VM-level pagination:
  PagedCards never exceeds PageSize (6), pagination controls are
  correct for card count, all cards have valid Project references.
- **`ProjectsViewLayoutTests.cs`** (6 tests) — VM-level form
  binding: selecting a project populates ALL edit fields (including
  M4 #16 additions), HasSelection toggles correctly, Save writes
  all fields back, blank strings map to null on the Project.
- **`ProjectSelectionPersistenceTests.cs`** (6 tests) — locks in
  the passive-null-write protection rule on ActiveProjectContext.
  SetCurrent(null) after a real project must NOT clear it; only
  ClearCurrent() can reset to null.

These gaps were flagged in [LAYOUT_REGRESSION.md](LAYOUT_REGRESSION.md)
§"What to do in the next session" — the VM-level coverage is now in
place.

### Data lifecycle tests added (v0.32 audit)

- **`SqliteProjectStoreCascadeDeleteTests.cs`** (10 tests) — proves
  that `SqliteProjectStore.RemoveAsync` cascade-deletes all
  project-scoped rows across every dependent table in a single
  transaction. Seeds one project plus rows in all 7 project-scoped
  tables (`bugs`, `testing_plans`, `vision_records`, `audit_history`,
  `agent_actions`, `notes`, `ai_calls`), calls `RemoveAsync`, and
  asserts zero rows remain. Includes isolation test (second
  project's rows survive) and `Changed` event test.

## 3. Smoke test — the NON-NEGOTIABLE protocol

**Source:** HANDOFF.md §Conventions §"Smoke test after EVERY update".

**The rule:** After every commit that changes user-visible behaviour —
every view edit, every VM-bound property, every new command, every
layout tweak, every feature — launch the app and wait for the user to
visually verify *before* declaring done OR starting the next change.

**Why:** layers 1 and 2 prove code correctness, NOT feature
correctness. The v0.24 Skill Library Resources bug consumed 9
iterations in a row, each one shipping green tests and green builds
before the user smoke-tested it and rejected it. Per-update
verification catches a regression at iteration 2 instead of iteration
9.

### The exact procedure

1. **Kill any running `VybeDesk.App` process** before rebuilding.
   Windows holds DLL locks; `MSB3027` / `MSB3021` failures during build
   are usually a forgotten running instance:
   ```pwsh
   Stop-Process -Name VybeDesk.App -Force -ErrorAction SilentlyContinue
   ```
2. **Rebuild only if you changed code** since the last successful
   build. `dotnet build` (or `dotnet build --no-incremental` after
   a `bin/obj` wipe if you suspect stale state).
3. **Launch in the background**, so the window pops on the user's
   screen without blocking the conversation:
   ```pwsh
   dotnet run --project src/VybeDesk.App
   ```
   Use the Bash tool's `run_in_background: true` for this.
4. **Tell the user EXPLICITLY what to verify in THIS commit.** Not a
   generic "does everything still work" — name the specific behaviour
   the change is supposed to produce. Example: "Projects → click any
   project → the Save button is reachable; scrolling the form reaches
   it."
5. **Wait.** Do not queue up the next change in the same turn. If the
   user explicitly says "skip the smoke test" or "just keep going"
   for a specific scope, respect that scope.

### Exemptions

- **Doc-only commits** (`*.md` files only).
- **Pure test-only commits** (only files under `tests/`).
- **Refactors with zero behaviour change** AND green tests AND you
  manually traced every callsite. Use sparingly — the "I'm sure
  this is behaviour-preserving" instinct is wrong more often than
  it's right.

If you're unsure whether a change is user-visible, **assume it is**
and smoke test.

## 4. Layout regression — the specific protocol

Layout bugs are the most expensive class of bug VybeDesk has hit.
v0.24 Skill Library Resources, v0.27 Testing Manager, v0.29 Skill
Builder, and the current open issue all share the same family
(`ScrollViewer` desyncs against an unbounded parent).
[LAYOUT_REGRESSION.md](LAYOUT_REGRESSION.md) is the open ticket.

### When you change a layout

A layout change is any edit to a `.axaml` file that:

- Adds, removes, or re-orders a panel (`DockPanel`, `Grid`, `StackPanel`, `WrapPanel`, `Panel`).
- Adds, removes, or changes a `RowDefinitions` / `ColumnDefinitions` declaration.
- Adds, removes, or changes a `ScrollViewer`.
- Adds, removes, or changes a `MaxHeight` / `MinHeight` / `Height` /
  `MaxWidth` / `MinWidth` / `Width`.
- Adds, removes, or changes a `DockPanel.Dock` / `Grid.Row` /
  `Grid.Column` / `Grid.RowSpan` / `Grid.ColumnSpan` attached property.
- Adds, removes, or changes the outer container of the UserControl
  root.
- Changes `App.axaml` styles that select on layout primitives
  (`ScrollViewer`, `ScrollBar`, `Grid`, `DockPanel`, etc.).
- Adds an `IsVisible` toggle that swaps content (especially
  scrollable content).

### The mandatory checklist (smoke test specifics for layout)

1. **Add enough content to overflow the viewport** before smoke
   testing. A form that fits a 1080p screen with three fields needs
   to be tested with fifteen fields. A card list needs to be tested
   with more cards than the window can show.
2. **Resize the window** to a smaller size and verify the layout
   doesn't break (no clipping, no buttons disappearing, no
   horizontal scrollbar appearing where it shouldn't).
3. **Verify scrollbar behaviour:**
   - Does a scrollbar appear when content overflows? (Should: yes.)
   - Does dragging the scrollbar move the content the full extent?
     (Should: yes.)
   - Does scrolling reach the LAST item / the LAST button? (Should:
     yes.)
4. **Open Avalonia DevTools** (F12 in Debug builds) and inspect:
   - The ScrollViewer's `Viewport`, `Extent`, and `Offset`
     properties. `Extent.Height` should be the true content height,
     NOT just the viewport.
   - The chain of `Bounds` from the UserControl root down to the
     ScrollViewer. Every ancestor should have a finite, non-zero
     Bounds.Height.
5. **Document the pattern.** If the change introduces a new layout
   shape, add a comment block to the .axaml file explaining the
   shape (DocumentationView.axaml lines 9-32 is a good example).

### Layout regression tests (PROPOSED — not yet wired)

[LAYOUT_REGRESSION.md](LAYOUT_REGRESSION.md) flags the need for an
automated layout-regression test rig before the open bug is closed.
The Avalonia Headless renderer
(`Avalonia.Headless.XUnit` + `[AvaloniaFact]`) can:

- Construct a window with the production XAML loaded.
- Force a specific size (`window.Width=1180; window.Height=760`).
- Set its DataContext to a stub VM with N items.
- Walk the visual tree post-layout and assert:
  - ScrollViewer `Extent.Height >= viewportHeight + (N-3) * itemHeight`
    (i.e. extent must report the real content).
  - Every "important" Button (Save / Delete / pagination) has
    `IsVisible == true` after scrolling to `Offset.Y = Extent.Height -
    Viewport.Height`.

When the LAYOUT_REGRESSION bug is closed, add three of these tests
**at minimum**: HomeView with 20 cards, ProjectsView with the
selected-project form, and DocumentationView with a 100-item findings
list. They'll be the canary for the next regression.

## 5. Running tests in CI

There is no CI today. When CI is added (future M5 polish item or
post-v1.0), the canonical order is:

```pwsh
dotnet restore
dotnet build --no-restore
dotnet test --no-build --logger "console;verbosity=normal"
```

CI cannot run smoke tests (no display). The smoke test stays
human-in-the-loop until a headless renderer regression suite exists
per §4.

## 6. Testing conventions

- **Test files mirror service files.** `FooService.cs` →
  `FooServiceTests.cs` next to the others under
  `tests/VybeDesk.Tests/`.
- **Test method names: `MethodName_Condition_ExpectedOutcome`.**
  E.g. `ImportAsync_FolderHasClaudeMd_ProjectDescriptionPopulated`.
- **Use NSubstitute for service dependencies.** Avoid Moq —
  HANDOFF.md §Gotchas + the C# Mastery skill flag SponsorLink issues
  in recent Moq versions.
- **Use real SQLite for store tests.** Open an in-memory database
  (`Data Source=:memory:`) and exercise the full schema. Mocking
  SQLite proves nothing.
- **Don't test third-party libraries.** No Markdig parser tests, no
  Avalonia binding tests — assume they work and test our integration
  with them.

## 7. The escalation rule

If a smoke test fails twice in a row on the same fix, **stop and
write down what you observed before trying a third time.** Capture:

- What the user reported visually.
- What the .axaml change was supposed to produce.
- Why you think it didn't.
- What hypothesis you'd test next.

If the third attempt also fails, **escalate to documentation**:
update [LAYOUT_REGRESSION.md](LAYOUT_REGRESSION.md) (or a new
component-specific regression doc) with the full failed-pattern list,
the working-elsewhere pattern for comparison, and the hypothesis
queue. Then stop and ask the user how to proceed.

The v0.24 Resources saga is the cost of NOT escalating: nine
iterations were burned because each one looked promising in
isolation. The pattern was visible only when laid side-by-side, which
is what documentation forces you to do.
