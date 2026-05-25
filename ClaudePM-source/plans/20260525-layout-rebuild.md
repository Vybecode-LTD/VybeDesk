# Plan: Incremental Layout Rebuild — HomeView + ProjectsView

**Created:** 2026-05-25
**Status:** Stage 0 verified, Stage 1 next
**Bug:** ScrollViewer content overflows without functional scrolling (9 failed fix attempts)
**Proven baseline:** DockPanel + ModuleHeader Dock="Top" + empty fill Border — WORKS

## Strategy

Rebuild both views one layer at a time from the verified header-only
baseline. Smoke test after each stage. The first stage that breaks
scrolling IS the root cause.

Full snapshots saved at `docs/debug/*-FULL-SNAPSHOT.axaml`.
Feature inventory at `docs/debug/DEBUG-FEATURE-INVENTORY.md`.

## Stages

### Stage 0 — Header only ✅ VERIFIED
- DockPanel > ModuleHeader Dock="Top" > empty Border as fill child
- Both views: header renders at top, empty area below, no overflow

### Stage 1 — Scrollable test content (NO real data)
- **ProjectsView:** Add a static left rail (Border Dock="Left" Width="340")
  + ScrollViewer as fill child containing 20 hardcoded TextBlocks
- **HomeView:** ScrollViewer as fill child containing 20 hardcoded TextBlocks
- **Success:** both scrollbars appear and scroll the full 20 items

### Stage 2 — Real data bindings (still simplified templates)
- **ProjectsView:** Replace left static content with bound ListBox;
  replace test TextBlocks with the real form fields
- **HomeView:** Replace test TextBlocks with bound ItemsControl + simplified
  card template (just Name + Description, no metrics row yet)
- **Success:** scroll still works with real data

### Stage 3 — Full card / form content
- **ProjectsView:** Full form (Logo, Model, Output path, action buttons)
- **HomeView:** Full card template (metrics row, logo slot, all badges)
  + pagination controls
- **Success:** scroll works with full content, buttons reachable

### Stage 4 — Styles + polish
- HomeView local styles (Button.home-card hover)
- Verify against full snapshot for feature parity

## Key constraints
- The DockPanel pattern is NON-NEGOTIABLE (proven by BugTrackerView,
  DocumentationView, NotebookView; Grid-column pattern proven broken)
- Left rail MUST use DockPanel.Dock="Left", NOT Grid.Column="0"
- ScrollViewer MUST be the DockPanel fill child (LastChildFill="True")
- HorizontalAlignment="Stretch" on inner content (not "Left")
- Each stage: build → test → smoke → proceed or stop
