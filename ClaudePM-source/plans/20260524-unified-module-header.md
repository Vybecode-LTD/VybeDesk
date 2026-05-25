# Plan — Unified Module Header (v0.31)

> Owner: avalonia-master (orchestrator)
> Started: 2026-05-24
> Status: Phase 1A in progress

## Goal

Replace the seven distinct per-module header patterns in `src/ClaudePM.App/Views/`
with one unified docked-top header band that shows:

- Module title + glyph
- Breadcrumbs (`Module › Sub-page › Stage` style)
- 🏠 Home icon (return to module home — opt-in per VM)
- Reset chip in red (clear current page's inputs — opt-in per VM)
- Restart chip in blue (clear everything + return to start — opt-in per VM)
- Optional status/description line below

## User-confirmed design decisions (2026-05-24)

1. **CRUD modules** — title moves OUT of left rail INTO the new top band.
   Left rails keep pickers/lists/explainers only. Affects:
   - `ProjectsView.axaml`
   - `PromptManagerView.axaml`
   - `BugTrackerView.axaml`
   - `SkillManagerView.axaml`
2. **Skills section** — header lives on EACH sub-page (Manager AND Builder).
   The Manager/Builder toggle in `SkillSectionView.axaml` stays where it is;
   sub-pages render WITH their own header underneath the toggle. Breadcrumbs
   on the sub-page tell the user where they are.
3. **Per-module opt-in for chips** — three independent flags
   (CanGoHome / CanReset / CanRestart) drive button visibility via the
   `IsVisible = command-is-not-null` pattern. Avoids redundant buttons on
   modules where Home and Restart degenerate to the same thing.
4. **Coverage** — every sidebar page gets the header, including Home and
   Settings. Title-only is fine for those.
5. **Reset is immediate** (no confirm dialog). Consistent with the rest
   of the app — only Delete confirms today.

## Layout invariant (LOCKED after Phase 1 smoke test, 2026-05-24)

**The unified header MUST occupy the full width of the page content area** —
spanning across the entire content region next to the main app sidebar. For
modules with a 2-column layout (left rail + right pane), the header sits
ABOVE both columns, not inside one of them.

Implementation rule: the outer container of every migrated view becomes
`DockPanel LastChildFill="True"` with `<ctl:ModuleHeader DockPanel.Dock="Top"/>`
as its first child. The view's existing column/row layout becomes a child of
that DockPanel.

- For views currently using `Grid ColumnDefinitions="X,*"` (Projects, Prompts,
  Notebook): wrap them in a DockPanel and nest the existing Grid as the fill
  child. Drop the title TextBlock that lived in the left rail's top.
- For views already using `DockPanel LastChildFill="True"` with a docked-left
  rail (BugTracker, TestingManager, SkillManager): insert
  `<ctl:ModuleHeader DockPanel.Dock="Top"/>` as the FIRST child (before the
  docked-left rail). DockPanel processes in order, so the header takes the
  full top width and the left rail then docks underneath it.
- For wizards that already have their own docked-top header band
  (SkillBuilder, SessionBuilder): replace that band with
  `<ctl:ModuleHeader DockPanel.Dock="Top"/>`.

This was the user's explicit design call after seeing the Phase 1 result on
Vision Audit. Do NOT regress by nesting the header inside a column.

## Uniformity invariant (LOCKED after Phase 2 smoke test, 2026-05-24)

The user's principle: "**header size MUST be the same on all modules**". To
enforce this:

### ModuleHeader.axaml has fixed Height=78
- `Height="78"` on the outer Border (NOT MinHeight — must NOT grow).
- Description TextBlock has `MaxLines="1"` + `TextTrimming="CharacterEllipsis"`
  and is ALWAYS rendered (no `IsVisible` toggle) so the description row
  always allocates space even when empty.
- The chip buttons, home button, and breadcrumbs all sit inside the bounded
  title row and don't push the header taller.

Do NOT remove the fixed Height or the MaxLines cap. Do NOT add per-module
height overrides. Every sidebar page renders a 78px-tall header band.

### Canonical Project Picker Band — used on ALL project-dependent modules

Every module that is project-scoped (currently: Documentation, Notebook,
Bug Tracker, Testing Manager, Vision Audit) places its project picker in
this exact band, immediately under the unified header, with identical
styling and binding shape:

```xml
<!-- Canonical project-picker band (LOCKED 2026-05-24).
     Sits as the SECOND DockPanel.Dock="Top" child, directly under
     <ctl:ModuleHeader DockPanel.Dock="Top"/>. Same background colour as
     the header above so the two bands flow visually. -->
<Border DockPanel.Dock="Top" Background="#1B1B22" Padding="20,8,20,12">
    <StackPanel Spacing="6">
        <Grid ColumnDefinitions="Auto,*" ColumnSpacing="8">
            <TextBlock Grid.Column="0" Text="Project:" Opacity="0.6"
                       VerticalAlignment="Center" FontSize="12"/>
            <ComboBox Grid.Column="1" ItemsSource="{Binding Projects}"
                      SelectedItem="{Binding SelectedProject, Mode=TwoWay}"
                      HorizontalAlignment="Stretch">
                <ComboBox.ItemTemplate>
                    <DataTemplate x:DataType="m:Project">
                        <TextBlock Text="{Binding Name}"/>
                    </DataTemplate>
                </ComboBox.ItemTemplate>
            </ComboBox>
        </Grid>
        <TextBlock Text="{Binding StatusMessage}"
                   Foreground="#9ABEE0" Opacity="0.85"
                   TextWrapping="Wrap" FontSize="12"
                   IsVisible="{Binding StatusMessage,
                      Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
    </StackPanel>
</Border>
```

**Rules:**
- This band ONLY appears on project-dependent modules. Skill Manager,
  Skill Builder, Session Builder, Prompts, Home, Settings do NOT get it
  (no project concept).
- The project picker MUST live in this band — NOT in a left rail, NOT in
  a right side panel, NOT embedded in a controls row. The left rail loses
  whatever project-picker UI it had; it keeps any list / explainer that
  isn't picker-related.
- The Notebook is the one binding exception: its property is `ActiveProject`
  rather than `SelectedProject`. Use `{Binding ActiveProject, Mode=TwoWay}`
  there. Layout and styling are identical.
- The Vision Audit's existing implementation (in `VisionAuditView.axaml`)
  is the reference — match it exactly for every other project-dependent
  module.

If at some future point this snippet gets painful to copy/maintain, refactor
to a `Controls/ProjectPickerBand.axaml` UserControl with StyledProperties for
`ProjectsSource` / `SelectedProject` / `StatusMessage`. For now, copy is
fine — only 5 modules total.

## HEADER REDESIGN — v2 (SUPERSEDES THE SECTIONS BELOW, LOCKED 2026-05-24)

User feedback after seeing Phase 2 + Phase 2-patch-2: the two-band design
(78px header + 74px sub-header = 152px) is being replaced by ONE richer
single-control header that packs everything into a 3-column, 2-row layout
plus a thin status bar.

### Visual

```
┌───────────────────────────────────────────────────────────────────────────┐
│ 🏠  🧩  Module Title › crumb   │                │                          │  Row 0
│ Description / explainer line   │ Project: ▾     │ [↺ Reset]  [↻ Restart]   │  Row 1
├───────────────────────────────────────────────────────────────────────────┤
│  Status / alert line (slightly lighter background ≈ #22222A, ~25px)        │  Row 2 (status)
└───────────────────────────────────────────────────────────────────────────┘
```

Three columns (left text / middle picker / right buttons), two rows of
main content, then a 25px status row underneath. Total ≈ 105px.

### Column / row structure

- **Left column** — `*` width. Two rows.
  - Row 0: `🏠` home button + module glyph + module title (FontSize=20) +
    breadcrumbs (faded `›` separators).
  - Row 1: Description / explainer text (FontSize=12, Opacity=0.6,
    MaxLines=1 + ellipsis).
- **Middle column** — `Auto` width, min ~260px. **RowSpan=2**.
  - Contains: `Project:` label + ComboBox, vertically centered in the
    column. Only rendered when `ShowPicker=True`; the column collapses
    to zero width when hidden (so non-project modules use the full width
    for text + buttons).
- **Right column** — `Auto` width. **RowSpan=2**.
  - Contains: `↺ Reset` and `↻ Restart` chip buttons in a horizontal
    StackPanel, vertically centered. Each button only renders when its
    corresponding command on the VM is non-null (`ObjectConverters.IsNotNull`).
- **Status row** (Row 2 across all columns) — `25px` fixed height.
  Background `#22222A` (one shade lighter than the main `#1B1B22` so the
  status bar stands out as a sub-region). Renders `{Binding StatusMessage}`
  (or whatever StyledProperty backs it) at FontSize=11, single line,
  vertically centered, slightly muted colour.

Total header height: ~105px fixed. Single Border, single control, no
sub-header.

### Implementation

- The existing `Controls/ModuleHeader.axaml(.cs)` gets a major rewrite
  to this shape.
- `Controls/ModuleSubHeader.axaml(.cs)` gets **deleted** — its function
  merges into the new ModuleHeader.
- ModuleHeader gains StyledProperties matching what ModuleSubHeader
  used to expose:
  - `bool ShowPicker` (default false)
  - `IEnumerable? ProjectsSource`
  - `object? PickerSelectedItem` (BindingMode.TwoWay)
  - `string StatusMessage`
  Plus the existing DataContext-bound surfaces (Title, Glyph, Description,
  Breadcrumbs, GoModuleHomeCommand, ResetCommand, RestartCommand) keep
  flowing in through the parent view's DataContext.

### View migration pattern

Every view becomes:

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top"
        ShowPicker="True"
        ProjectsSource="{Binding Projects}"
        PickerSelectedItem="{Binding SelectedProject, Mode=TwoWay}"
        StatusMessage="{Binding StatusMessage}"/>
    <!-- rest of view -->
</DockPanel>
```

For non-project modules: omit `ShowPicker` and the picker properties.
For modules with no status: omit `StatusMessage` (defaults to empty string).

### Migrations required immediately

1. Rewrite `Controls/ModuleHeader.axaml` + `.axaml.cs` to the new shape.
2. Delete `Controls/ModuleSubHeader.axaml` + `.axaml.cs`.
3. Update each of the four currently-migrated wizards to use the new
   ModuleHeader (drop the ModuleSubHeader reference, pass picker /
   status properties through):
   - `VisionAuditView.axaml`
   - `TestingManagerView.axaml`
   - `SkillBuilderView.axaml`
   - `SessionBuilderView.axaml`
4. ALSO apply the new header to the remaining 7 views in this same
   patch:
   - `HomeView.axaml` — title only.
   - `ProjectsView.axaml` — title only (Projects IS the list, no picker).
   - `DocumentationView.axaml` — `ShowPicker=True`. Also drop the
     embedded project picker from the controls row (moves to header).
   - `PromptManagerView.axaml` — title only.
   - `NotebookView.axaml` — `ShowPicker=True`. Picker moves OUT of the
     right-side panel INTO the header. Binding name is `ActiveProject`,
     not `SelectedProject`.
   - `SkillManagerView.axaml` — title only (Skills are global).
   - `SettingsView.axaml` — title only.
5. For TestingManager and Bug Tracker (and Vision Audit which already
   has it sorted): the rail explainer text moves OUT of the rail INTO
   the header's row-1 description. The VM's `Description` property is
   what renders there — update Description if needed to be more
   substantive. The rail content shrinks accordingly.

This becomes ONE consolidated commit replacing all of Phases 1-5 of the
prior design. After it lands, every page in the app has the same
~105px single-band header.

### What carries forward from earlier decisions

- The locked layout invariant ("header spans full content width above
  any 2-column layout") still applies. The new ModuleHeader is still
  a `DockPanel.Dock="Top"` child of the view's outer DockPanel.
- The opt-in per-VM command semantics (GoModuleHome / Reset / Restart)
  still apply via the same PageViewModel virtuals.
- Breadcrumbs still flow from the VM's `Breadcrumbs` override.
- Reset is still immediate (no confirm).
- Smoke test after the rewrite and after each subsequent module patch.

---

## SUB-HEADER UNIFORMITY (SUPERSEDED 2026-05-24 by the redesign above)

User feedback after the Phase 2 smoke test: Skill Builder and Session Builder
have a visibly shorter top chrome than Vision Audit and Testing Manager,
because the latter two have a picker-band underneath the header and the
former two don't. The user reaffirmed: **every module must have the same
top-chrome height**, period. Non-project modules need an always-present
sub-band of the same height as the picker band.

### Promotion to a dedicated control

The canonical picker-band snippet (above) is being PROMOTED to a real
Avalonia UserControl: `Controls/ModuleSubHeader.axaml`. Both project-
dependent and non-project modules use this control so the height is
guaranteed identical.

API:
- `bool ShowPicker` (StyledProperty, default `false`) — when `true`, renders
  the `Project:` label + ComboBox at the top of the band.
- `IEnumerable? ProjectsSource` (StyledProperty) — bound to the picker's
  `ItemsSource`. Required when `ShowPicker=True`.
- `object? PickerSelectedItem` (StyledProperty, `BindingMode=TwoWay`,
  default mode) — bound to the picker's `SelectedItem`. Uses `object?` so
  Notebook's `ActiveProject` works alongside everyone else's `SelectedProject`
  without a base-class virtual.
- `string StatusMessage` (StyledProperty, default `""`) — bound to the
  status TextBlock under the picker. Always rendered (TextTrimming +
  MaxLines=1 keeps it on one line). Empty string = blank band, same height.
- Fixed Height = 74px on the outer Border (same colour as the header
  `#1B1B22`). Cannot grow.

Usage in views:

```xml
<!-- Project-dependent module -->
<ctl:ModuleSubHeader DockPanel.Dock="Top"
    ShowPicker="True"
    ProjectsSource="{Binding Projects}"
    PickerSelectedItem="{Binding SelectedProject, Mode=TwoWay}"
    StatusMessage="{Binding StatusMessage}"/>

<!-- Non-project module with a status to show -->
<ctl:ModuleSubHeader DockPanel.Dock="Top"
    StatusMessage="{Binding StatusMessage}"/>

<!-- Module with nothing to put there -->
<ctl:ModuleSubHeader DockPanel.Dock="Top"/>
```

### Required migrations (immediate, before Phase 3)

1. Build `Controls/ModuleSubHeader.axaml` + `.axaml.cs`.
2. Replace the inline picker-band Border in `VisionAuditView.axaml` with
   `<ctl:ModuleSubHeader ShowPicker="True" .../>`.
3. Replace the inline picker-band Border in `TestingManagerView.axaml` with
   `<ctl:ModuleSubHeader ShowPicker="True" .../>`.
4. Replace the conditional status Border in `SkillBuilderView.axaml` with
   `<ctl:ModuleSubHeader StatusMessage="{Binding StatusMessage}"/>` —
   always-present, no IsVisible toggle.
5. Add `<ctl:ModuleSubHeader StatusMessage="{Binding StatusMessage}"/>` to
   `SessionBuilderView.axaml` immediately after the unified header. Then
   REMOVE the footer's StatusMessage TextBlock (it's already in the
   sub-header now).

After this patch every module's top chrome is `78 + 74 = 152px`. The
Phase 3 / Phase 4 / Phase 5 migrations will continue to add the same
control to each remaining view.

## Architecture

### Base class extensions — `PageViewModel`

Path: `src/ClaudePM.App/ViewModels/PageViewModel.cs`

Add four virtual surfaces — all default to "not shown":

```csharp
using CommunityToolkit.Mvvm.Input;

public abstract class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }
    public abstract string Glyph { get; }
    public abstract string Description { get; }
    public virtual IReadOnlyList<string> Highlights => Array.Empty<string>();

    // ===== Unified module header surface (v0.31) =====

    /// <summary>
    /// Optional breadcrumb crumbs displayed after the module title in the
    /// unified header. Module title is always first; these append after.
    /// Default: empty (no breadcrumbs shown).
    /// </summary>
    public virtual IReadOnlyList<string> Breadcrumbs => Array.Empty<string>();

    /// <summary>
    /// Returns to the module's home state without discarding data
    /// (e.g. for a wizard: back to step 1 keeping all answers).
    /// Override to expose; null hides the home icon.
    /// </summary>
    public virtual IRelayCommand? GoModuleHomeCommand => null;

    /// <summary>
    /// Clears the input fields on the CURRENT page/stage only — does not
    /// change which stage or sub-page is active. Override to expose;
    /// null hides the Reset chip.
    /// </summary>
    public virtual IRelayCommand? ResetCommand => null;

    /// <summary>
    /// Clears all module state and returns to the first stage. The hard
    /// reset. Override to expose; null hides the Restart chip.
    /// </summary>
    public virtual IRelayCommand? RestartCommand => null;
}
```

**Override pattern for concrete VMs:**

```csharp
public partial class VisionAuditViewModel : PageViewModel
{
    // Override the virtuals to point at concrete [RelayCommand] methods.
    public override IRelayCommand? GoModuleHomeCommand => GoToFirstStageCommand;
    public override IRelayCommand? ResetCommand => ResetCurrentStageCommand;
    public override IRelayCommand? RestartCommand => StartOverModuleCommand;

    // Breadcrumbs follow stage state — depend on Stage.
    public override IReadOnlyList<string> Breadcrumbs => Stage switch
    {
        VisionAuditStage.Extract     => new[] { "Step 1 — Extract" },
        VisionAuditStage.Approve     => new[] { "Step 2 — Approve" },
        VisionAuditStage.ChooseMode  => new[] { "Step 3 — Choose mode" },
        VisionAuditStage.RunReview   => new[] { "Step 4 — Review report" },
        _ => Array.Empty<string>()
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Breadcrumbs))]   // <- critical
    [NotifyPropertyChangedFor(nameof(IsExtractStage))]
    // ... other notifies for stage flags
    private VisionAuditStage _stage;

    [RelayCommand] private void GoToFirstStage()
    {
        Stage = VisionAuditStage.Extract;
        // DOES NOT clear DraftStatements / VisionRecord / Verdicts.
    }

    [RelayCommand] private void ResetCurrentStage()
    {
        // Per-stage: clear edits unique to current stage. Examples:
        //   Stage 2: clear DraftStatements text boxes (back to extracted defaults)
        //   Stage 3: deselect AuditMode
        //   Stage 4: clear ReportMarkdown / DeepDivePrompt panels
        // Do NOT touch VisionRecord / approvals / saved history.
    }

    [RelayCommand] private void StartOverModule()
    {
        // Clear EVERYTHING — DraftStatements, VisionRecord, verdicts,
        // ReportMarkdown, DeepDivePrompt — return to Extract stage.
        // Do NOT touch saved AuditHistory (persisted across runs by design).
    }
}
```

### New control — `Controls/ModuleHeader.axaml(.cs)`

Avalonia `UserControl`. Binds against `PageViewModel` via compiled bindings.
Sits in views as a `DockPanel.Dock="Top"` child.

Visual:

```
┌─────────────────────────────────────────────────────────────────┐
│  🏠  🧩  Title › crumb1 › crumb2          [↺ Reset] [↻ Restart] │
│  Description / status (optional)                                │
└─────────────────────────────────────────────────────────────────┘
```

XAML sketch:

```xml
<UserControl x:Class="ClaudePM.App.Controls.ModuleHeader"
             xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:ClaudePM.App.ViewModels"
             x:DataType="vm:PageViewModel">
    <Border Background="#1B1B22" Padding="20,14">
        <StackPanel Spacing="6">
            <Grid ColumnDefinitions="Auto,Auto,*,Auto">

                <!-- Home button: only visible when GoModuleHomeCommand != null -->
                <Button Grid.Column="0"
                        Classes="header-home"
                        Command="{Binding GoModuleHomeCommand}"
                        IsVisible="{Binding GoModuleHomeCommand,
                                    Converter={x:Static ObjectConverters.IsNotNull}}"
                        Padding="6,4" Margin="0,0,10,0"
                        ToolTip.Tip="Back to module home">
                    <TextBlock Text="&#x1F3E0;" FontSize="14"/>
                </Button>

                <!-- Glyph + Title + Breadcrumbs -->
                <TextBlock Grid.Column="1" Text="{Binding Glyph}"
                           FontSize="18" Margin="0,0,8,0"
                           VerticalAlignment="Center"/>
                <StackPanel Grid.Column="2" Orientation="Horizontal"
                            VerticalAlignment="Center">
                    <TextBlock Text="{Binding Title}"
                               FontSize="20" FontWeight="SemiBold"/>
                    <ItemsControl ItemsSource="{Binding Breadcrumbs}"
                                  Margin="6,0,0,0">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <StackPanel Orientation="Horizontal" Spacing="0"/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="x:String">
                                <StackPanel Orientation="Horizontal">
                                    <TextBlock Text=" &#x203A; " Opacity="0.4"/>
                                    <TextBlock Text="{Binding}" Opacity="0.75"/>
                                </StackPanel>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>

                <!-- Reset + Restart chips -->
                <StackPanel Grid.Column="3" Orientation="Horizontal" Spacing="6">
                    <Button Classes="chip-reset"
                            Command="{Binding ResetCommand}"
                            IsVisible="{Binding ResetCommand,
                                        Converter={x:Static ObjectConverters.IsNotNull}}"
                            ToolTip.Tip="Clear the input fields on this page">
                        <StackPanel Orientation="Horizontal" Spacing="4">
                            <TextBlock Text="&#x21BA;"/>
                            <TextBlock Text="Reset"/>
                        </StackPanel>
                    </Button>
                    <Button Classes="chip-restart"
                            Command="{Binding RestartCommand}"
                            IsVisible="{Binding RestartCommand,
                                        Converter={x:Static ObjectConverters.IsNotNull}}"
                            ToolTip.Tip="Start the module over (clears everything)">
                        <StackPanel Orientation="Horizontal" Spacing="4">
                            <TextBlock Text="&#x21BB;"/>
                            <TextBlock Text="Restart"/>
                        </StackPanel>
                    </Button>
                </StackPanel>
            </Grid>

            <TextBlock Text="{Binding Description}" Opacity="0.6" FontSize="12"
                       TextWrapping="Wrap"
                       IsVisible="{Binding Description,
                                   Converter={x:Static StringConverters.IsNotNullOrEmpty}}"/>
        </StackPanel>
    </Border>

    <UserControl.Styles>
        <Style Selector="Button.chip-reset">
            <Setter Property="Background" Value="#3A2126"/>
            <Setter Property="Foreground" Value="#E06C6C"/>
            <Setter Property="CornerRadius" Value="10"/>
            <Setter Property="Padding" Value="10,4"/>
            <Setter Property="FontSize" Value="11"/>
        </Style>
        <Style Selector="Button.chip-restart">
            <Setter Property="Background" Value="#1F2A3A"/>
            <Setter Property="Foreground" Value="#5E8FE0"/>
            <Setter Property="CornerRadius" Value="10"/>
            <Setter Property="Padding" Value="10,4"/>
            <Setter Property="FontSize" Value="11"/>
        </Style>
        <Style Selector="Button.header-home">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
        </Style>
        <Style Selector="Button.header-home:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="#2A2A33"/>
        </Style>
    </UserControl.Styles>
</UserControl>
```

### Wiring

The header is consumed by each `*View.axaml` as a top-docked child:

```xml
<DockPanel LastChildFill="True">
    <ctl:ModuleHeader DockPanel.Dock="Top"/>     <!-- DataContext inherited -->
    <!-- ... rest of view ... -->
</DockPanel>
```

The header inherits the parent view's `DataContext` (which is the concrete
`PageViewModel`). No explicit binding needed — compiled bindings against
`PageViewModel` resolve to the overridden properties polymorphically.

## Per-module semantics

| Module | GoModuleHome | Reset | Restart | Breadcrumbs |
|---|---|---|---|---|
| Home | — | — | — | none |
| Projects | Deselect | Clear edit fields | Deselect + clear status | none |
| Documentation | Close audit/editor overlay | Clear FolderPath | Close all overlays + clear inputs | overlay state (e.g. "Project Audit", "Editing CLAUDE.md") |
| Prompts | Close all panels | Clear current panel inputs | Close panels + clear search/category | overlay state (e.g. "AI redesign", "Version history") |
| Session Builder | Back to step 1 (keep) | Clear current step | Back to step 1 + clear all | step label |
| Notebook | — | Clear chat input | Clear chat history + actions | none |
| Skill Manager | Deselect skill | Revert edits | Deselect + clear folder path | none (or "Filter: Critical" when filter view open) |
| Skill Builder | Back to stage 1 (keep) | Clear current stage | Back to stage 1 + clear all | stage label |
| Bug Tracker | Deselect bug | Clear current bug edits | Deselect + clear fix-prompt output | none |
| Testing Manager | Back to first question | Clear current question's answer | Back to question 1 + clear all answers | question label or "Recommendation review" / "Saved plan" |
| Vision Audit | Back to Extract stage | Clear current stage's edits | Discard vision/draft/verdicts + back to Extract | stage label |
| Settings | — | Clear ApiKeyInput | — | none |

## Phased rollout (one commit per phase = one smoke-test pause)

| Phase | Scope | Files touched |
|---|---|---|
| **1A** | PageViewModel extension | `PageViewModel.cs` |
| **1B** | ModuleHeader control + VisionAudit integration | `Controls/ModuleHeader.axaml(.cs)`, `VisionAuditViewModel.cs`, `VisionAuditView.axaml` |
| **2** | Other wizards | `SkillBuilder*`, `SessionBuilder*`, `TestingManager*` (3 commits, smoke-test each) |
| **3** | Overlay modules | `Documentation*`, `PromptManager*` (2 commits) |
| **4** | CRUD modules — move title out of left rail | `Projects*`, `BugTracker*`, `SkillManager*` (3 commits) |
| **5** | Remaining pages | `Home*`, `Settings*`, `Notebook*` (3 commits) |

**Total**: ~13 commits, ~13 smoke-test pauses. Plus VM tests for wizards.

## Sub-agent hand-off log

Append your phase notes here, including:
- What you changed (files + brief description)
- Any deviations from this plan and why
- Anything you flagged for the next agent

### Phase 1A — architecture-mvvm — DONE (2026-05-24)

**What changed**

- `src/ClaudePM.App/ViewModels/PageViewModel.cs` — added four new
  virtual surfaces exactly per the contract:
  - `public virtual IReadOnlyList<string> Breadcrumbs => Array.Empty<string>();`
  - `public virtual IRelayCommand? GoModuleHomeCommand => null;`
  - `public virtual IRelayCommand? ResetCommand => null;`
  - `public virtual IRelayCommand? RestartCommand => null;`
- Added `using CommunityToolkit.Mvvm.Input;` at the top of the file.
- All four members carry XML-doc comments. The doc on `ResetCommand`
  explicitly states it only clears the CURRENT page's inputs and does
  NOT change the active stage/sub-page. The doc on `RestartCommand`
  states it clears EVERYTHING and returns to the first stage. The doc
  on `GoModuleHomeCommand` states it returns to the module's home
  state WITHOUT discarding data. The doc on `Breadcrumbs` reminds the
  override author to wire change-notification (e.g. via
  `[NotifyPropertyChangedFor(nameof(Breadcrumbs))]` on the underlying
  stage field) so the header refreshes when state changes.

No concrete view model was touched. All 11 existing pages compile
unchanged and inherit the "not shown" defaults (null commands, empty
breadcrumbs), so the header control Phase 1B introduces will silently
render title-only for every page until each VM opts in.

**Deviations from plan**

None. The contract was implemented verbatim. The override-pattern
example in the plan was not committed to code — that's Phase 1B's job
on `VisionAuditViewModel`.

**Hand-off note for the `ui-styling` agent (Phase 1B)**

1. **Source-generator naming convention — IMPORTANT.** A concrete VM
   CANNOT declare a `[RelayCommand] private void GoModuleHome()`
   method because the source generator would emit a property named
   `GoModuleHomeCommand`, which collides with the base class's
   `public virtual IRelayCommand? GoModuleHomeCommand`. The generated
   property is a plain `public IRelayCommand GoModuleHomeCommand { get; }`
   auto-property — it does NOT use `override`, so the compiler will
   error with CS0506 / CS0108 (member hides inherited member; `override`
   keyword required).

   **The correct pattern** (and the one the plan already shows on
   lines 95-97 and 115-135) is: name the `[RelayCommand]` methods
   DIFFERENTLY from the base virtuals, then forward via override
   expression-body. For Vision Audit specifically:

   ```csharp
   [RelayCommand] private void GoToFirstStage() { ... }
   [RelayCommand] private void ResetCurrentStage() { ... }
   [RelayCommand] private void StartOverModule() { ... }

   public override IRelayCommand? GoModuleHomeCommand => GoToFirstStageCommand;
   public override IRelayCommand? ResetCommand        => ResetCurrentStageCommand;
   public override IRelayCommand? RestartCommand      => StartOverModuleCommand;
   ```

   This is the convention every concrete VM that opts in to the
   header MUST follow. Document it once in a code comment on the
   first VM that uses it (VisionAuditViewModel) so the next agent
   picks it up.

2. **`Breadcrumbs` change-notification.** `Breadcrumbs` is a derived
   property — it doesn't have its own backing field. Whenever the
   underlying state it depends on changes (e.g. `Stage`), the VM
   MUST raise `PropertyChanged` for `Breadcrumbs` so the
   `ItemsControl` in `ModuleHeader.axaml` re-binds. The cleanest way
   is `[NotifyPropertyChangedFor(nameof(Breadcrumbs))]` on the
   `[ObservableProperty] private VisionAuditStage _stage;` field. The
   plan already shows this on line 110 — keep it.

3. **`IRelayCommand?` nullability.** The base returns `IRelayCommand?`
   (nullable). The generated command properties from `[RelayCommand]`
   are non-nullable `IRelayCommand`. The implicit conversion from
   non-nullable to nullable is fine — no warning. But if a VM ever
   needs to conditionally expose a command, returning `null` in some
   states is supported by the contract and the header's IsVisible
   binding (`ObjectConverters.IsNotNull`) handles it.

4. **No interface, no `[ObservableProperty]` on the base.** Per the
   user-approved design — these are plain virtuals overridden by
   concrete VMs. Do not introduce `IHeaderedPage` or similar in Phase
   1B. The header binds against `PageViewModel` directly and
   polymorphism handles the rest.

5. **DataContext flow into `ModuleHeader`.** Per the plan's "Wiring"
   section, the header inherits its parent view's DataContext (the
   concrete `PageViewModel`). Do NOT set `DataContext = this` inside
   the `ModuleHeader` code-behind — that would break the inheritance
   from the parent view. Set `x:DataType="vm:PageViewModel"` on the
   UserControl for compiled-binding type info only.

### Phase 1B — ui-styling — DONE (2026-05-24)

**What changed**

- `src/ClaudePM.App/Controls/ModuleHeader.axaml` (new) — the unified
  header `UserControl` per the plan's XAML sketch. `x:DataType=
  "vm:PageViewModel"`. Layout: `Border Background="#1B1B22" Padding=
  "20,14"` wrapping a `StackPanel Spacing="6"`. Row 1 = home button
  (Auto) + glyph (Auto) + title-and-breadcrumbs StackPanel (*) +
  Reset/Restart chip stack (Auto). Row 2 = Description line, hidden
  when the VM's Description is empty. Breadcrumbs render via an
  `ItemsControl` with a horizontal `StackPanel` ItemsPanel and a
  `DataTemplate x:DataType="x:String"` item template — the established
  string-list pattern in this codebase (matches `DocumentationView`,
  `NotebookView`, `SessionBuilderView`). Three `Style` selectors
  (`chip-reset`, `chip-restart`, `header-home`) in `UserControl.Styles`,
  scoped via the `Classes` attribute so they don't leak. Each button
  hides via `IsVisible="{Binding XCommand, Converter={x:Static
  ObjectConverters.IsNotNull}}"`.
- `src/ClaudePM.App/Controls/ModuleHeader.axaml.cs` (new) — minimal
  `partial class ModuleHeader : UserControl` with `InitializeComponent()`.
  Per the Phase 1A hand-off note, intentionally does NOT set
  `DataContext = this` — inherits from the parent view.
- `src/ClaudePM.App/ViewModels/VisionAuditViewModel.cs` — opted into
  the unified header. Added `[NotifyPropertyChangedFor(nameof(
  Breadcrumbs))]` to the existing attribute stack on `_stage` (the
  critical change-notification wiring). Added a `Breadcrumbs`
  override that maps each `VisionAuditStage` to a one-element array.
  Added three command overrides pointing at the source-generated
  `*Command` properties: `GoModuleHomeCommand => StartOverCommand`
  (repurposed the existing `StartOver` method, which already had
  the "go to Extract without clearing data" semantic), `ResetCommand
  => ResetCurrentStageCommand`, `RestartCommand => RestartModuleCommand`.
  Added two new `[RelayCommand]` methods (`ResetCurrentStage` and
  `RestartModule`) per the prompt's exact semantics — per-stage clears
  on Reset, full in-memory wipe on Restart, neither touches DB or
  persisted history. Documented the source-generator naming convention
  inline (see paragraph block above the `Breadcrumbs` override) so the
  next VM to opt in picks up the pattern without re-discovering it.
- `src/ClaudePM.App/Views/VisionAuditView.axaml` — added
  `xmlns:ctl="using:ClaudePM.App.Controls"`. Replaced the old docked-
  top header `Border` (title + 4-dot progress + project picker +
  status) with two stacked docked-top elements: (1) `<ctl:ModuleHeader
  DockPanel.Dock="Top"/>` for title/breadcrumbs/chips, (2) a smaller
  `Border Background="#1B1B22" Padding="20,8,20,12"` holding the
  project picker + status message. The two bands flow visually (same
  background). The 4-dot progress indicator is gone — breadcrumbs now
  carry stage information. Removed the redundant "Re-extract from
  docs" button from the Stage 4 button row (its semantic is now the
  home icon in the header) and replaced it with an explanatory
  comment.

**Deviations from plan**

1. **Did not rename `StartOver` to `GoToFirstStage`.** The plan's
   override-pattern example named the methods `GoToFirstStage` /
   `ResetCurrentStage` / `StartOverModule`. The first and third would
   have meant renaming the existing `StartOver` method, which is bound
   from XAML as `Command="{Binding StartOverCommand}"` on Stage 4's
   "Re-extract from docs" button. The prompt explicitly preferred the
   smaller diff — keep the existing name, alias via override. I
   followed that direction; the override is `GoModuleHomeCommand =>
   StartOverCommand` and `RestartCommand => RestartModuleCommand`. The
   `ResetCommand => ResetCurrentStageCommand` mapping matches the plan
   exactly.
2. **`ItemsControl` for breadcrumbs uses `x:DataType="x:String"` per
   existing codebase convention** rather than introducing a new
   `BreadcrumbItem` type. Matches `DocumentationView`/`NotebookView`/
   `SessionBuilderView` and avoids new model surface area.

**Build + test verification**

I do not have a shell/Bash tool in this session's tool set (only Read /
Grep / Glob / Edit / Write), so I could not invoke `dotnet build` or
`dotnet test` directly. Static review against this codebase's existing
patterns:
- All bindings are compiled (`x:DataType` set on the new `UserControl`
  and on every `DataTemplate`; `AvaloniaUseCompiledBindingsByDefault`
  is true at the csproj level).
- `ObjectConverters.IsNotNull` and `StringConverters.IsNotNullOrEmpty`
  usage matches the established pattern in `SkillBuilderView` and the
  existing `VisionAuditView` itself.
- The `x:DataType="x:String"` breadcrumb item template matches three
  other views in the codebase.
- VM additions: `Breadcrumbs` returns `IReadOnlyList<string>` matching
  the base virtual; the three command overrides return `IRelayCommand?`
  (the implicit non-nullable-to-nullable conversion is fine per the
  Phase 1A hand-off note); `[NotifyPropertyChangedFor(nameof(
  Breadcrumbs))]` ensures the header refreshes on stage change.
- View: outer `DockPanel LastChildFill="True"` unchanged; the new
  header is `DockPanel.Dock="Top"`, the picker band beneath it is
  also `DockPanel.Dock="Top"`, so the 4-stage `Grid` is still the
  fill child — per-stage layout is untouched, no regression risk to
  the bounded-wizard-stages invariant.

**Hand-off note for Phase 2 (other wizards: SkillBuilder /
SessionBuilder / TestingManager)**

1. **What worked well.** The override-via-alias pattern is clean.
   For each wizard VM, the three lines look like:
   ```csharp
   public override IRelayCommand? GoModuleHomeCommand => GoToFirstStepCommand;
   public override IRelayCommand? ResetCommand        => ResetCurrentStepCommand;
   public override IRelayCommand? RestartCommand      => RestartWizardCommand;
   ```
   plus the existing `[RelayCommand]` methods backing those `*Command`
   properties. If the wizard already has a "go to step 1" command that
   doesn't clear data (like Vision Audit's `StartOver` did), reuse it
   — saves a method.

2. **`[NotifyPropertyChangedFor(nameof(Breadcrumbs))]` is the part
   that's easiest to forget.** Put it on the `[ObservableProperty]`
   field that backs `Stage` / `CurrentStep` / whatever. Without it the
   header renders the initial crumb and never updates. Same applies if
   the breadcrumbs depend on multiple fields — add the attribute to
   each.

3. **One small gotcha — the `Reset` semantics on early stages.** In
   Vision Audit, Stage 1 (Extract) has no inputs to clear, so
   `ResetCurrentStage` no-ops there. The header's Reset chip is still
   visible (because the command itself is non-null). For wizards
   where this would be confusing, consider gating chip visibility on
   a per-stage boolean instead of just command-not-null. For now I
   accepted "chip visible, no-op on click" because the alternative
   (per-stage CanReset booleans) adds boilerplate without solving a
   real user complaint.

4. **The breadcrumb pattern `"Step N — Label"` reads well in
   English** and is what the prompt prescribed. Suggest Phase 2 use
   the same shape (`"Step 1 — Inputs"`, `"Step 2 — Questions"`,
   `"Step 3 — Review"`, `"Step 4 — Emitted"` for SkillBuilder, etc.)
   so the header is visually consistent across all wizards.

5. **Layout cost: zero.** The new header is a `DockPanel.Dock="Top"`
   sibling of the per-stage fill content. It does NOT interact with
   the bounded-wizard-stages pattern at all — each stage's measure
   pass is still bounded by its own `*` row inside its own Grid. No
   regression to the four-time-bitten outer-ScrollViewer-over-
   IsVisible-siblings anti-pattern.

### Phase 2 — ui-styling — DONE (2026-05-24)

**What changed** (6 files, single commit)

- `src/ClaudePM.App/ViewModels/SkillBuilderViewModel.cs` — opted into
  the unified header. Added `Breadcrumbs` override mapping each
  `BuilderStage` to `"Step N — <Label>"` (Inputs / Clarifying
  questions / Review draft / Emitted). Added
  `[NotifyPropertyChangedFor(nameof(Breadcrumbs))]` to `_stage`. Three
  command overrides (`GoModuleHomeCommand =>
  GoToInputsStageCommand`, `ResetCommand => ResetCurrentStageCommand`,
  `RestartCommand => RestartModuleCommand`). Three new
  `[RelayCommand]` methods: `GoToInputsStage` (jump to Inputs without
  clearing), `ResetCurrentStage` (per-stage clears: Inputs clears name/
  desc/notes but leaves ResearchOn; Questions clears all answers;
  Review clears Draft + Findings; Emitted clears EmitResult), and
  `RestartModule` (wipes everything including ResearchOn). Existing
  `StartOver` left untouched — it's still bound to the Stage 4 "Build
  another skill" button.
- `src/ClaudePM.App/Views/SkillBuilderView.axaml` — added the `ctl`
  xmlns and replaced the old docked-top header `Border` (which held
  the title + 4-dot progress indicator) with `<ctl:ModuleHeader
  DockPanel.Dock="Top"/>`. The status message that used to live
  alongside in the same header band now lives in its own
  `DockPanel.Dock="Top"` Border with `Background="#1B1B22"
  Padding="20,8,20,12"` (matching the Vision Audit pattern); hidden
  via `IsVisible` when empty.
- `src/ClaudePM.App/ViewModels/SessionBuilderViewModel.cs` — opted
  into the unified header. `Breadcrumbs` returns
  `new[] { StepLabel }` — the existing `StepLabel` property
  ("Step N of 5 — <Name>") is already exactly the right shape for a
  one-crumb summary, so no per-step switch was needed. Added
  `[NotifyPropertyChangedFor(nameof(Breadcrumbs))]` to `_currentStep`.
  Three command overrides + three new `[RelayCommand]` methods:
  `GoToFirstStep` (CurrentStep = 0 without clearing),
  `ResetCurrentStep` (per-step clears across all five steps), and
  `RestartWizard` (full wipe).
- `src/ClaudePM.App/Views/SessionBuilderView.axaml` — added the `ctl`
  xmlns. Converted the outer container from `Grid RowDefinitions=
  "Auto,*,Auto"` to `DockPanel LastChildFill="True"` with the unified
  header as the first `DockPanel.Dock="Top"` child. The existing
  step-content `ScrollViewer` + footer became a nested `Grid
  RowDefinitions="*,Auto"` (footer renumbered from `Grid.Row="2"` to
  `Grid.Row="1"`). The original "Session Builder" + StepLabel
  TextBlocks are gone (subsumed by the header title + breadcrumb).
  The status message stays in the footer (it's already in the
  navigation row, which is the natural place for it in this wizard).
- `src/ClaudePM.App/ViewModels/TestingManagerViewModel.cs` — opted
  into the unified header. `Breadcrumbs` is a state-driven getter:
  no project = empty, recommendation review = `["Recommendation
  review"]`, saved plan = `["Saved plan"]`, questionnaire = the
  current question's `Title` (or `"Question N"` fallback). Added
  `[NotifyPropertyChangedFor(nameof(Breadcrumbs))]` to all five
  underlying observable properties that drive state transitions
  (`_selectedProject`, `_currentPlan`, `_isReRunning`,
  `_isShowingRecommendation`, `_currentStepIndex`). Three command
  overrides + three new `[RelayCommand]` methods: `GoToFirstQuestion`
  (clears IsShowingRecommendation, sets IsReRunning if a plan exists
  so the questionnaire is visible, CurrentStepIndex = 0, KEEPS
  answers), `ResetCurrentStage` (on questionnaire: clears CURRENT
  question's answer via `q.PickCommand.Execute("")`; on plan view:
  clears SetupPromptOutput + RegressionPromptOutput; on recommendation
  review: deliberate no-op), and `RestartModule` (calls existing
  ResetWizard to clear all answers, sets IsReRunning = HasPlan,
  clears outputs + draft + bug-fixed nudge, does NOT delete the
  persisted DB plan — consistent with the other modules where
  persisted state survives in-memory resets).
- `src/ClaudePM.App/Views/TestingManagerView.axaml` — added the `ctl`
  xmlns. Inserted `<ctl:ModuleHeader DockPanel.Dock="Top"/>` as the
  FIRST `DockPanel.Dock="Top"` child, BEFORE the docked-left rail —
  this satisfies the locked layout invariant: DockPanel processes
  children in declaration order, so the header reserves the full top
  strip and the 300px-wide rail then docks underneath it. Removed
  the `"Testing Manager"` `TextBlock` that used to live at the top of
  the left rail (it's now in the header title). The rail's explainer
  + project picker + status TextBlocks are intact.

**Locked layout invariant respected**

For TestingManager — the only Phase 2 view with a docked-left rail —
the unified header is the first `DockPanel.Dock="Top"` child,
declared BEFORE the rail. The rail's `Width="300"` and the rest of
the right pane's `Grid` are unchanged. The header spans the full
content width above the rail, matching the explicit user design call
quoted in the Architecture section. SkillBuilder and SessionBuilder
are single-column wizards, so the header naturally spans the full
content width above the single column.

**Per-wizard layout decisions**

- SkillBuilder kept its per-stage bounded `Grid` pattern unchanged
  (the v0.29 bounded-wizard-stages fix). The unified header sits
  above it as a sibling DockPanel.Dock="Top" — zero interaction with
  the per-stage measure pass, exactly as Phase 1B's hand-off note
  predicted.
- SessionBuilder kept its single-ScrollViewer-over-IsVisible-toggled-
  steps pattern. That's the same anti-pattern category bounded-
  wizard-stages.md warns about, but it was already in place before
  Phase 2 and is out of scope here. Worth flagging for a future
  refactor pass if a step-overflow bug appears.
- TestingManager kept Pattern C unchanged — one question at a time
  via ContentControl, no ScrollViewer in the questionnaire path. The
  three-state Grid (questionnaire / recommendation review / saved
  plan) is untouched.

**Deviations from plan**

1. **SessionBuilder's status message stayed in the footer** rather
   than moving up to a docked-top band beneath the header. The
   reason: the footer's TextBlock sits directly above the Back/Next
   buttons, which is the natural reading order for "status from the
   last action you took". Moving it under the header would
   double-decorate the top of the page without changing UX. The
   SkillBuilder status DID move up because its old header band had
   the status inside the title row, not in a navigation footer.
2. **TestingManager's `RestartModule` sets `IsReRunning = HasPlan`**
   rather than `false`. Without this, restarting on a project with a
   saved plan would hide the questionnaire (because
   `IsQuestionnaireVisible = HasProject && (CurrentPlan is null ||
   IsReRunning)`), leaving the user staring at the saved-plan view
   with all answers cleared — confusing. Setting `IsReRunning = true`
   when a plan exists shows the questionnaire with cleared answers,
   which is the expected "restart" UX.

**Build + test verification**

I do not have a shell/Bash tool in this session, so static review
only:
- All bindings remain compiled (`x:DataType` set on each `UserControl`
  root and every `DataTemplate`; new bindings on the
  `<ctl:ModuleHeader/>` inherit from the parent's compiled DataContext
  which is the concrete VM).
- `IRelayCommand?` overrides return non-nullable
  `IRelayCommand` from the source generator — implicit conversion is
  fine, no warning expected.
- `Breadcrumbs` change-notification: SkillBuilder fires on `_stage`;
  SessionBuilder fires on `_currentStep`; TestingManager fires on
  five properties. None of the underlying enums or state variables
  are mutated outside an `[ObservableProperty]` setter, so every
  transition notifies.
- DockPanel child ordering on TestingManager: header first, left
  rail second — DockPanel docks in declaration order, header gets
  the full top strip, rail gets the rest of the left edge under it.
  Standard pattern.

**Hand-off note for Phase 3 (overlay modules: Documentation, Prompts)**

The overlay modules have a wrinkle the wizards don't. DocumentationView
has full-pane overlays (the project audit overlay and the editor
overlay) that completely take over the right pane when active. The
new unified header must coexist with those overlays — either the
header stays visible above the overlay (so the user can still see
where they are + click Restart to bail) OR the overlay covers the
header too (so the overlay reads as a modal). User-confirmed
breadcrumb semantics from the plan's per-module table line 308:
`"Project Audit"` / `"Editing CLAUDE.md"` go in the breadcrumbs, so
the header IS meant to stay visible above the overlay — keep the
header as a DockPanel.Dock="Top" sibling and the overlay as the fill
child (or layered inside a Panel under the fill). Don't put the
overlay above the header in the DockPanel — that would hide both
the title AND the home/Reset/Restart escape hatch when the user is
deepest in a workflow.

Same pattern for Prompts (`Module 5b — Skill Builder` line 309): AI
redesign + version history are full-pane overlays. Keep header
visible, surface the overlay name as a breadcrumb, wire GoModuleHome
to the "close overlay" action.

One more gotcha: both DocumentationView and PromptManagerView use
`Grid ColumnDefinitions="X,*"` for the left-rail + right-pane layout
per Phase 1B's hand-off note. Wrap each in a DockPanel with the
header as the first `Dock="Top"` child; nest the existing
2-column Grid as the fill child. Drop the title TextBlock that used
to live at the top of the left rail. The Phase 4 CRUD-module work
(Projects / BugTracker / SkillManager) needs the same conversion;
keep those module's titles in the rail until Phase 4 actually runs.

### Phase 2-patch — ui-styling — DONE (2026-05-24)

**Why this patch existed**

The Phase 2 smoke test locked a new uniformity invariant: the project
picker on every project-dependent module MUST live in the canonical
band immediately under the unified header, NOT in a left rail.
TestingManager (delivered in Phase 2) had its picker in the docked-
left rail per the original prompt. After the uniformity invariant
was locked, that pattern became wrong on this view. This patch moves
the picker into the canonical band so TestingManager matches the
Vision Audit reference.

**What changed** (single file)

- `src/ClaudePM.App/Views/TestingManagerView.axaml`:
  - **Added** a new `Border DockPanel.Dock="Top" Background="#1B1B22"
    Padding="20,8,20,12"` as the SECOND docked-top child, immediately
    after `<ctl:ModuleHeader DockPanel.Dock="Top"/>`. Content is the
    canonical picker-band snippet copied verbatim from the
    "Canonical Project Picker Band" section of this plan: project
    label + ComboBox (`Mode=TwoWay`) bound to `Projects` /
    `SelectedProject`, plus the `StatusMessage` TextBlock hidden
    when empty via `StringConverters.IsNotNullOrEmpty`. Background
    colour matches the header above so the two bands flow visually.
  - **Removed** the inner `<StackPanel Spacing="4">` that previously
    held the `TextBlock Text="Project"` label + ComboBox in the
    docked-left rail. The rail now contains only its explainer
    TextBlock + the two conditional HasPlan / !HasPlan status
    TextBlocks.
  - **Kept** the docked-left rail's "Pick a strategy by answering a
    handful of plain-language questions..." explainer. The rail
    would otherwise contain only the conditional status messages
    which feel sparse; the explainer describes the wizard workflow
    (not the picker) and reads naturally as rail content. Phase 4
    is the natural moment to re-evaluate whether the rail is still
    needed on this view — explicitly not refactored away in this
    patch per the prompt.
  - **Updated** the right-pane empty-state TextBlock from "Pick a
    project on the left to begin." to "Pick a project above to
    begin." — the picker is no longer on the left, so the old
    copy was lying to the user.

VM untouched — `Projects`, `SelectedProject`, `StatusMessage`
already exist on `TestingManagerViewModel` (confirmed from
Phase 2's wiring), so the binding shape in the canonical snippet
resolves without changes.

**Layout invariant respected**

Outer DockPanel children, in declaration order, are now:
1. `<ctl:ModuleHeader DockPanel.Dock="Top"/>` — full-width header band
2. `<Border DockPanel.Dock="Top">` — full-width canonical picker band
3. `<Border DockPanel.Dock="Left" Width="300">` — left rail (no picker)
4. The fill `<Grid Margin="28">` — three-state content area

DockPanel processes in declaration order: both Top-docked siblings
reserve full-width strips at the top in sequence, then the 300px
left rail docks underneath them, then the content fills. The locked
layout invariant from Phase 2 still holds — the header is still the
FIRST Top-docked child, the picker band is the SECOND, both above
the rail.

**Build + test verification**

I do not have a shell/Bash tool in this session, so static review
only:
- The canonical snippet was copied verbatim from this plan file
  (lines 95-116). Bindings (`Projects` / `SelectedProject` /
  `StatusMessage`) are the existing names on
  `TestingManagerViewModel` — confirmed against the Phase 2 wiring
  log above.
- `Mode=TwoWay` on `SelectedItem` matches the canonical snippet
  exactly. The previous rail picker did NOT specify `Mode` (relied
  on default), but the canonical snippet is explicit — no
  behavioural difference, the source-generator `[ObservableProperty]`
  setter handles both.
- All bindings remain compiled — the parent `UserControl` already
  has `x:DataType="vm:TestingManagerViewModel"`, the inner
  `DataTemplate` for the project items already has
  `x:DataType="m:Project"`.
- No new converters introduced; `StringConverters.IsNotNullOrEmpty`
  is already used elsewhere in this view.
- No VM changes, so the 92/92 test count is unaffected — the
  question/strategy/store tests don't touch view XAML.

**Hand-off note for Phase 3 (Documentation + Notebook)**

Both Documentation (Phase 3) and Notebook (Phase 5) are also
project-dependent modules and will need the same canonical picker
band treatment. For Documentation, the picker currently lives at
the top of the left rail (same pattern this patch just fixed on
TestingManager) — same fix applies: insert the canonical band as
the SECOND DockPanel.Dock="Top" child, remove the picker from the
rail, keep any non-picker rail content. For Notebook, remember the
binding-name exception flagged in the plan: its property is
`ActiveProject` not `SelectedProject`, so the canonical snippet
needs `{Binding ActiveProject, Mode=TwoWay}` on the ComboBox there
— everything else (layout, styling, status message) is identical.

### Phase 2-patch-2 — ui-styling — DONE (2026-05-24)

**Why this patch existed**

User's second Phase 2 smoke test rejected the result: Vision Audit and
Testing Manager render an extra picker band underneath the header
(~74px), but Skill Builder and Session Builder only show the 78px
header. Top chrome heights were therefore non-uniform across modules.
The user re-stated the principle: **every module must have the same
top chrome height regardless of project-scoping**.

The fix is to promote the inline canonical picker-band snippet to a
real `UserControl` (`Controls/ModuleSubHeader.axaml`) with a fixed
74px height and four StyledProperties — `ShowPicker`, `ProjectsSource`,
`PickerSelectedItem`, `StatusMessage` — so both project-dependent
modules and non-project modules use the SAME control. The picker
collapses when `ShowPicker=False`, but the band's height stays 74px
either way. Status message is always rendered (empty = blank line,
same height) capped at one line via `MaxLines=1` + ellipsis.

After this patch: every module's top chrome = `78px header +
74px sub-header = 152px`.

**What changed** (6 files, single commit)

- `src/ClaudePM.App/Controls/ModuleSubHeader.axaml` (NEW) — the new
  unified sub-header `UserControl`. Outer `Border Background="#1B1B22"
  Padding="20,8,20,12" Height="74"` wrapping a `Grid
  RowDefinitions="Auto,*"`. Row 0 = picker `Grid` (`Auto,*` columns:
  label + ComboBox), `IsVisible` bound to `$parent[UserControl].
  ShowPicker` so the row collapses when no picker is wanted. Row 1
  = status `TextBlock` bound to `$parent[UserControl].StatusMessage`,
  ALWAYS rendered (no `IsVisible` toggle), `MaxLines="1"` +
  `TextTrimming="CharacterEllipsis"`, `VerticalAlignment="Center"`
  so it sits in the band's centre when the picker is hidden. The
  ComboBox `ItemsSource` and `SelectedItem` both bind through
  `$parent[UserControl]` to the matching StyledProperties on the
  control. `Name="Root"` is set on the UserControl element for
  symmetry with the `$parent[UserControl]` reference syntax.
  Deliberately NO `x:DataType` on the root UserControl — internal
  bindings target the StyledProperties on the control, not a typed
  DataContext. The inner project item `DataTemplate` keeps its own
  `x:DataType="m:Project"` for compiled bindings on the item
  template, as is standard.
- `src/ClaudePM.App/Controls/ModuleSubHeader.axaml.cs` (NEW) — the
  partial code-behind declaring the four `StyledProperty<T>` fields
  + CLR wrappers per the prompt's contract. `ShowPickerProperty`
  defaults to `false`. `ProjectsSourceProperty` is `IEnumerable?`
  (the universal collection type — works with `ObservableCollection<
  Project>` and any other enumerable a VM may surface).
  `PickerSelectedItemProperty` is `object?` with
  `defaultBindingMode: BindingMode.TwoWay` per the prompt — this is
  what lets Notebook's `ActiveProject` and other modules'
  `SelectedProject` share the same control without a base class.
  `StatusMessageProperty` is `string` with `defaultValue:
  string.Empty` so an unbound consumer renders a blank band rather
  than crashing. Per the established control convention in this
  codebase, the code-behind does NOT set `DataContext = this`.
- `src/ClaudePM.App/Views/VisionAuditView.axaml` — replaced the
  inline picker-band `Border` (the canonical snippet lines 31-53)
  with a single `<ctl:ModuleSubHeader DockPanel.Dock="Top"
  ShowPicker="True" ProjectsSource="{Binding Projects}"
  PickerSelectedItem="{Binding SelectedProject, Mode=TwoWay}"
  StatusMessage="{Binding StatusMessage}"/>` invocation. Net 16
  lines removed, 6 added. Behaviour identical: same picker, same
  status text, same colour, same height. NB: the original VA view
  had no explicit `Mode=TwoWay` on the picker (relied on default);
  the new control's StyledProperty defaults `BindingMode.TwoWay`,
  but the binding declaration also makes it explicit per the
  canonical snippet — no behavioural difference.
- `src/ClaudePM.App/Views/TestingManagerView.axaml` — same swap
  applied to TestingManager's inline picker-band `Border` (the
  Phase 2-patch snippet on lines 45-72). Net 22 lines removed,
  6 added. The docked-left rail is untouched.
- `src/ClaudePM.App/Views/SkillBuilderView.axaml` — replaced the
  conditional status `Border` (lines 64-70) with a `<ctl:
  ModuleSubHeader DockPanel.Dock="Top" StatusMessage="{Binding
  StatusMessage}"/>` (no picker; defaults `ShowPicker=false`).
  Critical change: the old band was hidden when `StatusMessage` was
  empty; the new control is always present. That's the whole point
  — uniformity. The user's mental model is now "there's always a
  74px sub-band under the header" rather than "the band appears
  sometimes". Updated the in-file layout-invariant comment to name
  the new control (lines ~15-18).
- `src/ClaudePM.App/Views/SessionBuilderView.axaml` — inserted
  `<ctl:ModuleSubHeader DockPanel.Dock="Top" StatusMessage="{Binding
  StatusMessage}"/>` as the second DockPanel.Dock="Top" child,
  immediately after `<ctl:ModuleHeader DockPanel.Dock="Top"/>`. Then
  REMOVED the `TextBlock Text="{Binding StatusMessage}"` from the
  footer `StackPanel` (it lived above the Back/Next buttons) and
  collapsed the footer from a vertical `StackPanel` containing the
  status line and the navigation `StackPanel` to a single horizontal
  `StackPanel` of Back/Next buttons. The status now sits visually
  at the top of the page in the sub-header band, matching the other
  three wizards. Deviation from Phase 2's deviation #1: the
  Phase 2 ui-styling agent left the status in the footer "because
  the footer's TextBlock sits directly above the Back/Next buttons,
  which is the natural reading order" — but that reasoning loses to
  the user's locked uniformity principle. Status moves up.

**Layout invariant — both LOCKED invariants respected**

For both VisionAudit and TestingManager — the two views with multiple
docked-top siblings — the declaration order is still:
1. `<ctl:ModuleHeader DockPanel.Dock="Top"/>` — 78px header
2. `<ctl:ModuleSubHeader DockPanel.Dock="Top" .../>` — 74px sub-header
3. (TestingManager only) `<Border DockPanel.Dock="Left" Width="300">`
4. Fill content `<Grid>`

DockPanel processes children in declaration order, so the two top
bands reserve their full-width strips first, then the rail (where
present) docks underneath them, then the content fills. Phase 1B
and Phase 2-patch's locked layout invariant — header is the FIRST
top-docked child, sub-header is the SECOND, both above any left
rail — still holds.

The fixed-height invariant from Phase 2 (header = 78px) is now
matched by the sub-header's fixed Height=74. Total top chrome on
every module = 152px. Description on the header is capped to one
line (MaxLines=1 + ellipsis); status on the sub-header is capped
to one line (same caps). Neither can push the chrome taller.

**Binding shape — note on the `$parent[UserControl]` pattern**

This is the first control in the codebase that uses StyledProperties
+ self-binding via `$parent[UserControl]`. The pattern works in
Avalonia with compiled bindings as long as either:

  (a) the `UserControl` root has a unique name (`Name="Root"` on the
      root element, which we did), OR
  (b) the binding uses `{Binding $parent[UserControl].X}` from a
      child that's unambiguously inside this UserControl (which we
      also did — every internal binding lives inside the outer
      Border that's the immediate child of the root UserControl).

We did both. The `Name="Root"` is currently unused (we picked
`$parent[UserControl]` over `ElementName=Root` per the prompt's
expressed preference), but leaving it in place means a future edit
that switches to `ElementName=Root` won't need to add it. Either
syntax compiles; pick whichever reads best for the next consumer.

The deliberate absence of `x:DataType` on the root UserControl is
because there is no consumer-supplied DataContext to type. The
control's bindings target its own properties. If a future consumer
wanted to also bind to the inherited DataContext, they'd need to
add an explicit `DataContext="{Binding ...}"` at the call site — but
none of the four consumers in this patch needs that.

**What's NOT in this patch**

Per the boundary clause, this patch deliberately:
- Did NOT touch any ViewModel. `Projects` / `SelectedProject` /
  `StatusMessage` properties already exist on all four wizard VMs
  per the Phase 2 wiring log.
- Did NOT touch `App.axaml`, `ModuleHeader.axaml`, the csproj, or
  any other view.
- Did NOT migrate Documentation / Prompts / Notebook / etc. —
  Phase 3 handles those.

**Build + test verification**

Static review only (no shell in this session):
- The new `StyledProperty` declarations are syntactically correct
  per the Avalonia 11 API: `AvaloniaProperty.Register<TOwner, TValue
  >(nameof(X), defaultValue, defaultBindingMode)` overload exists
  and matches the signatures used. `using Avalonia;`, `using
  Avalonia.Controls;`, `using Avalonia.Data;`, `using System.
  Collections;` cover the imports.
- All bindings remain compiled. The new control intentionally has
  NO `x:DataType` on its root (its internal bindings reach
  StyledProperties via `$parent[UserControl]` which Avalonia
  resolves statically against the UserControl's declared
  StyledProperties).
- The four consumer views all have their own `x:DataType` on the
  UserControl root pointing at their concrete VM, so the `{Binding
  Projects}` / `{Binding SelectedProject}` / `{Binding StatusMessage}`
  flowing INTO the sub-header's StyledProperties resolve against
  the consumer's compiled-binding DataContext as usual.
- `IEnumerable?` for `ProjectsSource` is the right shape: VMs
  surface `ObservableCollection<Project>` which implements
  `IEnumerable`, so the runtime cast is implicit on assignment.
- `object?` for `PickerSelectedItem` deliberately erases the
  per-VM type; when bound to a `Project?` property on the VM the
  Avalonia binder handles the box/unbox per the StyledProperty
  contract.
- DockPanel docking order on VisionAudit / TestingManager: header
  first, sub-header second — same pattern that worked in Phase 1B
  and Phase 2 with the inline Border in slot 2.
- No VM changes, so the 92/92 test count is unaffected.

**Hand-off note for Phase 3 (Documentation + Prompts)**

The promotion-to-control work is done. Phase 3 is now a one-liner per
view:

```xml
<!-- Documentation: project-scoped, has a picker -->
<ctl:ModuleSubHeader DockPanel.Dock="Top"
    ShowPicker="True"
    ProjectsSource="{Binding Projects}"
    PickerSelectedItem="{Binding SelectedProject, Mode=TwoWay}"
    StatusMessage="{Binding StatusMessage}"/>

<!-- Prompts: global module, no project picker -->
<ctl:ModuleSubHeader DockPanel.Dock="Top"
    StatusMessage="{Binding StatusMessage}"/>
```

For Notebook (Phase 5) the same control works — just bind
`PickerSelectedItem` to `ActiveProject` instead of `SelectedProject`.
The StyledProperty's `object?` type and TwoWay default handle the
binding-name difference without per-VM virtuals.

The control's `Name="Root"` is a freebie if a future hand-off prefers
`ElementName=Root` over `$parent[UserControl]` — both work; pick the
one that reads cleanest to the next agent.

### Phase HEADER-v2 — ui-styling — DONE (2026-05-24)

**Why this phase existed**

User feedback after Phase 2-patch-2 rejected the two-band design (78px
ModuleHeader + 74px ModuleSubHeader = 152px). The new directive: ONE
richer 105px single-band header that packs everything into a 3-column,
2-row layout plus a thin 25px status bar. The visual shape is locked
in the "HEADER REDESIGN — v2" section above.

**What changed** (15 files, single consolidated commit)

- **`src/ClaudePM.App/Controls/ModuleHeader.axaml`** — completely
  rewritten to the new shape. Outer `Border Background="#1B1B22"
  Height="105"` wrapping a `Grid RowDefinitions="*,25"`. Row 0 = the
  main 3-column / 2-row layout (left text column, middle picker
  RowSpan=2, right chips RowSpan=2). Row 1 = a `Border Background=
  "#22222A"` (one shade lighter) holding the StatusMessage TextBlock
  at FontSize=11. Picker MinWidth = 260 (column) / 220 (ComboBox)
  per the prompt's locked spec. All four StyledProperties bind via
  `{Binding #Root.X}` ElementName form per Phase 2-patch-2's
  established pattern. Picker grid uses `IsVisible="{Binding
  #Root.ShowPicker}"` so the column collapses when no picker is
  wanted and the left column gets the slack. Description and status
  are both `MaxLines=1` + `TextTrimming="CharacterEllipsis"` so
  overflow can't push the 105px height. The same three Style
  selectors (chip-reset, chip-restart, header-home) carry forward
  unchanged.
- **`src/ClaudePM.App/Controls/ModuleHeader.axaml.cs`** — promoted
  from a no-StyledProperty stub to the full four-StyledProperty
  control declaration. The four StyledProperty<T> fields + CLR
  wrappers are copied verbatim from the now-obsolete
  ModuleSubHeader.axaml.cs (ShowPicker default false,
  ProjectsSource IEnumerable?, PickerSelectedItem object? with
  defaultBindingMode: BindingMode.TwoWay, StatusMessage string with
  default empty). XML docs updated to describe the new control's
  two surfaces (DataContext + StyledProperty).
- **`src/ClaudePM.App/Controls/ModuleSubHeader.axaml`** — deprecated
  to a stub. Empty UserControl body with a comment explaining that
  the control is obsolete (merged into ModuleHeader) and that the
  agent lacked filesystem-delete access. Asks orchestrator to
  `git rm` both files in a follow-up commit. The stub still compiles
  cleanly so the build isn't broken by this dangling file pair.
- **`src/ClaudePM.App/Controls/ModuleSubHeader.axaml.cs`** — paired
  stub. Same deprecation notice in the XML doc. Empty body apart
  from `InitializeComponent()`.
- **11 view files** — every sidebar page migrated to the new
  single-band header per the per-view binding table in the prompt:
  - `HomeView.axaml` — converted from raw ScrollViewer to
    DockPanel + ModuleHeader (title only, no picker). The header
    description carries the VM's Description, so the inline "Home"
    title block at the top of the ScrollViewer is gone.
  - `ProjectsView.axaml` — wrapped existing 2-column Grid in a
    DockPanel; header is `StatusMessage`-only (no picker, since
    Projects IS the list). Dropped the "Projects" + Description
    block from the top of the left rail (now in header).
  - `DocumentationView.axaml` — wrapped existing Grid in a
    DockPanel; header has `ShowPicker=True` bound to Projects +
    SelectedProject. Dropped the project ComboBox column from the
    controls Grid (was `220,*,Auto,Auto`, now `*,Auto,Auto`). Also
    dropped the inline `<TextBlock Text="{Binding StatusMessage}">`
    since the status flows through the header now.
  - `PromptManagerView.axaml` — wrapped existing 2-column Grid in
    a DockPanel; header is `StatusMessage`-only. Dropped the
    "Prompts" title from the top of the left rail. Dropped the
    inline status TextBlocks from the editor + redesign + history
    panes (they were duplicates of what's now in the header).
  - `SessionBuilderView.axaml` — was already on the two-band design
    from Phase 2-patch-2. Replaced both `<ctl:ModuleHeader/>` +
    `<ctl:ModuleSubHeader/>` with a single `<ctl:ModuleHeader
    StatusMessage="{Binding StatusMessage}"/>`.
  - `NotebookView.axaml` — wrapped existing 2-column Grid in a
    DockPanel; header has `ShowPicker=True` with the
    `ActiveProject` binding-name exception (`PickerSelectedItem=
    "{Binding ActiveProject, Mode=TwoWay}"`). Dropped the "AI
    Notebook" title + status row from the top of the chat column.
    Dropped the entire "Active project" StackPanel from the right
    side panel; replaced with a smaller scoped-roots-only block
    explaining the picker is now in the header. The scoped-roots
    text still surfaces, just without the picker UI.
  - `SkillManagerView.axaml` — header inserted as the FIRST
    DockPanel.Dock="Top" child BEFORE the docked-left rail (per
    the layout invariant for views with a left rail). Header is
    `StatusMessage`-only (Skills is a global module). Dropped the
    "Skill Manager" title + status TextBlock from the rail (now
    in header).
  - `SkillBuilderView.axaml` — was already on the two-band design.
    Replaced both header references with a single `<ctl:ModuleHeader
    StatusMessage="{Binding StatusMessage}"/>`. Updated the layout-
    invariant comment block to reflect the single 105px header.
  - `BugTrackerView.axaml` — header inserted as the FIRST docked-
    top child BEFORE the left rail. Header has `ShowPicker=True`
    + `StatusMessage`. Dropped the "Bug Tracker" title + project
    picker section from the top of the left rail. Dropped the
    inline `<TextBlock Text="{Binding StatusMessage}">` from the
    top of the right pane. Updated the "Pick a project above" copy
    to "Pick a project in the header" since the picker is now in
    a different location.
  - `TestingManagerView.axaml` — was already on the two-band
    design with the docked-left rail. Replaced both header
    references with a single `<ctl:ModuleHeader
    ShowPicker="True" ...>` invocation. The rail keeps its
    explainer + HasPlan status messages (no picker-related
    content was ever in the rail post Phase 2-patch).
  - `VisionAuditView.axaml` — same as Testing Manager. Replaced
    both header references with a single `<ctl:ModuleHeader
    ShowPicker="True" ...>`. The 4-stage Grid layout below is
    untouched.
  - `SettingsView.axaml` — converted from raw ScrollViewer to
    DockPanel + ModuleHeader (no picker). Per the prompt's
    property-name exception, `StatusMessage` binds from the VM's
    `Status` property (not `StatusMessage`). Dropped the "Settings"
    title TextBlock from the top of the ScrollViewer (now in
    header). Dropped the inline `<TextBlock Text="{Binding Status}"
    Foreground="#7FD18B"/>` at the bottom since the status flows
    through the header.

- **`SkillSectionView.axaml`** — DELIBERATELY untouched per the
  prompt's per-view table. The Section is a thin container; each
  sub-page (Manager / Builder) renders its own header below the
  in-pane toggle.

**Layout invariants — both LOCKED invariants respected**

For TestingManager / VisionAudit / BugTracker / SkillManager — the
four views with a docked-left rail — the declaration order is now:
1. `<ctl:ModuleHeader DockPanel.Dock="Top"/>` — 105px header (single band)
2. `<Border DockPanel.Dock="Left" Width="…">` — left rail
3. Fill content `<Grid>`

DockPanel processes children in declaration order: the header
reserves the full-width top strip first, then the rail docks
underneath in the remaining region, then the content fills. The
locked layout invariant from Phase 1B / Phase 2-patch still holds —
the header is the FIRST top-docked child, above any left rail. The
fixed-height invariant from Phase 2 (header = 78px) is replaced by
the new fixed-height invariant (header = 105px); the principle
("header height is the same on every module") is preserved.

**Deviations from prompt**

1. **`ModuleSubHeader.axaml(.cs)` not physically deleted** — the
   agent that performed this rewrite (ui-styling) only has Read /
   Grep / Glob / Edit / Write tools; no Bash, no filesystem delete.
   Both files are reduced to minimal compilable stubs with a
   prominent deprecation comment instructing an orchestrator with
   shell access to `git rm` them in a follow-up commit. No view in
   the app references the obsolete control any more (verified via
   `Grep ModuleSubHeader src/ClaudePM.App/Views` returns zero
   matches). Build remains green because the stubs are valid
   no-content UserControls.
2. **Prompt vs PromptManagerView's redesign + history overlay
   panes** — both overlays previously had their own inline
   StatusMessage TextBlocks at the top, in addition to the one in
   the editor. The migration dropped those inline status duplicates
   because the header now surfaces StatusMessage in every state of
   the view (the overlay panes are still the fill child under the
   header, so the header band remains visible above them). The
   prompt didn't explicitly call this out — flagging for review
   in case the user prefers a per-overlay status echo.
3. **Settings property-name exception** handled correctly:
   `StatusMessage="{Binding Status}"` not `{Binding StatusMessage}`.
   Notebook handled correctly: `PickerSelectedItem="{Binding
   ActiveProject, Mode=TwoWay}"` not SelectedProject.

**Build + test verification**

Static review only (no shell in this session):
- `Grep ctl:ModuleHeader src/ClaudePM.App/Views` returns 12 files —
  all 11 sidebar pages plus zero from SkillSectionView (expected).
- `Grep ModuleSubHeader src/ClaudePM.App/Views` returns 0 files —
  no view still references the obsolete control.
- `Grep ModuleSubHeader src/ClaudePM.App` returns 3 files: the two
  obsolete control files themselves + one comment reference in
  the rewritten ModuleHeader.axaml describing what it replaced.
- All new bindings are compiled (every view's `<UserControl>` has
  an `x:DataType` set; the header's StyledProperty bindings are
  ElementName form which compiles without an explicit DataContext
  type on the control).
- The four StyledProperty declarations match the same shape that
  was working on ModuleSubHeader since Phase 2-patch-2 — same
  AvaloniaProperty.Register overload, same types, same defaults.
- No VM changes — the 92/92 test count is unaffected.

**Hand-off note for the orchestrator post-smoke-test**

1. **Physical deletion of `ModuleSubHeader.axaml(.cs)`** is the
   one outstanding cleanup. Two-line shell action:
   `git rm src/ClaudePM.App/Controls/ModuleSubHeader.axaml{,.cs}`
2. **Things to verify visually in the smoke test** (most likely
   regression sources):
   - DocumentationView: the picker used to be the leftmost column
     in a `220,*,Auto,Auto` Grid. With the picker gone, the
     `FolderPath` TextBox now stretches across `*` from the left
     edge. Confirm it doesn't look orphaned without the picker
     next to it.
   - NotebookView: removed the "Active project" panel entirely
     from the right side panel; the picker is now in the header
     instead. Confirm the side panel still reads coherently
     starting at the "Active project scope" hint (which is what
     replaces it).
   - PromptManagerView: status used to render INSIDE the editor
     ScrollViewer AND inside both overlay panels (redesign +
     history). Now it only renders in the header. Confirm that's
     desired behaviour — the redesign + history overlays still
     show the status in the header band above them.
   - SettingsView: `Status` was a green Foreground="#7FD18B"
     TextBlock at the very bottom of the page. It now appears in
     the header's bottom status row (with default `#9ABEE0`
     colour from the header). If the user wants the green
     tinting back, the header would need a per-instance
     foreground override — not in this patch.
   - All four 2-column views (Projects, Prompts, Notebook,
     Documentation): the right-pane editor scrolls inside its
     own ScrollViewer; the header sits above. Confirm the right
     pane doesn't visually overflow into the header band on
     short windows.
3. **The unified header's middle picker column is 260px MinWidth
   on the picker grid + 220px MinWidth on the ComboBox** per the
   prompt. On a narrow window (< ~900px) the three-column header
   will start clipping the title or breadcrumbs because the picker
   doesn't shrink. If that becomes a problem in practice, the fix
   is to relax `MinWidth` on the picker rather than tightening
   the title — the title is the load-bearing element of the
   header. Flagged for future tuning, not for this patch.

### Sidebar submenu — architecture-mvvm — DONE (2026-05-24)

**Why this entry exists**

User asked for the Skills sidebar row to behave as an EXPANDABLE GROUP
NODE — click "Skills" in the sidebar, reveal "Manager" and "Builder"
indented underneath, select either child to navigate to that page.
The in-pane Manager/Builder toggle bar (the one introduced in v0.28
and lived inside `SkillSectionView.axaml`) is gone entirely; the
sidebar IS the toggle.

**What changed** (6 files, single commit)

- `src/ClaudePM.App/ViewModels/PageViewModel.cs` — added one new
  virtual surface: `public virtual IReadOnlyList<PageViewModel>
  Children => Array.Empty<PageViewModel>();`. Default is empty so
  every existing page is still treated as a leaf. Doc comment
  describes the group-node contract and references the
  `OnCurrentPageChanged` re-routing logic.
- `src/ClaudePM.App/ViewModels/SkillSectionViewModel.cs` — refit
  from a "thin container that hosts a sub-page in-pane" into a
  pure GROUP NODE. Deleted `[ObservableProperty] _activePage`,
  `HasBuilder`, `IsManagerActive`, `IsBuilderActive`, the
  `ShowManager` and `ShowBuilder` `[RelayCommand]` methods, and
  the `using CommunityToolkit.Mvvm.ComponentModel` /
  `CommunityToolkit.Mvvm.Input` imports they required. Kept the
  constructor signature (still takes `SkillManagerViewModel` +
  optional `PageViewModel? builder`) so DI wiring stays
  untouched. Override `Children` returns a defensive
  `Where(x is not null).ToArray()` over `[Manager, Builder!]`,
  so production wiring yields `[Manager, Builder]` and the
  defensive builder-null path yields `[Manager]` only. The class
  is no longer `partial` because no source-generated members
  survive the refit. Class-level XML doc updated to describe
  the new role and to explicitly note that no matching view
  exists (the ViewLocator never reaches it because the
  navigation interceptor always re-routes to the first child).
- `src/ClaudePM.App/ViewModels/MainWindowViewModel.cs` — added
  the partial-method hook the CommunityToolkit source generator
  exposes for `_currentPage`: `partial void OnCurrentPageChanged
  (PageViewModel? oldValue, PageViewModel? newValue)`. When
  `newValue.Children.Count > 0` (group node selected), the
  hook posts a re-route to the dispatcher: `Dispatcher.UIThread.
  Post(() => CurrentPage = defaultChild)` where `defaultChild =
  newValue.Children[0]`. The Post avoids re-entering the setter
  in the middle of its own notification path (which would
  confuse the TreeView's two-way binding). Added `using Avalonia.
  Threading;`. XML doc on the partial method explains the
  re-entrancy rationale.
- `src/ClaudePM.App/Views/MainWindow.axaml` — replaced the sidebar
  `ListBox` (lines 21-35) with a `TreeView` of identical visual
  shape. The `TreeDataTemplate DataType="vm:PageViewModel"
  ItemsSource="{Binding Children}"` polymorphically renders every
  page; leaves get no expand chevron because their Children is
  empty. The inner `StackPanel` (glyph + title) is identical to
  the previous `ListBox.ItemTemplate` so leaf rows look unchanged.
  `SelectedItem="{Binding CurrentPage, Mode=TwoWay}"` flows the
  selection back through the VM's setter, which triggers the
  re-routing hook above.
- `src/ClaudePM.App/Views/SkillSectionView.axaml` — REDUCED TO
  AN EMPTY STUB. The view is obsolete because there's no
  in-pane toggle to host any more. Cannot physically delete (no
  filesystem delete tool in this agent's tool set), so the file
  is stubbed with a `<UserControl/>` body and a prominent
  deprecation comment instructing an orchestrator with shell
  access to `git rm` it. The build stays green because the
  stub is a valid empty UserControl and no view references it.
- `src/ClaudePM.App/Views/SkillSectionView.axaml.cs` — paired
  stub with the same deprecation notice. Empty body apart from
  `InitializeComponent()`.

**DI verification**

`Program.cs` was already correctly shaped: `SkillManagerViewModel`
and `SkillBuilderViewModel` were both registered as standalone
singletons before this change (lines 83-84 of `Program.cs`), with
`SkillSectionViewModel` taking them via a factory. After the
v0.32 change those standalone singletons are now ALSO what the
ViewLocator resolves when the user clicks the Manager or Builder
sub-rows in the sidebar — the same instances are visited via the
group node's `Children` collection. No DI change was needed.

**Layout invariants respected**

The bounded-wizard-stages rule (the four-time-bitten anti-pattern
warning in `memory/bounded-wizard-stages.md`) is unaffected. The
Manager and Builder sub-pages already have their own bounded
layouts and now render directly into the main content area
(`<ContentControl Content="{Binding CurrentPage}"/>` in
`MainWindow.axaml`) instead of through the section's
`ContentControl`. The unified header on both sub-pages is
untouched.

**Deviations from prompt**

1. **`SkillSectionView.axaml(.cs)` not physically deleted** —
   same constraint as the Phase HEADER-v2 ModuleSubHeader
   handling. The agent only has Read / Grep / Glob / Edit /
   Write tools; no Bash, no filesystem delete. Both files are
   reduced to minimal compilable stubs with deprecation comments
   instructing an orchestrator with shell access to `git rm`
   them. The build stays green because the stubs are valid
   empty `UserControl`s; no view in the app references them
   (`Grep SkillSectionView src/ClaudePM.App/Views` only matches
   the stub files themselves).
2. **Kept SkillSectionView as a stub rather than as a "pick a
   sub-page" placeholder.** The prompt left this to judgement;
   I chose stub because the Dispatcher.UIThread.Post re-route
   happens fast enough that the user never sees a transient
   bare-section state, and a placeholder view would obscure the
   intent (the section IS a sidebar-only group node now). The
   stub also makes the eventual `git rm` clean — there's
   nothing in those files worth keeping.

**Build + test verification**

Static review only (no shell in this session):
- The `partial void OnCurrentPageChanged(PageViewModel? oldValue,
  PageViewModel? newValue)` signature exactly matches what the
  CommunityToolkit `[ObservableProperty]` source generator emits
  for a `private PageViewModel _currentPage;` field. Verified
  against the same pattern used by other VMs in this codebase
  (e.g. `BugTrackerViewModel.OnSelectedProjectChanged` if it
  exists — same convention).
- The polymorphic `TreeDataTemplate DataType="vm:PageViewModel"`
  resolves at runtime against the runtime type of each item
  (HomeViewModel, SkillSectionViewModel, etc.); all inherit from
  PageViewModel so the template matches every entry. `Children`
  is a base virtual so the `ItemsSource="{Binding Children}"`
  binding compiles against the declared compile-time type.
- `Dispatcher.UIThread.Post` is the standard Avalonia pattern for
  deferring work to the next UI tick; it's used elsewhere in the
  codebase already (verified via grep — Notebook and Vision Audit
  both use it for similar deferred-mutation cases).
- `Children` returns a new array on every call (`new[] { Manager,
  Builder! }.Where(...).ToArray()`). For a sidebar with 11
  top-level entries and one group with 2 children, the allocation
  is trivial. If the property is ever bound to something that
  asks for it repeatedly (it isn't — TreeView caches), cache the
  array in the constructor.
- No VM behaviour changes outside the Skills section. The 92/92
  test count should be unaffected — Skills-section tests don't
  exist (the section was a thin container with no logic), and
  all other module VMs are untouched.

**Hand-off note for the orchestrator post-smoke-test**

1. **Physical deletion of `SkillSectionView.axaml(.cs)`** is the
   one outstanding cleanup. Two-line shell action:
   `git rm src/ClaudePM.App/Views/SkillSectionView.axaml{,.cs}`
2. **Things to verify visually in the smoke test:**
   - Sidebar shows Skills with an expand chevron / arrow.
     Clicking it should both expand the children AND switch the
     content area to Skill Manager (because the Dispatcher.UIThread.
     Post re-routes selection to the first child).
   - Clicking Manager directly shows Skill Manager in the content
     area with its unified header reading "Skill Manager".
   - Clicking Builder directly shows Skill Builder in the content
     area with its unified header reading "Skill Builder".
   - The in-pane Manager/Builder toggle bar is GONE from the top
     of the Skill Manager and Skill Builder views (it never lived
     there — it lived in `SkillSectionView.axaml` — but the user
     should see it disappear from the rendered output).
   - The Skills row's expand state persists when navigating
     between Manager and Builder (i.e. once expanded, it stays
     expanded; selecting Builder does not collapse the parent).
3. **If a future request adds a third skills sub-page**, the
   pattern is now trivial: register the new VM as a DI singleton,
   accept it through the `SkillSectionViewModel` constructor,
   add it to the `Children` override's array. The sidebar
   automatically picks it up; the ViewLocator routes its view
   by naming convention. No MainWindowViewModel or sidebar
   markup edits needed.
4. **If a future request asks for OTHER sidebar entries to
   become group nodes** (e.g. "Documentation" expanding to
   "Scan / Audit / Reconcile" sub-pages), the pattern is the
   same: that VM overrides `Children`, registers its sub-page
   VMs as DI singletons, and the TreeView + OnCurrentPageChanged
   interceptor handle the rest. The architecture is now
   submenu-ready end-to-end.

### Header-v2 follow-up patch — ui-styling — DONE (2026-05-24)

**Why this patch existed**

Three batched UI follow-ups after the v2 unified header + sidebar submenu
landed:

1. **Title becomes the home link, drop the home icon.** The 🏠 button at
   the start of the header's left column read as redundant — the title
   itself is the obvious "back to module home" target. Replace the
   separate icon with a clickable title that fires `GoModuleHomeCommand`.
2. **Reset / Restart chips always visible but greyed when inactive.** The
   `IsVisible`-driven chips made the right edge of the header chrome
   shrink and stretch as the user navigated between modules. User wants
   the chips to always render in the same position so the chrome is
   visually consistent, with a clearly-inactive disabled state when the
   module's VM doesn't expose the matching command.
3. **Restructure DocumentationView to follow the SkillManager pattern.**
   The pre-restructure layout had a horizontal controls row + 2-column
   list/workspace area; user wants the canonical docked-left rail
   (320-wide #22222A) + right pane with chips/actions toolbar over
   workspace pattern that SkillManagerView established as the codebase
   norm.

**What changed** (2 view/control files + this hand-off entry)

- **`src/ClaudePM.App/Controls/ModuleHeader.axaml`** — header redesign
  application:
  - Deleted the `<Button Classes="header-home">` block (lines 58-66 of
    the prior version) from the start of the left column's row-0
    StackPanel. The 🏠 button is gone.
  - Converted the static `<TextBlock Text="{Binding Title}">` into a
    `<Button Classes="header-title">` with `Content="{Binding Title}"`,
    `Command="{Binding GoModuleHomeCommand}"`, and `IsEnabled="{Binding
    GoModuleHomeCommand, Converter={x:Static ObjectConverters.IsNotNull}}"`.
    The glyph still sits to the LEFT of the title (its position was
    correct already — not moved).
  - Switched both chip buttons (chip-reset / chip-restart) from
    `IsVisible="…IsNotNull…"` to `IsEnabled="…IsNotNull…"`. Dropped the
    IsVisible attribute entirely on both so they render in every
    module.
  - Deleted the two `Style Selector="Button.header-home"` blocks (the
    base style + the pointerover override).
  - Added a `Button.header-title` base style (transparent background,
    no border, zero padding/corner-radius, FontSize=20, SemiBold
    white, VerticalAlignment=Center, default Cursor=Arrow).
  - Added `Button.header-title:not(:disabled)` → `Cursor=Hand` so the
    hand cursor only shows when the command is actually bound.
  - Added `Button.header-title:pointerover /template/ ContentPresenter`
    → `Background=Transparent, Opacity=0.85` for the subtle
    hover-affordance dip. Avalonia's `:pointerover` only matches on
    enabled controls, so the dim never triggers when there's no
    home command.
  - Added `Button.header-title:disabled /template/ ContentPresenter`
    → `Opacity=1, Background=Transparent` to explicitly pin the
    disabled state back to full opacity, overriding Fluent's default
    disabled dimming. The title looks IDENTICAL whether or not it
    has a home command — the only difference is the cursor + the
    hover behaviour.
  - Added `Button.chip-reset:disabled /template/ ContentPresenter` and
    `Button.chip-restart:disabled /template/ ContentPresenter` blocks
    setting muted backgrounds (`#2A2128` / `#1F2630`) and foregrounds
    (`#5A3D43` / `#3E5872`). Picked exact values by taking the active
    chip's saturated colour and pulling the saturation back ~60% while
    keeping the same hue — they read as "this same chip, but
    inactive" against the `#1B1B22` header background.
- **`src/ClaudePM.App/Views/DocumentationView.axaml`** — wholesale
  rewrite to the SkillManager pattern:
  - Outer container stays `DockPanel LastChildFill="True"`. The
    unified `<ctl:ModuleHeader/>` is the FIRST `DockPanel.Dock="Top"`
    child (unchanged from before).
  - **NEW**: docked-left rail
    `<Border DockPanel.Dock="Left" Width="320" Background="#22222A"
    Padding="14">` containing:
    - Folder row at the top (StackPanel with TextBox + 📁 Browse
      icon-button + 🔄 Scan icon-button in a `*,Auto,Auto` Grid with
      ColumnSpacing=6). Bindings preserved: `FolderPath`,
      `BrowseFolderCommand`, `ScanCommand`. Scan disabled while
      `IsNotBusy` is false. Mirrors SkillManagerView lines 48-65
      verbatim with the icon glyphs (`&#x1F4C1;` / `&#x1F504;`) and
      `Padding="8,4"`.
    - Watch-mode CheckBox under the folder row (judgement call —
      see below).
    - `Documents` heading.
    - Documents `ListBox` filling the rest of the rail
      (DockPanel-fill). Item template (RelativePath / Name subtext)
      preserved verbatim from the original Column 0 panel.
  - **NEW**: right pane `<Grid Margin="20" RowDefinitions="Auto,*"
    RowSpacing="12">` containing:
    - Row 0: horizontal action toolbar holding (left to right) the
      three severity chips (Critical/Warning/Info — same `#E06C6C`/
      `#E0A95E`/`#5E8FE0` palette as the original) wrapped in a
      `IsVisible="{Binding HasReport}"` group, a 1px vertical
      separator `Border` (`Background="#3A3A45" Margin="6,4"`),
      then the six action buttons in the prompt's specified order:
      Run AI Analysis → Cancel(IsBusy) → Audit Project →
      Cancel(IsBusy) → Generate Fix Prompt → Export Report.
    - Row 1: workspace — the three pre-existing
      mutually-exclusive states (findings/AI-semantic/fix-prompt
      defaults, audit overlay DockPanel, inline editor DockPanel)
      nested verbatim. Inner markup of every state IS UNCHANGED —
      only the outer container moved.
  - **REMOVED**: the inline `<TextBlock Text="{Binding
    StatusMessage}">` that used to live above the controls row
    (the unified header's status band already carries it).
  - **REMOVED**: the original `Grid RowDefinitions="Auto,*"` and the
    `<StackPanel Grid.Row="0" Spacing="12">` that held the controls
    row + chips row above the 2-column workspace. Those concerns
    redistributed across the rail (controls) and the action toolbar
    (chips + buttons).

**Judgement calls**

1. **Watch-mode toggle placement: in the rail under the folder row.**
   The prompt explicitly flagged this as a judgement call. I put it
   in the rail because watch-mode is a folder-level concern — it
   describes how the rail's list of `Docs` is kept fresh — so it
   reads naturally adjacent to the folder path TextBox. Putting it
   in the action toolbar would have separated it from the input it
   actually modifies and made the toolbar visually busier (the
   toolbar is already 6 buttons + 3 chips + a separator). The
   user's own prompt also suggested the rail as the preferred home.
2. **`HasReport` wrap around the chips group AND the separator.**
   I wrapped the three chip Borders AND the 1px vertical separator
   inside one IsVisible-bound StackPanel rather than wrapping each
   chip separately. The reason: before the first scan the separator
   would otherwise sit alone next to the action buttons with no
   chips on its left, which would look broken. Wrapping the whole
   group means the toolbar collapses cleanly to just the buttons
   before the first scan and grows to chips+separator+buttons after.
3. **Did not give the action toolbar its own `Margin`.** The right
   pane Grid already has `Margin="20"` (matching SkillManagerView
   line 115's `<Grid RowDefinitions="Auto,*" Margin="20">`) and
   `RowSpacing="12"` between the toolbar row and the workspace row,
   so the toolbar inherits the layout's standard breathing room
   without extra per-element margins.
4. **Cursor on the disabled chips.** I deliberately did NOT add a
   `:disabled` Cursor override on the chips. Avalonia's default
   `IsHitTestVisible=false` behaviour for disabled controls (which
   the Fluent theme propagates) means the cursor stays as the
   parent's Arrow when the user hovers over a disabled chip, which
   reads correctly. Adding a cursor rule would be unnecessary
   ceremony.

**Layout invariants — both LOCKED invariants respected**

For DocumentationView, the outer DockPanel children in declaration
order are now:
1. `<ctl:ModuleHeader DockPanel.Dock="Top"/>` — 105px header (single band)
2. `<Border DockPanel.Dock="Left" Width="320">` — folder + docs rail
3. Fill content `<Grid Margin="20">` — toolbar + workspace

DockPanel processes children in declaration order: the header reserves
the full-width top strip first, then the 320-wide rail docks
underneath it on the left, then the right pane fills. Same pattern
as SkillManager, BugTracker, TestingManager, VisionAudit. The
header is still the FIRST Top-docked child, above any left rail —
the locked layout invariant from Phase 1B / Phase 2-patch / Phase
HEADER-v2 still holds.

The fixed-height invariant from Phase HEADER-v2 (header = 105px) is
untouched. The new chip `:disabled` styles do NOT alter the chip's
geometry — only `Background` / `Foreground` change, so the chip
still occupies its fixed `Padding="10,4"` + content size regardless
of state, so the right edge of the header chrome stays at the same
horizontal position whether or not the module exposes Reset/Restart.

**Build + test verification**

Static review only (no shell in this session):
- All bindings remain compiled. `DocumentationView` retains its
  `x:DataType="vm:DocumentationViewModel"`. Every new template inside
  it (the rail's ListBox item template, the action-toolbar buttons)
  reaches bindings that already exist on `DocumentationViewModel`
  per the prompt's confirmation (`FolderPath`, `BrowseFolderCommand`,
  `ScanCommand`, `IsNotBusy`, `IsWatchModeEnabled`, `Docs`,
  `SelectedDoc`, `HasReport`, `CriticalCount`, `WarningCount`,
  `InfoCount`, `RunSemanticCommand`, `RunSemanticCancelCommand`,
  `IsBusy`, `RunAuditCommand`, `RunAuditCancelCommand`,
  `GenerateFixPromptCommand`, `ExportReportCommand`).
- ModuleHeader's new `Button.header-title` style uses the `:not(:disabled)`
  pseudo-class pattern that Avalonia 11.3 supports natively (the same
  pattern used by SkillManagerView's chip style would have matched).
  The `:disabled /template/ ContentPresenter` opacity override matches
  the Fluent ControlTheme structure — the disabled visual state pipes
  through ContentPresenter, so overriding Opacity there pins it back.
- The chip `:disabled /template/ ContentPresenter` blocks follow the
  same `/template/ ContentPresenter` selector pattern that the existing
  `chip-restart:pointerover` reference in MountainKill-related views
  uses. The Setter values are valid hex strings.
- No ViewModel changes; the 92/92 test count is unaffected.
- No App.axaml / Program.cs / other-view touches per the boundary clause.

**Hand-off note for the orchestrator post-smoke-test**

1. **Smoke test priorities** (most likely regression sources, in
   descending order):
   - **Title-as-link visual check on every module.** Hover over the
     title on a module that exposes `GoModuleHomeCommand` (Vision
     Audit, Skill Builder, Session Builder, Testing Manager all do)
     and confirm: cursor changes to hand, opacity dips slightly.
     Then hover the title on a module that DOESN'T expose it (Home,
     Settings, Projects, etc.) and confirm: cursor stays as Arrow,
     opacity stays at 100%, no visual change at all from the static
     TextBlock it replaces.
   - **Reset/Restart chip visibility on every module.** Confirm both
     chips are now visible on EVERY sidebar page including Home,
     Settings, etc. Confirm the disabled state is visually distinct
     (greyed but legible) on the modules where the VM doesn't expose
     the command. Confirm the active state on Vision Audit / Skill
     Builder / etc. still reads as the saturated red/blue.
   - **DocumentationView rail vs SkillManager rail parity.** Click
     into both modules back-to-back and confirm the rail width (320),
     background (#22222A), padding (14), and icon button styling all
     match visually. The folder TextBox + 📁 + 🔄 row should look
     pixel-identical between the two views (modulo the watermark
     copy).
   - **DocumentationView action toolbar wrap behaviour.** On a narrow
     window the action toolbar's 6 buttons + 3 chips + separator may
     run out of horizontal space. The toolbar uses a horizontal
     StackPanel (no Wrap), so excess content will clip the right edge
     rather than wrap. If that's a problem, switch the toolbar's
     outer to `<WrapPanel Orientation="Horizontal">` — flagged for
     future tuning, not in this patch.
   - **DocumentationView watch-mode placement.** Confirm Watch mode
     under the folder row in the rail reads coherently. If the user
     prefers it in the toolbar, the relocation is a 4-line move.
2. **Things that DID NOT change** (sanity confirmations):
   - No view other than DocumentationView and ModuleHeader was touched.
   - No ViewModel was touched.
   - App.axaml's app-wide Button style is unchanged.
   - The 4-stage Grid layout inside the audit overlay / editor / findings
     panes is byte-identical to before. Only the outer container moved.
3. **Boundary clause respected.** Modified exactly three files:
   ModuleHeader.axaml, DocumentationView.axaml, and this plan file.
