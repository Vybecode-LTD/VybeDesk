# Cross-Module Project Persistence Bug (RESOLVED 2026-05-28)

> **STATUS: RESOLVED.** The root cause was passive null writes from
> TwoWay ComboBox bindings flowing through `ActiveProjectContext.SetCurrent`.
> Every time a ModuleHeader ComboBox initialized (or the Projects collection
> was cleared/rebuilt), a null was written through the TwoWay chain into
> `SetCurrent(null)`, which broadcast a `Changed` event that cleared every
> other module's selection. The fix was a two-part rewrite of
> `ActiveProjectContext` + per-module project isolation hardening.
>
> **Resolution session:** 2026-05-28 (Codex-audit-driven fix plan).
>
> Companion docs:
> - [LAYOUT_REGRESSION.md](LAYOUT_REGRESSION.md) — the remaining open bug
>   (HomeView/ProjectsView overflow) — separate problem
> - [TESTING.md](TESTING.md) — smoke-test protocol
> - [HANDOFF.md](../HANDOFF.md) — orientation for new sessions

---

## 1. The bug

**Steps to reproduce:**
1. Launch the app.
2. Navigate to **Testing Manager** (or Bug Tracker, Vision Audit, Documentation).
3. Open the "Project:" dropdown in the module header and select any project.
4. Navigate to a different sidebar entry (any module).
5. Navigate back to Testing Manager.

**Expected:** the project selected in step 3 is still shown.  
**Actual:** the picker is empty (reverted).

The same revert happens in every module with a project picker: Bug Tracker,
Vision Audit, and Documentation all exhibit the same behaviour.

---

## 2. Architecture context

### The IActiveProjectContext chain

`IActiveProjectContext` is a cross-cutting singleton service with three
members: `Current`, `SetCurrent(Project?)`, and a `Changed` event.

Every project-scoped module VM:
- Subscribes to `_projects.Changed` → calls `LoadProjectsAsync`
- Subscribes to `_activeProjectContext.Changed` → `Dispatcher.UIThread.Post`
  → syncs `SelectedProject` to whatever the context says
- In `OnSelectedProjectChanged`: calls `_activeProjectContext.SetCurrent(newValue)`
  so other modules sync up

### The ModuleHeader picker binding chain

The project picker lives in `Controls/ModuleHeader.axaml`. Each module view
embeds it like this (Testing Manager shown):

```xml
<ctl:ModuleHeader ShowPicker="True"
                  ProjectsSource="{Binding Projects}"
                  PickerSelectedItem="{Binding SelectedProject, Mode=TwoWay}"
                  StatusMessage="{Binding StatusMessage}"/>
```

Inside ModuleHeader the ComboBox is:

```xml
<ComboBox ItemsSource="{Binding #Root.ProjectsSource}"
          SelectedItem="{Binding #Root.PickerSelectedItem, Mode=TwoWay}"/>
```

So the full TwoWay chain is:

```
ComboBox.SelectedItem
  ↕ TwoWay (#Root.PickerSelectedItem element-name binding)
ModuleHeader.PickerSelectedItem (StyledProperty<object?>)
  ↕ TwoWay (parent view's compiled binding)
VM.SelectedProject ([ObservableProperty] Project?)
```

### ViewLocator behaviour

`ViewLocator.Build(data)` is called by ContentControl every time
`MainWindowViewModel.CurrentPage` changes. Before fix 3 (see below),
it called `Activator.CreateInstance(type)!` — a **brand-new view on
every navigation**.

---

## 3. All fixes applied (working tree, uncommitted)

All three fixes are present in the uncommitted working tree. Each was
tested by the user after application and confirmed still broken.

### Fix 1 — `_reloadingProjects` flag in four module VMs

**Files changed:**
- `src/VybeDesk.App/ViewModels/BugTrackerViewModel.cs`
- `src/VybeDesk.App/ViewModels/TestingManagerViewModel.cs`
- `src/VybeDesk.App/ViewModels/VisionAuditViewModel.cs`
- `src/VybeDesk.App/ViewModels/DocumentationViewModel.cs`

**What changed in each VM:**

```csharp
private bool _reloadingProjects;  // ← NEW field

private async Task LoadProjectsAsync()
{
    var all = await _projects.GetAllAsync();
    // keepId captured POST-await: uses SelectedProject first, falls back to
    // _activeProjectContext.Current in case another module's selection changed
    // while GetAllAsync was in flight.
    var keepId = SelectedProject?.Id ?? _activeProjectContext.Current?.Id;
    _reloadingProjects = true;
    Projects.Clear();
    foreach (var p in all) Projects.Add(p);
    _reloadingProjects = false;
    SelectedProject = keepId is not null
        ? Projects.FirstOrDefault(p => p.Id == keepId) ?? Projects.FirstOrDefault()
        : Projects.FirstOrDefault();
}

partial void OnSelectedProjectChanged(Project? oldValue, Project? newValue)
{
    if (_reloadingProjects) return;  // ← NEW guard
    if (newValue?.Id != _activeProjectContext.Current?.Id)
        _activeProjectContext.SetCurrent(newValue);
    if (oldValue?.Id == newValue?.Id) return;
    // ... rest of method unchanged
}

private void OnActiveProjectContextChanged()
{
    Dispatcher.UIThread.Post(() =>
    {
        var target = _activeProjectContext.Current;
        if (SelectedProject?.Id == target?.Id) return;
        if (target is null) { SelectedProject = null; }
        else
        {
            var found = Projects.FirstOrDefault(p => p.Id == target.Id);
            if (found is null) return;  // ← NEW guard: not in list yet, skip
            SelectedProject = found;
        }
    });
}
```

**Why we thought it would work:** when `LoadProjectsAsync` runs and clears the
`Projects` collection, the ComboBox TwoWay binding fires null back to
`SelectedProject`. The flag suppresses the handler during that window and the
explicit `SelectedProject = keepId match` at the end restores the selection.

**Why it didn't work:** user confirmed still broken. The _reloadingProjects
flag guards the `Projects.Clear()` path but does not guard the view
re-creation null-pulse (see Fix 3 hypothesis) or any other path.

---

### Fix 2 — Remove `SetCurrent(null)` from NotebookViewModel's AllProjects branch

**File changed:** `src/VybeDesk.App/ViewModels/NotebookViewModel.cs`

**What changed:** In `OnActiveProjectChanged`, the `IsAllProjects` branch
previously called `_activeProjectContext.SetCurrent(null)`. This was removed.

```csharp
partial void OnActiveProjectChanged(Project? oldValue, Project? newValue)
{
    SaveConversation(oldValue);
    RestoreConversation(newValue);

    if (IsAllProjects(newValue))
    {
        // ... scoped roots and agent setup unchanged ...
        // SetCurrent(null) removed — "All Projects" is Notebook-internal;
        // it must NOT broadcast null to all other modules.
    }
    else
    {
        // ... unchanged; still calls SetCurrent(newValue) for real projects
    }
    RebuildScopeRows();
}
```

**Why we thought it would work:** at startup, NotebookViewModel selects
its "All Projects" sentinel as `ActiveProject`, which fires
`OnActiveProjectChanged` → `SetCurrent(null)` → every other module's
`OnActiveProjectContextChanged` fires → clears their `SelectedProject`.
Removing the null broadcast should preserve other modules' selections.

**Why it didn't work:** user confirmed still broken. The startup null blast
was real and is now fixed, but it was not the only cause. Something else
continues to clear the selection.

---

### Fix 3 — ViewLocator caches one view per VM instance

**File changed:** `src/VybeDesk.App/ViewLocator.cs`

**What changed:**

```csharp
public sealed class ViewLocator : IDataTemplate
{
    private readonly ConditionalWeakTable<object, Control> _cache = new();

    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "null" };
        var name = data.GetType().FullName!.Replace("ViewModel", "View", ...);
        var type = Type.GetType(name);
        if (type is null) return new TextBlock { Text = "View not found: " + name };
        return _cache.GetValue(data, _ => (Control)Activator.CreateInstance(type)!);
    }
    public bool Match(object? data) => data is ObservableObject;
}
```

**Why we thought it would work:** the original ViewLocator called
`Activator.CreateInstance(type)!` on every navigation. A newly-constructed
`ModuleHeader` ComboBox initializes with `SelectedItem = null` (its default).
Before the parent view's `PickerSelectedItem="{Binding SelectedProject}"` can
supply the real value, the internal TwoWay element-name binding
`{Binding #Root.PickerSelectedItem, Mode=TwoWay}` fires null back through the
chain, clearing `SelectedProject` in the VM. Caching the view prevents this
initialization cycle from ever repeating.

**Why it didn't work:** user confirmed still broken. Either:
(a) the null pulse from view-recreation was not the actual cause (or not the
    only cause), OR
(b) ViewLocator caching itself has a subtle issue in Avalonia
    (see hypotheses below).

---

## 4. Current state of modified files

```
src/VybeDesk.App/ViewLocator.cs                      ← Fix 3
src/VybeDesk.App/ViewModels/BugTrackerViewModel.cs   ← Fix 1
src/VybeDesk.App/ViewModels/TestingManagerViewModel.cs  ← Fix 1
src/VybeDesk.App/ViewModels/VisionAuditViewModel.cs  ← Fix 1
src/VybeDesk.App/ViewModels/DocumentationViewModel.cs   ← Fix 1
src/VybeDesk.App/ViewModels/NotebookViewModel.cs     ← Fix 2
```

Build: ✓ 0 warnings, 0 errors  
Tests: ✓ 161/161

---

## 5. Hypotheses for next session (try in order)

### H1 — Instrument first, fix second (MANDATORY)

Before trying a fourth fix, add `Console.Error.WriteLine` calls to observe
what is ACTUALLY clearing the selection at runtime. Suggested instrumentation:

```csharp
// In TestingManagerViewModel (or any one VM):
partial void OnSelectedProjectChanged(Project? oldValue, Project? newValue)
{
    Console.Error.WriteLine(
        $"[TM] SelectedProject: {oldValue?.Name} → {newValue?.Name} | " +
        $"_reloadingProjects={_reloadingProjects} | " +
        $"stack={new System.Diagnostics.StackTrace(true).ToString().Split('\n')[1].Trim()}");
    // ... existing code
}

private void OnActiveProjectContextChanged()
{
    Dispatcher.UIThread.Post(() =>
    {
        Console.Error.WriteLine(
            $"[TM] OnActiveProjectContextChanged: context={_activeProjectContext.Current?.Name}");
        // ... existing code
    });
}
```

And in ViewLocator.Build:
```csharp
Console.Error.WriteLine($"[ViewLocator] Build called for {data?.GetType().Name}");
```

Run `dotnet run --project src/VybeDesk.App` in a console window (not
backgrounded), pick a project, navigate away and back, and read the output.
The first null-valued `[TM] SelectedProject` line will show the stack trace of
the caller.

### H2 — Avalonia ContentControl may not call ViewLocator.Build at all after caching

Verify the cache is actually being hit by the log above. If
`[ViewLocator] Build called for TestingManagerViewModel` appears EVERY
navigation (not just the first), the ConditionalWeakTable is not preventing
re-creation. This would indicate Avalonia's ContentControl/ContentPresenter
is NOT calling Build every time — it has its own template caching — and our
Fix 3 was solving a non-problem.

If caching is NOT the issue, the null pulse from view-recreation can be
ruled out entirely and we need a different explanation.

### H3 — CommunityToolkit.Mvvm `SetProperty` sets the backing field BEFORE the partial method

When `_reloadingProjects = true` and the ComboBox fires a null TwoWay
write, CommunityToolkit's `SetProperty` sets `_selectedProject = null` in
the backing field and THEN calls `OnSelectedProjectChanged`, which returns
early. So after `Projects.Clear()`, the backing field IS null, even though
`OnSelectedProjectChanged` was suppressed. The subsequent
`SelectedProject = keepId match` corrects it — but if that assignment
races with another async path or if `keepId` is unexpectedly null, the field
stays null. 

**To test:** add a log line just before the final `SelectedProject = ...`
assignment in `LoadProjectsAsync` that prints the value of `keepId` and the
result. If keepId is ever null, that's the problem.

### H4 — Another VM (Notebook or other) is still calling SetCurrent unexpectedly

Grep for ALL calls to `SetCurrent` and `_activeProjectContext.SetCurrent`:

```pwsh
Select-String -Path "src\VybeDesk.App\ViewModels\*.cs" -Pattern "SetCurrent"
```

Verify each call site. The only CORRECT callers are:
- Each project-scoped VM's `OnSelectedProjectChanged` (when newValue?.Id ≠ Current?.Id)
- (Notebook's real-project path in the `else` branch)

Any unexpected caller (e.g. in a constructor, in a Projects collection change
handler, in a settings handler) could broadcast null at the wrong moment.

### H5 — IActiveProjectContext implementation has a bug

Read `src/VybeDesk.Services/` or `src/VybeDesk.App/` for the implementation
of `IActiveProjectContext`. Verify:
- `SetCurrent` fires `Changed` synchronously
- `Changed` uses a simple event (not an async relay or dispatcher-posted relay)
- No thread-safety issue if called rapidly from Dispatcher.UIThread

### H6 — The binding mode on PickerSelectedItem needs OneWayToSource for the view→VM direction

Rather than TwoWay (which allows null to propagate FROM the ComboBox
BACK to the VM), change the parent view's binding to
`Mode=OneWayToSource`:

```xml
PickerSelectedItem="{Binding SelectedProject, Mode=OneWayToSource}"
```

And separately bind display sync as `OneWay` via a trigger or explicit
`OnSelectedProjectChanged` → `PickerSelectedItem = value` assignment.

This surgically prevents ANY null from the ComboBox ever reaching the VM.
The trade-off: the display won't update automatically when `SelectedProject`
changes from code (e.g. from `OnActiveProjectContextChanged`). You'd need to
wire that direction manually.

This is the nuclear option if all other hypotheses fail.

---

## 6. What to NOT do

- **Do not add a fourth XAML pattern or binding approach without the
  instrumentation pass (H1) first.** The saga up to this point burned three
  sessions of effort on hypothesis-driven code changes. Each change was
  "obvious" and "correct" in isolation — and each failed.
- **Do not commit any of the persistence-bug files until the bug is
  actually fixed and smoke-tested.** They are isolated from the v0.32 M3/M4/M5
  work; commit the working code first, then the persistence fix separately.

---

## 7. Related open bug

[LAYOUT_REGRESSION.md](LAYOUT_REGRESSION.md) — HomeView and ProjectsView
overflow the viewport. A separate, independent bug. Do not mix the two.

---

## 8. Resolution template

When the bug is fixed: update this file's STATUS line, describe the actual
root cause found (which hypothesis), what code was changed, and what the
final smoke-test verification looked like. Then archive it to the "Closed"
section in CHANGELOG.md.
