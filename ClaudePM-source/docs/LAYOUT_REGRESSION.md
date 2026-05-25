# Layout Regression — HomeView / ProjectsView (open as of 2026-05-25)

> **STATUS: BLOCKED.** The Home dashboard and the Projects edit pane both
> overflow the visible area without producing a usable scrollbar. Four
> distinct layout patterns have been tried this session and rejected by
> visual smoke test. This document captures the bug, every pattern that
> failed, the pattern that DOES work elsewhere in the app, and concrete
> hypotheses for the next session to test.
>
> Companion docs:
> - [TESTING.md](TESTING.md) — the regression-test protocol that should
>   prevent this kind of saga from repeating
> - [memory/bounded-wizard-stages.md](../../../.claude/projects/...) —
>   the established "per-stage bounded Grid" rule for wizards (correctly
>   followed by all 5 wizards but distinct from the bug below)
> - [memory/failed-layout-patterns.md](...) — short-form summary in
>   user memory, loaded into every new session

## 1. The bug

After M5 #17 (Project health cards on Home) and the M4 #16 enhancements
to ProjectsView (added Model override row, Default output path row,
Logo path row), both views overflow vertically:

- **Projects → Edit project** — the form starts with Name, Description,
  Folder, Status, Logo, Model dropdown + custom-ID textbox, Default
  output path, Save / Delete / Open in Claude Code buttons. The form is
  longer than the window's vertical client area. **No scrollbar
  appears.** Content past the last visible field is unreachable —
  including the Save button.
- **Home dashboard** — project health cards render correctly per card,
  but with more than ~3 cards the list extends past the bottom of the
  window. **A scrollbar does appear**, but dragging it only moves the
  content "a few pixels" (the user's description) — the extent the
  scrollbar reports is roughly equal to the viewport, not the true
  content height.

Both symptoms are classic "ScrollViewer received an infinite vertical
measure constraint" failures: when the ScrollViewer doesn't know how
tall its viewport is, it asks the content "how tall do you want to
be?" the StackPanel answers "as tall as my children", and the
ScrollViewer reports `ExtentHeight ≈ ViewportHeight` because it
thinks its viewport IS the full content.

Reference for the Avalonia-side discussion of this measure-pass
hazard:
- [AvaloniaUI/Avalonia#2701](https://github.com/AvaloniaUI/Avalonia/issues/2701)
  — ScrollViewer inside Grid column doesn't bound height
- [AvaloniaUI/Avalonia#3772](https://github.com/AvaloniaUI/Avalonia/issues/3772)
  — DockPanel + ScrollViewer fill-child height propagation

## 2. (A) Design patterns that did NOT work

All four were tried and rejected by visual smoke test in the same
session (the running process was killed and rebuilt for each attempt;
the user verified each one with a fresh window).

### Plan A — "bounded Grid pattern" (per memory/bounded-wizard-stages.md)

**Shape:**

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top"/>
    <Grid RowDefinitions="*,Auto" Margin="…">       <!-- * = scroll area, Auto = footer -->
        <ScrollViewer Grid.Row="0">
            <StackPanel>… form fields …</StackPanel>
        </ScrollViewer>
        <StackPanel Grid.Row="1" Orientation="Horizontal">
            <Button .../>  <!-- Save / Delete / etc., always reachable -->
        </StackPanel>
    </Grid>
</DockPanel>
```

This is the canonical pattern from the wizard work. It worked for
every wizard (Skill Builder, Testing Manager, Vision Audit). It did
NOT work here. The user reported "going off the page but this time
there's not even a scroll bar on the projects page".

### Plan B — explicit `RowDefinitions="*"` at MainWindow + nested explicit Grids

**Hypothesis:** Avalonia's *implicit* row default doesn't always
propagate a bounded height to descendants when the chain includes
both a DockPanel and a nested ContentControl. Adding `RowDefinitions="*"`
to MainWindow's outer Grid and rewriting both views as explicit
`Grid RowDefinitions="Auto,*,Auto"` (header / fill / footer) was meant
to leave no ambiguity in the measure chain.

**Result:** the user reported "no change at all". The explicit
RowDefinitions="*" on MainWindow is still in place in `MainWindow.axaml`
(line 23) — it makes the intent explicit and is harmless, so we kept
it. Everything else from Plan B was reverted.

### Plan C — remove the global `ScrollContentPresenter` Margin style

**Hypothesis:** App.axaml's app-wide style sets `Margin="0,0,50,0"` on
every `ScrollContentPresenter#PART_ContentPresenter` (to produce the
50px content-to-scrollbar gap). Maybe that style was somehow telling
the ScrollViewer's measure pass that the content area was wider than
it actually is, leaking into vertical measure too.

**Result:** "same exact thing". The style was put back. It IS needed
for the 50px content-to-scrollbar gap convention; removing it didn't
help and removed the gap everywhere else.

### Plan D — revert to the "simple working" pattern

**Shape:**

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top"/>
    <Grid ColumnDefinitions="340,*">             <!-- Projects view -->
        <Border Grid.Column="0">… left rail …</Border>
        <ScrollViewer Grid.Column="1">
            <StackPanel MaxWidth="640" HorizontalAlignment="Left">
                … form fields …
                <StackPanel Orientation="Horizontal">
                    … Save / Delete / Open buttons …    <!-- inside scrolling content -->
                </StackPanel>
            </StackPanel>
        </ScrollViewer>
    </Grid>
</DockPanel>
```

**Why we tried it:** the original Projects edit pane at commit
[`942d864`](#3-c-original-working-projectsview-commit-942d864) used
this shape and was known to work. The user's explicit instruction was
"revert the changes back to when the projects editing was working
fine and take the pattern that you used to build the project editing
section and apply it to the home page cards the same way."

**Result:** "no better." The pattern that worked in v0.7 does NOT work
in v0.32. Something else changed between those versions that breaks
the same XAML shape — see hypothesis section.

## 3. (B) The DocumentationView pattern that DOES work

[DocumentationView.axaml](../src/ClaudePM.App/Views/DocumentationView.axaml)
scrolls correctly in all three of its content states (findings panel,
audit overlay, inline editor). The pattern is more nested than what
ProjectsView is currently doing:

```xml
<DockPanel LastChildFill="True">

    <!-- 1) Header docked top (105px fixed-height ModuleHeader). -->
    <ctl:ModuleHeader DockPanel.Dock="Top" …/>

    <!-- 2) Left rail docked LEFT of the DockPanel (320px wide). -->
    <Border DockPanel.Dock="Left" Width="320" Background="#22222A" Padding="14">
        <DockPanel>
            <StackPanel DockPanel.Dock="Top">… folder/scan controls …</StackPanel>
            <ListBox … />   <!-- fill child of inner DockPanel -->
        </DockPanel>
    </Border>

    <!-- 3) Fill child of the OUTER DockPanel: a Grid with Auto/* rows. -->
    <Grid Margin="20" RowDefinitions="Auto,*" RowSpacing="12">

        <!-- Row 0 (Auto): action toolbar — chips + buttons. -->
        <StackPanel Grid.Row="0" Orientation="Horizontal">
            … severity chips … action buttons …
        </StackPanel>

        <!-- Row 1 (*): the workspace, a SINGLE-CELL Grid overlaying 3 states. -->
        <Grid Grid.Row="1">
            <!-- State 1: findings (default). -->
            <Grid IsVisible="…" RowDefinitions="*,Auto,Auto" RowSpacing="12">
                <Border Grid.Row="0">… findings list …</Border>      <!-- bounded * -->
                <Border Grid.Row="1" MaxHeight="220">… AI result …</Border>
                <Border Grid.Row="2">… fix prompt …</Border>
            </Grid>
            <!-- State 2: audit overlay (overlays state 1 when open). -->
            <DockPanel IsVisible="…">
                <StackPanel DockPanel.Dock="Top">… audit header … </StackPanel>
                <ScrollViewer>                                      <!-- inside DockPanel fill -->
                    <StackPanel MaxWidth="900">… long audit body …</StackPanel>
                </ScrollViewer>
            </DockPanel>
            <!-- State 3: inline editor. -->
            <DockPanel IsVisible="…">…</DockPanel>
        </Grid>
    </Grid>
</DockPanel>
```

**Key differences vs the ProjectsView/HomeView shape that's broken:**

| Property | DocumentationView (works) | ProjectsView Plan D (broken) |
|---|---|---|
| Outer container | `DockPanel LastChildFill="True"` | `DockPanel LastChildFill="True"` |
| Header position | `DockPanel.Dock="Top"` | `DockPanel.Dock="Top"` |
| Left rail position | `DockPanel.Dock="Left"` **on DockPanel itself** | `Grid.Column="0"` **inside a child Grid** |
| Fill container | A `Grid RowDefinitions="Auto,*"` as the DockPanel's fill child | A `Grid ColumnDefinitions="340,*"` as the DockPanel's fill child |
| ScrollViewer parent | Inside the `*` row of an `Auto,*` Grid | Inside the `*` column of a `340,*` Grid |

**The structural difference that may matter:**
- DocumentationView's left rail is **docked to the outer DockPanel**.
  The DockPanel handles the Top + Left children, then gives the FILL
  child only the remaining rectangle (window − header − rail).
- ProjectsView's left rail is **a column of a Grid that's itself the
  fill child** of the DockPanel. The Grid receives a bounded rectangle
  from DockPanel and subdivides it. The ScrollViewer is in column 1
  of that Grid.

Both *should* propagate bounded height equally. But DocumentationView
also wraps the ScrollViewer in another `Auto,*` Grid before it gets to
the ScrollViewer itself, providing one more layer of explicit bounded
`*` row. ProjectsView's ScrollViewer is a direct child of the column.

**This is the most plausible difference to investigate next.**

## 4. (C) Original working ProjectsView — commit `942d864`

Both `git log -- src/ClaudePM.App/Views/ProjectsView.axaml` and
`git log --all -- src/ClaudePM.App/Views/ProjectsView.axaml` show only
two historical commits to that file:

1. `3ec12f3` — initial Projects tab addition (Notebook + Projects landed
   together).
2. `942d864` — added the "Open in Claude Code" button.

Commit `942d864` is the **last version that the user remembers
working correctly**. The XAML shape at that commit:

```xml
<UserControl …>
    <Grid ColumnDefinitions="340,*">             <!-- Grid IS the root -->
        <Border Grid.Column="0">… left rail with list …</Border>
        <ScrollViewer Grid.Column="1" Padding="28">
            <StackPanel Spacing="16" MaxWidth="640" HorizontalAlignment="Left">
                <TextBlock Text="{Binding StatusMessage}" Foreground="#9ABEE0"/>
                … 4 form sections: Name / Description / Folder / Status …
                <StackPanel Orientation="Horizontal">
                    <Button Content="Save"   …/>
                    <Button Content="Delete" …/>
                    <Button Content="Open in Claude Code" …/>
                </StackPanel>
            </StackPanel>
        </ScrollViewer>
    </Grid>
</UserControl>
```

**Critical observations:**

1. **No outer DockPanel.** The Grid is the root element of the
   UserControl. The UserControl receives bounded height directly from
   its parent (the MainWindow's Column 1 ContentControl), and passes
   that bounded height to the Grid, which subdivides into two columns
   and gives `*` column 1 to the ScrollViewer.
2. **No ModuleHeader.** The header was added in v0.31. There is no
   "header strip" eating vertical space; the StatusMessage was an
   in-content `TextBlock` at the top of the form.
3. **Fewer form fields.** Only Name, Description, Folder, Status —
   the Logo path, Model dropdown, Custom model ID textbox, and
   Default output path rows were all added later (M4 #16, M5 #17
   enhancement). The form was always shorter than the window. We
   genuinely don't know whether `942d864`'s pattern would have
   continued to work if the form had grown past the viewport — it
   never had to scroll in v0.7.

**What this means:** "revert to the pattern that worked" is **not
strictly testable** because we never proved that pattern scrolled. We
only proved it *fit* in v0.7. In v0.32 it neither scrolls nor fits.

## 5. What we ruled out + what's still on the table

### Ruled out:

- **Stale process** — final attempt killed the old process, did a
  clean `bin/obj` wipe, rebuilt, launched fresh, and the user
  confirmed they saw "no change". So it's not a build caching issue.
- **Width-only Margin leaking into height** — Plan C removed the
  `ScrollContentPresenter` Margin style; no effect on the overflow.
- **Implicit Grid rows** — Plan B made every row definition explicit;
  no effect.
- **The simple revert pattern** — Plan D matched the v0.7 shape; no
  effect (the v0.7 shape was *never tested with long content*, so
  this proves only that the shape doesn't auto-scroll, not that the
  shape is broken).

### Still on the table (for tomorrow's session):

**H1: The MainWindow's `ContentControl Content="{Binding CurrentPage}"`
isn't propagating a bounded height to the loaded UserControl.**
ContentControl wraps content in its template; if the template doesn't
include a `Stretch` or explicit row/column attachment, the inner
UserControl may receive `double.PositiveInfinity` for height. Check
MainWindow.axaml:62 — `<ContentControl Content="{Binding CurrentPage}"/>`
— and try `HorizontalContentAlignment="Stretch"`
+ `VerticalContentAlignment="Stretch"` on the ContentControl.

**H2: The `ModuleHeader` UserControl reports an unbounded desired
height.** The ModuleHeader's outer Border has `Height="105"` — fixed
— but the outer element is a UserControl, and UserControls *can*
desire more than their content. Wrap the entire ModuleHeader contents
in a `<Border ClipToBounds="True">` or pin the UserControl's own
`Height="105"` from the outside on every view that uses it.

**H3: Avalonia 11.3 has a regression specific to
`DockPanel > Grid (with star column) > ScrollViewer`.** This wasn't
the case in 11.2.1 (the version at commit `942d864`). Check release
notes for 11.3.0 → 11.3.x. If found, either pin to 11.2.x or
introduce the DocumentationView-style nesting (extra
`Grid RowDefinitions="Auto,*"` layer between the column and the
ScrollViewer).

**H4: The `Avalonia.Themes.Fluent` ScrollViewer template has a
default `MaxHeight` or `VerticalContentAlignment="Stretch"` that we're
overriding via App.axaml's app-wide styles.** Audit App.axaml — the
`Style Selector="ScrollViewer"` `AllowAutoHide="False"` setter and the
`Style Selector="ScrollViewer /template/ ScrollContentPresenter#PART_ContentPresenter"`
`Margin="0,0,50,0"` setter — and try a one-off `<ScrollViewer>`
without app-wide styles applied (Classes-based opt-out).

**H5: The `MaxWidth="640" HorizontalAlignment="Left"` on the inner
StackPanel is propagating wrong measure information up.** Remove
`HorizontalAlignment="Left"` (default is `Stretch` inside a
ScrollViewer; Left makes the StackPanel report `Width=Auto` which may
also make the ScrollViewer compute a different content rect).

**H6: The recommended fix is to match DocumentationView's nesting
exactly.** Move the ProjectsView left rail from `Grid.Column="0"` to
`DockPanel.Dock="Left"`, and make the right-pane Grid an
`Auto,*` rather than a `340,*`. Specifically:

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top"/>
    <Border DockPanel.Dock="Left" Width="340" Background="#22222A">
        <DockPanel>
            <StackPanel DockPanel.Dock="Top">… new project + import …</StackPanel>
            <ListBox … />
        </DockPanel>
    </Border>
    <Grid Margin="20" RowDefinitions="Auto,*">
        <StackPanel Grid.Row="0" Orientation="Horizontal">
            <!-- optional toolbar; empty for ProjectsView -->
        </StackPanel>
        <ScrollViewer Grid.Row="1">
            <StackPanel MaxWidth="640">…</StackPanel>
        </ScrollViewer>
    </Grid>
</DockPanel>
```

The only thing that's empirically known to scroll under
`DockPanel + ModuleHeader + content + 50px scrollbar gap` is this
exact shape. Try it first.

## 6. What to do in the next session

1. Read this entire file.
2. Read [TESTING.md](TESTING.md) for the smoke-test contract.
3. Open the running app and reproduce the bug — confirm Projects
   edit pane overflows, confirm Home cards overflow.
4. **Do NOT start fixing.** First, instrument: add Avalonia DevTools
   (F12 hotkey is enabled in Debug builds) and inspect the actual
   `Bounds.Height` of the ScrollViewer, its `Viewport`, and its
   `Extent` properties. Compare to the desired height the StackPanel
   reports. This will tell you definitively WHICH parent in the
   chain is handing down an infinite constraint.
5. Test H6 first (match DocumentationView nesting exactly). If it
   works, the issue is the missing intermediate `Auto,*` Grid. If
   it doesn't, fall through to H1 (ContentControl alignment).
6. Whatever fixes it, **add an automated layout regression test**
   per [TESTING.md](TESTING.md) §"Layout regression tests" before
   considering the fix committed.

## 7. What NOT to do

- **Do not try yet another XAML shape without instrumenting first.**
  We've spent this session on hypothesis-driven trial-and-error and
  burned the user's trust. The next attempt needs to be evidence-led.
- **Do not commit the current broken state of HomeView/ProjectsView
  without an explicit fix or an explicit "ship as broken with these
  known gaps" decision from the user.** The 81 uncommitted files
  include lots of working M3/M4 work that should land — but Home
  and Projects need a resolution first.
- **Do not delete this file once the bug is fixed.** Update it
  with the resolution: what the actual root cause was, which
  hypothesis turned out to be right, and what the new canonical
  pattern is. Then it becomes a how-we-found-it postmortem
  rather than an open ticket.

## 8. Related history

- **v0.24 Skill Library Resources cut-off bug** — 9 layout
  iterations, every one passing tests and looking "done" before
  the user smoke-tested it and rejected it. Resolved in v0.25 by
  deleting the module entirely. Triggered the upgrade of the
  smoke-test rule from "milestone boundaries" to "every update"
  (HANDOFF.md §Conventions).
- **v0.27 Testing Manager wizard cut-off** — same family of bug
  in a different context. Resolved by the per-stage bounded Grid
  pattern (memory/bounded-wizard-stages.md).
- **This session — HomeView/ProjectsView overflow** — 4 patterns
  failed. The Skills saga's lesson ("don't iterate without
  evidence; the 10th attempt isn't going to succeed where 9
  failed") is the one to apply on day-2 of this debug.
