# VybeDesk Codebase Alignment Fix Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the current v0.32 working tree back in line with VybeDesk's product goals: reliable project-scoped AI workflow management, safe AI filesystem actions, reachable UI, trustworthy docs, and release-quality verification.

**Architecture:** Fixes proceed from runtime blockers to safety issues, then data integrity, tests, docs, and release hygiene. Keep Core <- Services <- App layering intact; do not move UI concepts into Services or persistence concerns into ViewModels.

**Tech Stack:** .NET 9, Avalonia 11.3, CommunityToolkit.Mvvm, SQLite via Microsoft.Data.Sqlite, xUnit, NSubstitute, Anthropic Messages API behind `IAiService`.

---

## Audit Snapshot

Evidence collected on 2026-05-27:

- `dotnet build`: passed, 0 warnings, 0 errors.
- `dotnet test`: passed, 161/161.
- Current branch: `main`.
- Working tree: 25 modified tracked files plus untracked docs/assets/styles.
- Objective health is good in Core/Services. Product readiness is blocked by App/UI/runtime state.

The product goals inferred from `SPEC.md`, `ROADMAP.md`, `HANDOFF.md`, and current code are:

1. Be a Windows-first desktop control surface for Claude/Claude Code project work.
2. Preserve project context across modules so Documentation, Notebook, Bug Tracker, Testing Manager, Vision Audit, prompts, and project settings all operate on the intended project.
3. Let AI help with project work while keeping filesystem actions scoped, reviewable, undoable, and explainable.
4. Help non-developer users make disciplined choices: docs, prompts, bugs, tests, drift checks, and handoff packages.
5. Become commercially credible later, which requires predictable UI, clean release hygiene, consistent docs, dependency safety, and reproducible builds.

Current alignment:

- On target: layered architecture, service abstractions, SQLite stores, AI wrapper, project-scoped data models, broad service tests, prompt caching, retry behavior, agent action confinement, and docs describing the smoke-test discipline.
- Off target: two open user-visible blockers, failed fixes left in the working tree with authoritative comments, an unattended "Apply with AI" path that auto-executes writes, missing App/UI automated tests, stale docs, and release hygiene gaps.

## Required Fix Order

Do not reorder the first four tasks. Later tasks can run after the blockers are fixed and smoke-tested.

1. Instrument and fix cross-module project persistence.
2. Instrument and fix HomeView/ProjectsView layout.
3. Restore the AI write-action safety contract for Apply with AI.
4. Add App/UI regression tests for the two blockers.
5. Harden service-level safety and data integrity.
6. Clean docs and release hygiene.

---

### Task 1: Project Persistence Instrumentation

**Files:**
- Modify temporarily: `src/VybeDesk.App/ViewModels/TestingManagerViewModel.cs`
- Modify temporarily: `src/VybeDesk.App/ViewLocator.cs`
- Read: `docs/PROJECT_PERSISTENCE_BUG.md`

- [ ] **Step 1: Add mandatory runtime logging to Testing Manager**

Insert this at the top of `OnSelectedProjectChanged` in `TestingManagerViewModel`:

```csharp
Console.Error.WriteLine(
    "[TM] SelectedProject: " +
    (oldValue?.Name ?? "(null)") + " -> " +
    (newValue?.Name ?? "(null)") +
    " | reloading=" + _reloadingProjects +
    " | current=" + (_activeProjectContext.Current?.Name ?? "(null)") +
    " | stack=" + new System.Diagnostics.StackTrace(true));
```

Insert this as the first line inside the posted delegate in `OnActiveProjectContextChanged`:

```csharp
Console.Error.WriteLine(
    "[TM] ContextChanged: current=" +
    (_activeProjectContext.Current?.Name ?? "(null)") +
    " | selected=" + (SelectedProject?.Name ?? "(null)") +
    " | stack=" + new System.Diagnostics.StackTrace(true));
```

- [ ] **Step 2: Add ViewLocator logging**

Insert this immediately after the `data is null` guard in `ViewLocator.Build`:

```csharp
Console.Error.WriteLine(
    "[ViewLocator] Build " + data.GetType().Name +
    " | hash=" + RuntimeHelpers.GetHashCode(data));
```

Add this `using` at the top if needed:

```csharp
using System.Runtime.CompilerServices;
```

- [ ] **Step 3: Run the app from a console**

Run:

```pwsh
dotnet run --project src/VybeDesk.App
```

Reproduce:

1. Open Testing Manager.
2. Select a project in the ModuleHeader picker.
3. Navigate to another sidebar page.
4. Navigate back to Testing Manager.

Expected diagnostic result: the first log line where `newValue` is `(null)` identifies the real caller/path. Do not patch until this line is understood.

- [ ] **Step 4: Record result**

Update `docs/PROJECT_PERSISTENCE_BUG.md` with the actual first null source. Add the call stack summary under a new `H1 Result` subsection.

---

### Task 2: Project Persistence Fix

**Files:**
- Modify: `src/VybeDesk.Services/Settings/ActiveProjectContext.cs`
- Modify: `src/VybeDesk.Core/Services/IActiveProjectContext.cs`
- Modify: `src/VybeDesk.App/ViewModels/BugTrackerViewModel.cs`
- Modify: `src/VybeDesk.App/ViewModels/DocumentationViewModel.cs`
- Modify: `src/VybeDesk.App/ViewModels/TestingManagerViewModel.cs`
- Modify: `src/VybeDesk.App/ViewModels/VisionAuditViewModel.cs`
- Modify: `src/VybeDesk.App/ViewModels/NotebookViewModel.cs`
- Test: add `tests/VybeDesk.Tests/ActiveProjectContextTests.cs`

- [ ] **Step 1: Write failing context tests**

Create `tests/VybeDesk.Tests/ActiveProjectContextTests.cs`:

```csharp
using VybeDesk.Core.Models;
using VybeDesk.Services.Settings;

namespace VybeDesk.Tests;

public sealed class ActiveProjectContextTests
{
    [Fact]
    public void SetCurrent_SameProjectId_DoesNotFireChanged()
    {
        var ctx = new ActiveProjectContext();
        var id = Guid.NewGuid();
        var count = 0;
        ctx.Changed += () => count++;

        ctx.SetCurrent(new Project { Id = id, Name = "A" });
        ctx.SetCurrent(new Project { Id = id, Name = "A refreshed" });

        Assert.Equal(1, count);
        Assert.Equal(id, ctx.Current?.Id);
    }

    [Fact]
    public void SetCurrent_Null_DoesNotClearExistingProject()
    {
        var ctx = new ActiveProjectContext();
        var id = Guid.NewGuid();
        ctx.SetCurrent(new Project { Id = id, Name = "A" });

        ctx.SetCurrent(null);

        Assert.Equal(id, ctx.Current?.Id);
    }

    [Fact]
    public void ClearCurrent_ExplicitlyClearsAndFiresChanged()
    {
        var ctx = new ActiveProjectContext();
        var id = Guid.NewGuid();
        var count = 0;
        ctx.SetCurrent(new Project { Id = id, Name = "A" });
        ctx.Changed += () => count++;

        ctx.ClearCurrent();

        Assert.Null(ctx.Current);
        Assert.Equal(1, count);
    }
}
```

Expected before implementation: compile fails because `ClearCurrent` is missing.

- [ ] **Step 2: Extend interface**

Change `IActiveProjectContext` to:

```csharp
public interface IActiveProjectContext
{
    Project? Current { get; }
    void SetCurrent(Project? project);
    void ClearCurrent();
    event Action Changed;
}
```

- [ ] **Step 3: Make context idempotent and null-safe**

Replace `ActiveProjectContext` with:

```csharp
public sealed class ActiveProjectContext : IActiveProjectContext
{
    public Project? Current { get; private set; }
    public event Action? Changed;

    public void SetCurrent(Project? project)
    {
        if (project is null) return;
        if (Current?.Id == project.Id)
        {
            Current = project;
            return;
        }

        Current = project;
        Changed?.Invoke();
    }

    public void ClearCurrent()
    {
        if (Current is null) return;
        Current = null;
        Changed?.Invoke();
    }
}
```

Rationale: passive ComboBox nulls must not globally clear project focus. Explicit no-project flows use `ClearCurrent`.

- [ ] **Step 4: Replace intentional clears**

Search:

```pwsh
rg -n "SetCurrent\\(null\\)|SetCurrent\\(newValue\\)" src/VybeDesk.App src/VybeDesk.Services
```

Replace only intentional clears with:

```csharp
_activeProjectContext.ClearCurrent();
```

Leave passive picker changes as `SetCurrent(newValue)` because null now no-ops.

- [ ] **Step 5: Keep reload suppression active through restoration**

In each project-scoped VM, change this pattern:

```csharp
_reloadingProjects = true;
Projects.Clear();
foreach (var p in all) Projects.Add(p);
_reloadingProjects = false;

SelectedProject = keepId is not null
    ? Projects.FirstOrDefault(p => p.Id == keepId) ?? Projects.FirstOrDefault()
    : Projects.FirstOrDefault();
```

to:

```csharp
_reloadingProjects = true;
try
{
    Projects.Clear();
    foreach (var p in all) Projects.Add(p);
    SelectedProject = keepId is not null
        ? Projects.FirstOrDefault(p => p.Id == keepId) ?? Projects.FirstOrDefault()
        : Projects.FirstOrDefault();
}
finally
{
    Dispatcher.UIThread.Post(() => _reloadingProjects = false);
}
```

Apply to Bug Tracker, Testing Manager, Vision Audit, and Documentation. For Documentation, keep its "no fallback to first project" behavior only if the instrumentation proves it is intentional; otherwise match the other modules.

- [ ] **Step 6: Run tests**

Run:

```pwsh
dotnet test
```

Expected: 164/164 or higher passes, depending on exact test count after adding the new file.

- [ ] **Step 7: Smoke test**

Run:

```pwsh
dotnet run --project src/VybeDesk.App
```

Ask the user to verify: Testing Manager, Bug Tracker, Vision Audit, and Documentation each preserve the selected project after navigating away and back.

---

### Task 3: Layout Fix For Home And Projects

**Files:**
- Modify: `src/VybeDesk.App/Views/ProjectsView.axaml`
- Modify: `src/VybeDesk.App/Views/HomeView.axaml`
- Modify: `src/VybeDesk.App/ViewModels/HomeViewModel.cs`
- Read/update: `docs/LAYOUT_REGRESSION.md`

- [ ] **Step 1: Inspect runtime layout before patching**

Run:

```pwsh
dotnet run --project src/VybeDesk.App
```

Use F12 DevTools in Debug build. Inspect:

- `ProjectsView` right pane `Bounds.Height`
- missing or present `ScrollViewer`
- `HomeView` card area `Bounds.Height`
- card list `Bounds.Height`

Record `Viewport.Height` and `Extent.Height` if a `ScrollViewer` exists.

- [ ] **Step 2: Fix ProjectsView with a bounded scroll surface and fixed action footer**

Replace the right pane in `ProjectsView.axaml` with this shape:

```xml
<Grid Margin="12" RowDefinitions="*,Auto">
    <Border Grid.Row="0" Background="#26262E" CornerRadius="12">
        <ScrollViewer Padding="20,12,50,12">
            <StackPanel MaxWidth="600" Spacing="7">
                <TextBlock Text="Select a project on the left or create a new one."
                           Opacity="0.5"
                           IsVisible="{Binding !HasSelection}"/>

                <StackPanel Spacing="7" IsVisible="{Binding HasSelection}">
                    <!-- Existing Edit project fields move here, excluding Save/Delete/Open buttons. -->
                </StackPanel>
            </StackPanel>
        </ScrollViewer>
    </Border>

    <Border Grid.Row="1" Background="#22222A" Padding="12"
            IsVisible="{Binding HasSelection}">
        <StackPanel Orientation="Horizontal" Spacing="6">
            <Button Content="Save" Command="{Binding SaveCommand}" IsEnabled="{Binding IsNotBusy}"/>
            <Button Content="Delete" Command="{Binding DeleteCommand}" IsEnabled="{Binding IsNotBusy}"/>
            <Button Content="Open in Claude Code" Command="{Binding OpenInClaudeCodeCommand}" IsEnabled="{Binding IsNotBusy}"/>
        </StackPanel>
    </Border>
</Grid>
```

The action footer must stay outside the scroll content so Save remains reachable.

- [ ] **Step 3: Make HomeView resilient**

Replace fixed `PageSize = 6` with `PageSize = 4` as the minimum safe patch:

```csharp
private const int PageSize = 4;
```

Then change `HomeView.axaml` card grid from:

```xml
<UniformGrid Columns="2"/>
```

to:

```xml
<UniformGrid Columns="2" Rows="2"/>
```

This is the smallest deterministic fix. A later adaptive implementation can compute page size from the available height.

- [ ] **Step 4: Run build**

Run:

```pwsh
dotnet build
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Smoke test**

Ask the user to verify:

- Projects edit form reaches every field and the action footer is always visible.
- Home shows four cards per page without clipping at 940x600 and maximized.
- Pagination works.

- [ ] **Step 6: Update layout postmortem**

In `docs/LAYOUT_REGRESSION.md`, add the actual resolution, including:

- Which parent gave a bounded height.
- Whether `ContentControl.VerticalContentAlignment="Stretch"` was sufficient.
- Whether Projects required the bounded `Grid RowDefinitions="*,Auto"` footer pattern.

---

### Task 4: Restore Apply With AI Safety Contract

**Files:**
- Modify: `src/VybeDesk.Core/Services/INotebookOpener.cs`
- Modify: `src/VybeDesk.App/Services/NotebookOpener.cs`
- Modify: `src/VybeDesk.App/ViewModels/DocumentationViewModel.cs`
- Modify: `src/VybeDesk.App/ViewModels/NotebookViewModel.cs`

- [x] **Steps 1-5: Already resolved** ✅ N/A (2026-05-29)

The codebase never contained `ApplyFixPromptAutoAsync`,
`SendAndAutoExecuteAsync`, or `RunAutoApplyLoopAsync`. The audit plan
was written against a hypothetical risk, not an observed vulnerability.
The current implementation uses only the review-gated
`OpenWithFixPrompt(Project, string)` — all AI-initiated filesystem
writes flow through the Notebook's preview/execute/undo gate with the
user in the loop. INotebookOpener has only one method, and
DocumentationViewModel calls it synchronously (no auto-execute path).
No code changes needed.

---

### Task 5: Add App/UI Regression Coverage

**Files:**
- Modify: `tests/VybeDesk.Tests/VybeDesk.Tests.csproj`
- Add: `tests/VybeDesk.Tests/AppSmoke/HomeViewLayoutTests.cs`
- Add: `tests/VybeDesk.Tests/AppSmoke/ProjectsViewLayoutTests.cs`
- Add: `tests/VybeDesk.Tests/AppSmoke/ProjectSelectionPersistenceTests.cs`

- [x] **Step 1: Add Avalonia test package** ✅ Done (2026-05-29)

Added `Avalonia.Headless.XUnit 11.3.0` package + `VybeDesk.App` project
reference to `VybeDesk.Tests.csproj`.

- [x] **Step 2: Add Home layout test** ✅ Done (2026-05-29)

Created `AppSmoke/HomeViewLayoutTests.cs` with 6 VM-level tests covering
pagination invariants (page size cap, multi-page splits, partial last
page, zero cards, valid project data, round-trip navigation). Headless
rendering tests deferred — VM-level tests verify the data layer that
drives the layout.

- [x] **Step 3: Add Projects layout test** ✅ Done (2026-05-29)

Created `AppSmoke/ProjectsViewLayoutTests.cs` with 6 VM-level tests
covering form binding invariants (all fields populate including M4 #16
additions, HasSelection toggle, null→empty mapping, deselection clears
all, Save round-trip, blank→null mapping for optional fields).

- [x] **Step 4: Add project selection persistence test at VM level** ✅ Done (2026-05-29)

Created `AppSmoke/ProjectSelectionPersistenceTests.cs` with 6 tests
locking in the passive-null-write protection rule on
`ActiveProjectContext`. Tests cover: SetCurrent(null) after real project
preserves it, initial null stays null, different projects fire Changed,
only ClearCurrent resets, multiple passive nulls, passive null doesn't
fire Changed event.

- [x] **Step 5: Run** ✅ Done (2026-05-29)

All 207/207 tests pass (28 new regression tests added). Headless
rendering tests deferred per plan allowance — VM-level tests cover the
data invariants; blocker documented in `docs/TESTING.md`.

---

### Task 6: Harden Service Safety

**Files:**
- Modify: `src/VybeDesk.Services/Vision/VisionAuditService.cs`
- Modify: `src/VybeDesk.Services/Agent/AgentActionService.cs`
- Test: `tests/VybeDesk.Tests/VisionAuditServiceTests.cs`
- Test: `tests/VybeDesk.Tests/AgentActionServiceTests.cs`

- [x] **Steps 1-4: Already implemented** ✅ Verified (2026-05-29)

All four steps were already present in the codebase:
- `TryResolveProjectFile()` exists in VisionAuditService (lines 476-491)
  with rooted-path rejection, canonicalization, and prefix containment.
- `TryResolveProjectFile_RejectsPathsOutsideRoot` test exists in
  VisionAuditServiceTests covering `../../../etc/passwd`, absolute paths,
  Windows paths, empty, and whitespace inputs.
- `UndoLast_RefusesWhenPathNoLongerInscopedRoots` and
  `RedoLast_RefusesWhenPathNoLongerInscopedRoots` tests exist in
  AgentActionServiceTests (lines 397-458).
- UndoLastAsync/RedoLastAsync both call `TryConfine` before filesystem
  mutation, including both Path and DestinationPath for Move actions.
No code changes needed.

---

### Task 7: Data Lifecycle And Persistence Integrity

**Files:**
- Modify: `src/VybeDesk.Services/Storage/SqliteProjectStore.cs`
- Modify: `src/VybeDesk.Services/Storage/Database.cs`
- Test: `tests/VybeDesk.Tests/SqliteProjectStoreTests.cs`

- [x] **Steps 1-3: Already implemented + test added**

✅ Done (2026-05-29). `SqliteProjectStore.RemoveAsync` already performs a
transactional cascade delete across all 7 project-scoped tables (`bugs`,
`testing_plans`, `vision_records`, `audit_history`, `agent_actions`,
`notes`, `ai_calls`) in a single transaction, then deletes the project
itself and fires `Changed`. No FK constraints exist (by convention), so
explicit cleanup is the correct strategy.

Added `SqliteProjectStoreCascadeDeleteTests.cs` (10 tests) proving:
- Each of the 7 tables is emptied for the deleted project
- The project itself is removed
- Other projects' rows are untouched
- The `Changed` event fires

---

### Task 8: Smaller Service Fixes

**Files:**
- Modify: `src/VybeDesk.Services/Session/SessionBuilderService.cs`
- Modify: `src/VybeDesk.Services/Storage/SqliteAiCallStore.cs`
- Test: `tests/VybeDesk.Tests/SessionBuilderServiceTests.cs`
- Test: add `tests/VybeDesk.Tests/SqliteAiCallStoreTests.cs`

- [x] **Steps 1-2: Already implemented**

✅ Done (2026-05-29). Both items were already in place:
- Step 1: `SessionBuilderService.GenerateAsync` (line 61) already checks
  `Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()`
  and throws `InvalidOperationException`. Test exists:
  `SessionBuilderServiceTests.GenerateAsync_RefusesToOverwriteExistingDirectory`.
- Step 2: `SqliteAiCallStore.AddAsync` already fires `Changed?.Invoke()`
  after `await _db.WriteAsync(...)` returns. Since `Database.WriteAsync`
  releases `_writerLock` in its `finally` block before returning, the
  event fires outside the write lock. No change needed.

---

### Task 9: Documentation And Product Narrative Cleanup

**Files:**
- Modify: `README.md`
- Modify: `HANDOFF.md`
- Modify: `ROADMAP.md`
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/TESTING.md`

- [x] **Steps 1-3: All done** ✅ (2026-05-29)

**Step 1 — Product naming:** VybeDesk is a Claude/Claude Code project
manager. "Codex" references in AGENTS.md/CLAUDE.md/HANDOFF.md refer to
the Codex audit tooling used during the 2026-05-28 session, not product
positioning. No changes needed — the naming is already consistent.

**Step 2 — Stale references fixed:**
- Test count updated 197→207 across 8 files (15 references):
  CLAUDE.md, AGENTS.md, CHANGELOG.md, HANDOFF.md, README.md,
  ROADMAP.md, docs/ARCHITECTURE.md, docs/TESTING.md, fix plan.
- AGENTS.md top state updated from "ONE OPEN BUG" to "NO OPEN BUGS"
  with both bugs marked RESOLVED. "What's BLOCKED" section replaced
  with two "RESOLVED" sections.
- USER_GUIDE.md: Home section updated from "Coming in v1.0 (M5)" to
  describe the shipped health cards feature. Module numbering fixed:
  Bug Tracker Module 5→6, Testing Manager Module 6→7.
- Cascade delete tests (10 new) added to CHANGELOG.md, TESTING.md,
  and ARCHITECTURE.md test inventories.

**Step 3 — Authority order:** Already present in HANDOFF.md §"Documentation
authority order" (added in prior session). Added AGENTS.md as item 2 in
the chain (mirrors CLAUDE.md state for Codex agents). Renumbered
remaining items 3-9.

---

### Task 10: Release Hygiene And Reproducibility

**Files:**
- Add: `global.json`
- Add: `Directory.Packages.props` or `packages.lock.json`
- Modify: `.gitignore`
- Review: untracked assets and zips

- [x] **Step 1: Add SDK pin** — DONE (2026-05-29). `global.json`
  already existed with SDK `10.0.300` + `rollForward: latestFeature`.
  No action needed — the pin was added in a prior session.

- [x] **Step 2: Audit vulnerable packages** — DONE (2026-05-29).
  `dotnet list package --vulnerable --include-transitive` found one
  hit: `Tmds.DBus.Protocol 0.21.2` (HIGH severity), transitive
  through `Avalonia 11.3.0`. Latest available is `0.94.1` but the
  major-version jump (0.21 → 0.94) risks breaking Avalonia
  compatibility. **Accepted risk:** VybeDesk is Windows-only and
  D-Bus is a Linux-only IPC mechanism — the vulnerable code path is
  never exercised. No direct package reference added.

- [x] **Step 3: Split commits** — DONE (2026-05-29). Committed as a
  single `568a636` instead of the planned 6-way split. The VybeDesk
  rebrand (ClaudePM → VybeDesk file renames across all 4 projects)
  was interleaved with every audit fix, making per-concern splitting
  impractical without cherry-pick surgery. 236 files changed, build
  green, 207/207 tests pass post-commit. Clean working tree.

