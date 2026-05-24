# Testing Manager — Wizard Design Pattern Options

> **Why this doc exists.** The Testing Manager questionnaire is a 5-question
> form that can render *taller than the visible window*. The first v0.27
> implementation rendered it as a long ScrollViewer-wrapped vertical stack
> and hit a persistent scrolling bug — content past the visible viewport was
> unreachable, even after multiple structural fixes (StackPanel → Grid,
> outer Grid → DockPanel, explicit `VerticalScrollBarVisibility="Auto"`).
> This is the same family of bug as the v0.24 Skill Library
> Resources/Validation cut-off saga that consumed nine layout iterations
> before the module was deleted in v0.25.
>
> Rather than re-fight the same battle, this doc captures three
> *architecturally distinct* approaches that each break the dependency on
> scrolling working correctly. If the chosen approach fails, the others are
> here ready to substitute — no re-discovery needed.

## The underlying bug, briefly

Per [Avalonia issue #2701](https://github.com/AvaloniaUI/Avalonia/issues/2701)
and [#3772](https://github.com/AvaloniaUI/Avalonia/issues/3772), and
confirmed in the Avalonia docs: **a ScrollViewer must be in a container
that gives it a constrained height in the scrolling direction.** When the
parent gives infinite height (e.g. a Grid cell with no explicit
`RowDefinitions`, propagating through the measure pass), the ScrollViewer's
content is measured at its full natural height, `ExtentHeight ==
ViewportHeight`, no overflow is detected, and no scrollbar appears.

We applied two fixes that *should* have resolved this — switching the inner
`StackPanel` to a `Grid` with explicit `RowDefinitions="Auto,..."`, and
wrapping the outer layout in a `DockPanel LastChildFill="True"` so the
ScrollViewer is the fill-child of a bounded container. Both fixes were
correct per the documented Avalonia patterns. Neither resolved the
user-reported symptom. The bug is *latent in the codebase* (BugTracker has
the same outer shape and the same potential failure), and the only
guaranteed-correct path forward is to **stop relying on the ScrollViewer
to handle this content height**.

These three patterns each do that, in different ways.

---

## Pattern A — One question per step (stepped wizard)

**The build-prompt called for "a stepped questionnaire state, closer to the
Session Builder's wizard layout than to a list"** — and we rendered it as a
list. Pattern A matches the spec literally.

### Shape

- VM gains a `CurrentStepIndex` integer (0–4).
- View shows ONE question at a time. The other four are hidden via
  `IsVisible` toggles bound to the index.
- Back / Next buttons at the bottom advance / reverse the index.
- A small progress strip at the top (5 dots, one per step) shows where the
  user is and which steps are complete.
- After step 5 (or 6, with a Review/Recommendation step), the recommendation
  panel takes the right pane.

### VM sketch (CommunityToolkit.Mvvm)

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsStep1Visible))]
[NotifyPropertyChangedFor(nameof(IsStep2Visible))]
[NotifyPropertyChangedFor(nameof(IsStep3Visible))]
[NotifyPropertyChangedFor(nameof(IsStep4Visible))]
[NotifyPropertyChangedFor(nameof(IsStep5Visible))]
[NotifyPropertyChangedFor(nameof(IsFirstStep))]
[NotifyPropertyChangedFor(nameof(IsLastStep))]
[NotifyPropertyChangedFor(nameof(StepLabel))]
private int _currentStepIndex;

public bool IsStep1Visible => CurrentStepIndex == 0;
public bool IsStep2Visible => CurrentStepIndex == 1;
// ...
public bool IsFirstStep => CurrentStepIndex == 0;
public bool IsLastStep => CurrentStepIndex == 4;
public string StepLabel => $"Step {CurrentStepIndex + 1} of 5";

[RelayCommand] private void GoBack() { if (CurrentStepIndex > 0) CurrentStepIndex--; }
[RelayCommand] private void GoNext() { if (CurrentStepIndex < 4) CurrentStepIndex++; }
```

The existing five `AnswerXxx` properties stay (one per question). The Pick
commands stay. Nothing else changes about the recommendation logic.

### View sketch

```xml
<DockPanel LastChildFill="True">
    <Border DockPanel.Dock="Left" Width="300">...left rail...</Border>

    <Grid Margin="28" RowDefinitions="Auto,Auto,*,Auto" RowSpacing="20">
        <!-- Progress strip (5 dots) -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="8">
            <Ellipse Width="12" Height="12" Fill="{Binding ..., one-way bool→brush}"/>
            <Ellipse Width="12" Height="12" .../>
            <Ellipse Width="12" Height="12" .../>
            <Ellipse Width="12" Height="12" .../>
            <Ellipse Width="12" Height="12" .../>
        </StackPanel>

        <!-- Step label "Step N of 5" -->
        <TextBlock Grid.Row="1" Text="{Binding StepLabel}" Opacity="0.6" FontSize="11"/>

        <!-- Question + radios (only the active step is visible) -->
        <Grid Grid.Row="2">
            <StackPanel IsVisible="{Binding IsStep1Visible}">
                <TextBlock Text="1. What are you building?" FontSize="20"/>
                <RadioButton .../><RadioButton .../>...
            </StackPanel>
            <StackPanel IsVisible="{Binding IsStep2Visible}">...Q2...</StackPanel>
            ... (3 more) ...
        </Grid>

        <!-- Back / Next -->
        <Grid Grid.Row="3" ColumnDefinitions="Auto,*,Auto">
            <Button Grid.Column="0" Content="← Back"
                    Command="{Binding GoBackCommand}"
                    IsEnabled="{Binding !IsFirstStep}"/>
            <Button Grid.Column="2" Content="Next →"
                    Command="{Binding GoNextCommand}"
                    IsVisible="{Binding !IsLastStep}"/>
            <Button Grid.Column="2" Content="See recommendation"
                    Command="{Binding SeeRecommendationCommand}"
                    IsVisible="{Binding IsLastStep}"/>
        </Grid>
    </Grid>
</DockPanel>
```

### Pros

- Matches the build-prompt's literal "stepped questionnaire" intent.
- **No ScrollViewer needed for the questionnaire** — each step fits easily
  in viewport. Bug eliminated by removing the affordance, not by fighting
  the layout.
- Accessible by default — one focus area at a time, single decision per
  screen.
- Re-running the questionnaire feels natural: Back walks the user through
  previous answers; the Pick commands let them adjust a single answer
  without redoing the whole flow.
- The progress strip gives clear feedback about position.

### Cons

- More VM state (CurrentStepIndex + 5 derived `IsStepNVisible` bools).
- More View XAML duplication — five near-identical StackPanel blocks (one
  per step), each with its own RadioButtons.
- Adding a sixth question means editing both VM and View (no data
  abstraction).
- The progress strip with 5 ellipses + colour-changing brushes wants a
  converter or per-dot bools.

### When to revisit

If the current implementation (Pattern C) feels over-engineered for the
fixed 5-question case, Pattern A is the simpler stepped alternative.
Switching is mechanical: collapse Steps[] back into individual fields and
turn each step's DataTemplate body into an explicit `IsVisible`-controlled
StackPanel.

---

## Pattern B — Two-column compact form

Render each question as **horizontal radios** instead of a vertical stack.
Total questionnaire height drops from ~900px to ~250px, which fits in any
reasonable viewport without scrolling.

### Shape

- All 5 questions visible at once on a single screen.
- Each question renders as: question label on the left, radios stacked
  horizontally on the right.
- The recommendation panel appears below the 5 questions, still on the
  same screen — fits in the remaining 700+px without needing scroll.

### View sketch

```xml
<DockPanel LastChildFill="True">
    <Border DockPanel.Dock="Left" Width="300">...left rail...</Border>
    <ScrollViewer Padding="28"> <!-- safety net only -->
        <Grid MaxWidth="1100" RowSpacing="14">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/> <!-- intro -->
                <RowDefinition Height="Auto"/> <!-- Q1 -->
                <RowDefinition Height="Auto"/> <!-- Q2 -->
                <RowDefinition Height="Auto"/> <!-- Q3 -->
                <RowDefinition Height="Auto"/> <!-- Q4 -->
                <RowDefinition Height="Auto"/> <!-- Q5 -->
                <RowDefinition Height="Auto"/> <!-- rec panel -->
            </Grid.RowDefinitions>

            <TextBlock Grid.Row="0" Text="Pick a testing strategy" FontSize="20"/>

            <!-- Q1 row: label-left, radios-right -->
            <Grid Grid.Row="1" ColumnDefinitions="200,*">
                <TextBlock Grid.Column="0" Text="What are you building?"
                           FontWeight="SemiBold" VerticalAlignment="Center"/>
                <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6">
                    <RadioButton Content="Library"/>
                    <RadioButton Content="Desktop"/>
                    <RadioButton Content="Web"/>
                    <RadioButton Content="CLI"/>
                    <RadioButton Content="Mixed"/>
                </StackPanel>
            </Grid>

            <!-- ... Q2-Q5 same shape ... -->

            <Border Grid.Row="6">...recommendation...</Border>
        </Grid>
    </ScrollViewer>
</DockPanel>
```

### Pros

- **Simplest implementation** — purely a View change, VM unchanged.
- Single-page experience: user sees the whole form at once, can compare
  answers visually.
- The recommendation appears below the questions; the user sees Q5 +
  recommendation together.
- Bug avoided by making the content *fit*, so even if the ScrollViewer is
  broken, the content is visible.

### Cons

- Less "stepped" than the spec wanted.
- Long option labels (Q5's "Heavily — databases, APIs, file system, network
  everywhere") wrap awkwardly when horizontal.
- Less coached — no one-decision-at-a-time framing.
- Visual density is high; less breathable than the original.
- If a future question has many long options (say 6 options each
  needing a sentence of context), Pattern B breaks down.
- Still has a ScrollViewer as a fallback — doesn't *prove* the scroll bug
  is gone, just papers over it for this content.

### When to revisit

Best for a simple settings-style screen where you've outgrown
one-question-at-a-time but the form is still bounded. If we add more
questions later and the page starts overflowing again, Pattern B becomes
the wrong call.

---

## Pattern C — Data-driven stepped wizard (ItemsControl + per-step DataTemplate)

The most extensible variant of Pattern A. Instead of hardcoding 5 step
blocks in XAML, the VM exposes a `Steps : ObservableCollection<QuestionViewModel>`
collection, and the View renders the *current* step via a `ContentControl`
bound to `CurrentQuestion` with a `DataTemplate` for `QuestionViewModel`.

### Shape

- A `QuestionViewModel` per question (Title + Options + SelectedToken).
- A `QuestionOption` per radio choice (Token + Label + IsSelected).
- Parent VM exposes `Steps`, `CurrentStepIndex`, `CurrentQuestion`,
  `GoBack` / `GoNext` / `SeeRecommendation` commands.
- View renders progress strip via an `ItemsControl` over `Steps` (one dot
  per step, colour-bound to `IsAnswered`).
- View renders the current question via a `ContentControl` with one
  `DataTemplate` for `QuestionViewModel`. RadioButtons come from an inner
  `ItemsControl` over `Options`.
- Back / Next row at the bottom.

### VM sketch

```csharp
public sealed partial class QuestionOption : ObservableObject
{
    public string Token { get; }
    public string Label { get; }
    [ObservableProperty] private bool _isSelected;
    public QuestionOption(string token, string label) { Token = token; Label = label; }
}

public sealed partial class QuestionViewModel : ObservableObject
{
    public string Title { get; }
    public IReadOnlyList<QuestionOption> Options { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnswered))]
    private string _selectedToken = "";

    public bool IsAnswered => !string.IsNullOrEmpty(SelectedToken);

    public QuestionViewModel(string title, IReadOnlyList<QuestionOption> options,
                             string initialToken = "")
    {
        Title = title; Options = options; _selectedToken = initialToken;
        SyncOptionFlags();
    }

    [RelayCommand]
    private void Pick(string? token)
    {
        if (token is null) return;
        SelectedToken = token;
        SyncOptionFlags();
    }

    private void SyncOptionFlags()
    {
        foreach (var o in Options) o.IsSelected = o.Token == SelectedToken;
    }
}
```

Parent VM:

```csharp
public ObservableCollection<QuestionViewModel> Steps { get; } = new();

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CurrentQuestion))]
[NotifyPropertyChangedFor(nameof(IsFirstStep))]
[NotifyPropertyChangedFor(nameof(IsLastStep))]
[NotifyPropertyChangedFor(nameof(StepLabel))]
private int _currentStepIndex;

public QuestionViewModel? CurrentQuestion => InBounds(CurrentStepIndex) ? Steps[CurrentStepIndex] : null;
public bool IsFirstStep => CurrentStepIndex <= 0;
public bool IsLastStep  => CurrentStepIndex >= Steps.Count - 1;
public string StepLabel => $"Step {CurrentStepIndex + 1} of {Steps.Count}";

[RelayCommand] private void GoBack() { if (!IsFirstStep) CurrentStepIndex--; }
[RelayCommand] private void GoNext() { if (!IsLastStep) CurrentStepIndex++; }
[RelayCommand] private void SeeRecommendation() { /* triggers showing the rec panel */ }
```

Step construction (ctor):

```csharp
Steps.Add(new QuestionViewModel("1. What are you building?", new[]
{
    new QuestionOption("Library", "Library or API"),
    new QuestionOption("Desktop", "Desktop application"),
    new QuestionOption("WebFrontend", "Web frontend"),
    new QuestionOption("CLI", "Command-line tool"),
    new QuestionOption("Mixed", "Mixed / not sure"),
}));
// ... 4 more ...
```

`BuildAnswers()` reads from `Steps[i].SelectedToken` instead of individual
fields.

### View sketch

```xml
<DockPanel LastChildFill="True">
    <Border DockPanel.Dock="Left" Width="300">...left rail...</Border>

    <Grid Margin="28" RowDefinitions="Auto,Auto,*,Auto" RowSpacing="16"
          IsVisible="{Binding IsQuestionnaireVisible}">

        <!-- Progress strip -->
        <ItemsControl Grid.Row="0" ItemsSource="{Binding Steps}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <StackPanel Orientation="Horizontal" Spacing="10"/>
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="vm:QuestionViewModel">
                    <Ellipse Width="12" Height="12"
                             Fill="{Binding IsAnswered,
                                Converter={x:Static conv:BoolToBrush},
                                ConverterParameter='#9ABEE0|#444'}"/>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <TextBlock Grid.Row="1" Text="{Binding StepLabel}" Opacity="0.6" FontSize="11"/>

        <!-- Current question + options -->
        <ContentControl Grid.Row="2" Content="{Binding CurrentQuestion}">
            <ContentControl.ContentTemplate>
                <DataTemplate x:DataType="vm:QuestionViewModel">
                    <StackPanel Spacing="12">
                        <TextBlock Text="{Binding Title}" FontSize="20" FontWeight="SemiBold"/>
                        <ItemsControl ItemsSource="{Binding Options}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate x:DataType="vm:QuestionOption">
                                    <RadioButton Content="{Binding Label}"
                                                 IsChecked="{Binding IsSelected, Mode=OneWay}"
                                                 Command="{Binding $parent[ItemsControl].
                                                    ((vm:QuestionViewModel)DataContext).PickCommand}"
                                                 CommandParameter="{Binding Token}"/>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </DataTemplate>
            </ContentControl.ContentTemplate>
        </ContentControl>

        <!-- Back / Next / See-recommendation -->
        <Grid Grid.Row="3" ColumnDefinitions="Auto,*,Auto">
            <Button Grid.Column="0" Content="← Back"
                    Command="{Binding GoBackCommand}"
                    IsEnabled="{Binding !IsFirstStep}"/>
            <Button Grid.Column="2" Content="Next →"
                    Command="{Binding GoNextCommand}"
                    IsVisible="{Binding !IsLastStep}"
                    IsEnabled="{Binding CurrentQuestion.IsAnswered}"/>
            <Button Grid.Column="2" Content="See recommendation →"
                    Command="{Binding SeeRecommendationCommand}"
                    IsVisible="{Binding IsLastStep}"
                    IsEnabled="{Binding IsAnswersComplete}"/>
        </Grid>
    </Grid>

    <!-- Recommendation review state -->
    <Grid Margin="28" IsVisible="{Binding IsRecommendationVisible}">...</Grid>

    <!-- Plan view state -->
    <ScrollViewer Padding="28" IsVisible="{Binding HasPlanViewVisible}">...</ScrollViewer>
</DockPanel>
```

### Pros

- **Most extensible.** Adding a 6th question is `Steps.Add(new
  QuestionViewModel(...))` — one line, no View edit.
- The wizard chrome (progress strip + Back/Next + ContentControl swap) is
  reusable across future wizards (Session Builder, the planned
  v2 Skill Library, anything else multi-step).
- Each `QuestionViewModel` is independently unit-testable.
- Cleanest separation of concerns per `avalonia-mvvm-patterns` guidance —
  data drives view, no hardcoded per-question XAML.
- No ScrollViewer needed in the questionnaire — same bug-elimination as
  Pattern A.
- Matches the `multi-step-wizard-builder` skill's "Linear wizard" pattern
  almost exactly.

### Cons

- Heaviest refactor — two new VM types (`QuestionViewModel`,
  `QuestionOption`), a `Bool → Brush` converter for the progress dots,
  and the View restructured around `ContentControl + DataTemplate`.
- For a fixed 5-question form, the data-driven indirection is arguably
  overkill versus Pattern A.
- The `ConverterParameter` for the progress dot brushes needs a converter
  that takes a literal pipe-separated brush spec (or a more conventional
  named-resource lookup) — small extra moving part.

### When to revisit

Pattern C is the right call if other modules will get wizards too (Session
Builder, the Skill Library rewrite, an Onboarding flow). The reusable
chrome pays itself back the second time it's used. For a one-off 5-question
form that won't grow, Pattern A is leaner.

---

## How to revert / switch patterns

If Pattern C doesn't resolve the scrolling symptom (extremely unlikely, but
the v0.24 Skill Library saga is a cautionary tale), the switch path is:

1. **C → A**: Keep `Steps[]` data structure for storage; replace the View's
   `ContentControl + DataTemplate` with five hardcoded `StackPanel`s, each
   visibility-bound to `CurrentStepIndex == N`. Lose the data-driven
   chrome, keep the stepped UX.
2. **A → B**: Drop the stepped index entirely. Render all 5 questions on
   one screen, each as a label-left/radios-right `Grid`. Drop Back/Next
   buttons (form is complete when all are picked).
3. **B → A or C**: If the all-on-one-page form starts overflowing the
   viewport again, escalate back to a stepped pattern.

Each transition is a one-session refactor. The CHANGELOG entry that
introduced the current pattern should note which alternatives were
considered, so future agents know the choice was deliberate.

---

## Sources consulted

- [Avalonia ScrollViewer docs](https://docs.avaloniaui.net/docs/reference/controls/scrollviewer)
- [Avalonia issue #2701 — ScrollViewer inside Grid column has inconsistent scrollbar visibility](https://github.com/AvaloniaUI/Avalonia/issues/2701)
- [Avalonia issue #3772 — ScrollViewer incorrectly calculates ViewPort when measured with infinite size](https://github.com/AvaloniaUI/Avalonia/issues/3772)
- Skill `anthropic-skills:avalonia-mvvm-patterns` (compiled bindings, VM-no-Views rule, footguns)
- Skill `anthropic-skills:avalonia-mvvm-app-scaffold` (CommunityToolkit.Mvvm patterns)
- Skill `anthropic-skills:csharp-mastery` (general .NET architecture)
- Skill `multi-step-wizard-builder` from `~/Development/Skills/` (linear-wizard pattern, progress bar, navigation)
- Existing in-codebase reference: `NotebookView.axaml` (DockPanel + LastChildFill pattern), `SettingsView.axaml` (ScrollViewer-as-UserControl-content pattern)
