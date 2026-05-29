# Stratum — Design System Handoff for vybedesk

A design system for **vybedesk**, a desktop Claude project-management app built in **C# / Avalonia 11 / .NET 8**.

This handoff is for **applying Stratum to an existing Avalonia codebase** — not for greenfield setup. The bundle contains a visual reference, structured tokens, and drop-in `.axaml` starters Claude Code can copy in and adapt.

---

## What's in this bundle

```
design_handoff_stratum/
├── README.md                              ← you are here
├── reference/
│   ├── vybedesk Design System.html        ← visual source of truth — open in a browser
│   └── tweaks-panel.jsx                   ← supporting JS; ignore (only needed by the HTML)
├── tokens/
│   ├── tokens.json                        ← machine-readable design tokens (light + dark)
│   └── tokens.css                         ← the original CSS variables (oklch values, exact)
└── axaml/
    ├── Stratum.Theme.axaml                ← Color / SolidColorBrush / type / spacing resources
    ├── Stratum.Controls.axaml             ← ControlTheme starters: Button, TextBox, ComboBox, CheckBox, TabControl, Border, ListBoxItem, ProgressBar
    └── App.axaml.example                  ← how to wire the dictionaries into App.axaml
```

---

## About the design files

The HTML in `reference/` is a **design reference, not production code**. It exists so you can open it in a browser and visually verify the result of your Avalonia work against an authoritative version of the system. Don't try to port HTML/CSS/JS into the app — port the **design** into Avalonia using the resources in `axaml/`.

The `axaml/` files **are** starter production code. They're written for Avalonia 11 and follow current `ControlTheme` / `ThemeDictionaries` conventions. Drop them in, wire them up, then iterate.

## Fidelity

**High-fidelity.** Colors, type scale, spacing, radii, shadows, and motion durations are all final. The `.axaml` files use hex values converted from the authoritative `oklch()` source (preserved as comments in `tokens/tokens.css` and `tokens/tokens.json`). If pixel-exact color matters, sample directly from the HTML reference with a color picker — the hex values are close but not bit-identical to the oklch.

---

## Design intent (read this first)

Stratum optimizes for three things, in order:

1. **Quiet by default.** Nothing competes for attention unless it must. Color is a tool, not a decoration. Surfaces stay flat; depth comes from elevation, not gradients.
2. **Reads like a tool.** Mono for identifiers, status, paths, durations, costs. Sans for prose. Numbers are tabular. Type is sized for hours on a 1440p panel, not for screenshots.
3. **Honest motion.** Motion describes structure — where something came from, where it's going. Never decorative. Almost always under 200ms; never over 320ms.

If a change makes the app louder, busier, or slower without earning it: revert.

---

## Implementation order

Each step can be a separate commit. Don't try to do them all at once.

### 1. Fonts

Drop **Geist** and **Geist Mono** font files into `Assets/Fonts/` (download from [vercel/geist-font](https://github.com/vercel/geist-font) — TTF or WOFF2). They're loaded via the `FontFamily` resources in `Stratum.Theme.axaml`:

```xml
<FontFamily x:Key="StratumFontSans">avares://YourApp/Assets/Fonts#Geist, Segoe UI, $Default</FontFamily>
<FontFamily x:Key="StratumFontMono">avares://YourApp/Assets/Fonts#Geist Mono, Consolas, $Default</FontFamily>
```

Replace `YourApp` with your project's namespace. The `#Geist` fragment is the font's PostScript family name; verify it matches what the actual file reports (use `fc-query` on Linux/macOS or the font's properties on Windows).

### 2. Theme dictionary

Copy `axaml/Stratum.Theme.axaml` to `Styles/Stratum.Theme.axaml` (or wherever your project keeps styles). Reference it from `App.axaml`:

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://YourApp/Styles/Stratum.Theme.axaml" />
</Application.Styles>
```

After this commit, **every `Window` and `TextBlock` will use Stratum's background, foreground, and font** — even if you haven't touched a single view file. Open the app to verify; if the chrome stays Fluent-default the include path is wrong.

### 3. Theme switching

Add a user preference for theme variant and bind it:

```csharp
// Anywhere with access to Application
Application.Current.RequestedThemeVariant = preference switch
{
    "light"  => ThemeVariant.Light,
    "dark"   => ThemeVariant.Dark,
    _        => ThemeVariant.Default,   // follow OS
};
```

The `ThemeDictionaries` block in `Stratum.Theme.axaml` handles the rest — every `DynamicResource` reference swaps automatically.

### 4. Control themes

Copy `axaml/Stratum.Controls.axaml` and include it **after** `Stratum.Theme.axaml`:

```xml
<StyleInclude Source="avares://YourApp/Styles/Stratum.Theme.axaml" />
<StyleInclude Source="avares://YourApp/Styles/Stratum.Controls.axaml" />
```

This restyles `Button`, `TextBox`, `ComboBox`, `CheckBox`, `ToggleSwitch`, `TabControl`, `ProgressBar`, plus `Border` and `ListBoxItem` style classes. Hover / focus / disabled / checked states are all wired.

### 5. Apply style classes in views

The control themes use **Classes** (Avalonia's selector-class equivalent of CSS classes). Update existing views:

```xml
<!-- Before -->
<Button Content="Save" Background="..." Foreground="..." />

<!-- After -->
<Button Classes="primary" Content="Save" />
```

Class map:
- `<Button Classes="primary">` — accent fill
- `<Button Classes="secondary">` — neutral fill, hairline border
- `<Button Classes="ghost">` — transparent until hover
- `<Button Classes="danger">` — red, for destructive actions
- `<Button Classes="icon">` — square, transparent, for icon-only buttons
- Modifiers: `Classes="primary sm"`, `Classes="secondary lg"`

For badges (used heavily in tables / status chips):

```xml
<Border Classes="badge success">
  <TextBlock Text="Done" />
</Border>
```

### 6. Window chrome

vybedesk uses a custom title bar (see the HTML reference). On the root `Window`:

```xml
<Window ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaChromeHints="NoChrome"
        ExtendClientAreaTitleBarHeightHint="-1">
  <DockPanel>
    <!-- Your custom title bar — see the HTML reference at the top of the showcase. -->
    <Grid DockPanel.Dock="Top" Height="38" Background="{DynamicResource StratumSurface1}">
      <!-- brand · screen tabs · window controls -->
    </Grid>
    <!-- ... main content ... -->
  </DockPanel>
</Window>
```

Window-drag region: set `PointerPressed="OnTitleBarDrag"` on the title bar `Grid` and call `BeginMoveDrag(e)` in the handler. Min / maximize / close buttons call `WindowState = ...` and `Close()` directly.

### 7. Density at runtime

The three control-sizing tokens (`StratumCtlHeight`, `StratumCtlPaddingX`, `StratumRowPaddingY`) are plain `x:Double` resources. To switch density, replace them at the `Application.Resources` level:

```csharp
public void SetDensity(string density)
{
    var (h, px, py) = density switch
    {
        "comfortable" => (34d, 13d, 10d),
        "cozy"        => (30d, 11d, 8d),
        _             => (26d, 9d,  6d),   // compact (default)
    };
    Application.Current!.Resources["StratumCtlHeight"]   = h;
    Application.Current!.Resources["StratumCtlPaddingX"] = px;
    Application.Current!.Resources["StratumRowPaddingY"] = py;
}
```

Default to **compact** — vybedesk is a dense pro tool.

### 8. Restyle existing screens

Walk through every `Page` / `UserControl`, replace ad-hoc colors with `DynamicResource` references. For each view, the visual fidelity test is: **open it side-by-side with the HTML reference and compare**. If something doesn't match, the token reference is the answer 90% of the time.

---

## Token map (CSS → Avalonia)

Use these resource keys in your XAML via `{DynamicResource KeyName}`.

### Colors (all `SolidColorBrush`, light + dark variants live in `ThemeDictionaries`)

| CSS variable          | Avalonia key            | Role                                  |
|-----------------------|-------------------------|---------------------------------------|
| `--bg`                | `StratumBg`             | Window background                     |
| `--surface-0`         | `StratumSurface0`       | Card / panel fill                     |
| `--surface-1`         | `StratumSurface1`       | Sidebar / hover                       |
| `--surface-2`         | `StratumSurface2`       | Sunken / pressed                      |
| `--surface-inv`       | `StratumSurfaceInv`     | Toast / tooltip background            |
| `--border-1`          | `StratumBorder1`        | Hairline divider                      |
| `--border-2`          | `StratumBorder2`        | Control border                        |
| `--border-3`          | `StratumBorder3`        | Strong border / hovered control       |
| `--text-1`            | `StratumText1`          | Primary text                          |
| `--text-2`            | `StratumText2`          | Secondary text                        |
| `--text-3`            | `StratumText3`          | Tertiary / dim text                   |
| `--text-inv`          | `StratumTextInv`        | Text on inverse surface (toast)       |
| `--accent`            | `StratumAccent`         | Primary action, links, active         |
| `--accent-2`          | `StratumAccent2`        | Accent hover / pressed                |
| `--accent-3`          | `StratumAccent3`        | Accent on dark surfaces               |
| `--accent-bg`         | `StratumAccentBg`       | Soft accent fill (selection, badge)   |
| `--accent-fg`         | `StratumAccentFg`       | Foreground on accent fill             |
| `--success` / `-bg`   | `StratumSuccess` / `Bg` | Success state                         |
| `--warn` / `-bg`      | `StratumWarn` / `Bg`    | Warning state                         |
| `--danger` / `-bg`    | `StratumDanger` / `Bg`  | Danger / destructive                  |
| `--info` / `-bg`      | `StratumInfo` / `Bg`    | Informational                         |

### Spacing (`x:Double`, in px)

| Token  | Px  | Use                              |
|--------|-----|----------------------------------|
| `StratumS1`  | 2   | hairline gap                     |
| `StratumS2`  | 4   | tight icon + label               |
| `StratumS3`  | 6   | control padding (compact)        |
| `StratumS4`  | 8   | row gap                          |
| `StratumS5`  | 12  | card padding (compact)           |
| `StratumS6`  | 16  | card padding (default)           |
| `StratumS7`  | 20  | section spacing                  |
| `StratumS8`  | 24  | between major blocks             |
| `StratumS9`  | 32  | between sections                 |
| `StratumS10` | 48  | top-level page padding           |

### Radii (`CornerRadius`)

| Token         | Px  | Use                                |
|---------------|-----|------------------------------------|
| `StratumR1`   | 2   | input internal corners             |
| `StratumR2`   | 4   | badges, small chips                |
| `StratumR3`   | 6   | **buttons, inputs (default)**      |
| `StratumR4`   | 8   | cards                              |
| `StratumR5`   | 10  | dialogs                            |
| `StratumR6`   | 14  | hero panels                        |
| `StratumRFull`| 999 | pills, dots                        |

### Type (`x:Double` font size + named `FontFamily`)

| Token            | Px   | Use                                |
|------------------|------|------------------------------------|
| `StratumTextXs`  | 10.5 | eyebrow / mono caption             |
| `StratumTextSm`  | 11.5 | secondary labels, mono inline data |
| `StratumTextMd`  | 13   | **default UI text**                |
| `StratumTextLg`  | 14.5 | subheads, large buttons            |
| `StratumTextXl`  | 17   | H4                                 |
| `StratumText2xl` | 22   | H3                                 |
| `StratumText3xl` | 32   | H2                                 |
| `StratumText4xl` | 52   | H1                                 |

Convenience type classes on `TextBlock`:

```xml
<TextBlock Classes="h1"      Text="Page title"/>
<TextBlock Classes="h2"      Text="Section"/>
<TextBlock Classes="eyebrow" Text="01 · PRINCIPLES"/>
<TextBlock Classes="mono"    Text="aurora/main"/>
<TextBlock Classes="muted"   Text="secondary"/>
<TextBlock Classes="dim"     Text="tertiary"/>
<TextBlock Classes="tabular" Text="$12.40"/>
```

### Motion

| Token              | Duration | Use                                  |
|--------------------|----------|--------------------------------------|
| `StratumDurInstant`| 80ms     | tooltips, focus rings                |
| `StratumDurQuick`  | 140ms    | button / input state changes         |
| `StratumDurBase`   | 200ms    | tab swap, drawer slide               |
| `StratumDurSlow`   | 320ms    | dialog open, page transition         |

| Easing              | Curve                                   |
|---------------------|-----------------------------------------|
| `StratumEaseOut`    | `cubic-bezier(0.2,  0.7,  0.2, 1.0)`    |
| `StratumEaseInOut`  | `cubic-bezier(0.6,  0.05, 0.2, 1.0)`    |
| `StratumEaseSpring` | `cubic-bezier(0.34, 1.36, 0.5, 1.0)`    |

Apply via `Transitions` on `Background`, `Foreground`, `BorderBrush`, `Opacity`, `RenderTransform`. **Never on layout properties** (`Width`, `Height`, `Margin`).

---

## Component → Avalonia control map

| Stratum component | Avalonia control | How to apply                                              |
|-------------------|------------------|-----------------------------------------------------------|
| `.btn-primary`    | `Button`         | `Classes="primary"`                                       |
| `.btn-secondary`  | `Button`         | `Classes="secondary"`                                     |
| `.btn-ghost`      | `Button`         | `Classes="ghost"`                                         |
| `.btn-danger`     | `Button`         | `Classes="danger"`                                        |
| `.btn-icon`       | `Button`         | `Classes="icon"` + an icon `Path` as `Content`            |
| `.input`          | `TextBox`        | default theme (mono variant: `Classes="mono"`)            |
| `.select`         | `ComboBox`       | default theme                                             |
| `.check`          | `CheckBox`       | default theme                                             |
| `.radio`          | `RadioButton`    | default theme                                             |
| `.switch`         | `ToggleSwitch`   | default theme                                             |
| `.seg` (segmented)| `ItemsControl` of `RadioButton` inside a `Border` | custom — see HTML reference |
| `.tabs`           | `TabControl`     | underline style is built into `TabItem` theme             |
| `.badge`          | `Border`         | `Classes="badge"` (+ `success`/`warn`/`danger`/`info`/`accent`) |
| `.tbl`            | `DataGrid`       | apply `FontFeatures="+tnum"` on number columns            |
| `.menu`           | `MenuFlyout`     | needs its own ControlTheme — extrapolate from button styles |
| `.dialog`         | `Window`         | use `WindowStartupLocation="CenterOwner"`                 |
| `.toast`          | `NotificationCard` (`Avalonia.Controls.Notifications`) | overlay manager |
| `.progress`       | `ProgressBar`    | default theme                                             |
| `.tooltip`        | `ToolTip`        | needs a ControlTheme — surface-inv background             |
| `.nav-item`       | `ListBoxItem`    | `Classes="nav"`                                           |
| `.tree-row`       | `TreeViewItem`   | needs a ControlTheme — extrapolate from `ListBoxItem.nav` |
| `.statusbar`      | `Border`         | `Classes="statusbar"`                                     |
| Window chrome     | Custom `Window` with extended client area + `Grid` header | see step 6 above |

---

## State → pseudo-class map

| CSS               | Avalonia            | Notes                                |
|-------------------|---------------------|--------------------------------------|
| `:hover`          | `:pointerover`      |                                      |
| `:active`         | `:pressed`          |                                      |
| `:focus`          | `:focus`            |                                      |
| `:focus-visible`  | `:focus-visible`    | preferred for keyboard focus rings   |
| `:disabled`       | `:disabled`         |                                      |
| `[checked]`       | `:checked`          | for `CheckBox` / `RadioButton`       |
| `[aria-selected]` | `:selected`         | for `TabItem` / `ListBoxItem`        |
| `[aria-invalid]`  | use a `Classes="error"` modifier | bind from view model        |

---

## What to watch out for

- **Tabular numerics.** Set `FontFeatures="+tnum"` on any data control (DataGrid, status bar, metric cards). Stratum is unusable without tabular nums in tables — columns won't align.
- **Mono for identifiers.** File paths, branch names, model IDs, hashes, durations, costs — all mono. Sans is reserved for prose and labels. When you find an ad-hoc `FontFamily` reference on a path or ID, swap it to `{DynamicResource StratumFontMono}`.
- **Density is runtime.** Don't bake control heights into individual styles. Use `{DynamicResource StratumCtlHeight}` and let the application switch them globally.
- **One accent at a time.** Semantic colors (`Success`, `Warn`, `Danger`, `Info`) have their own meaning — never use them as accent substitutes for decoration. If a screen feels "too quiet," that's the system working as intended.
- **No emoji.** Use line icons. The HTML reference uses simple 14×14 stroked SVGs — recreate as `Path` data in Avalonia, or pull in [Projektanker.Icons.Avalonia](https://github.com/Projektanker/Icons.Avalonia) with [Lucide](https://lucide.dev/) as the icon pack. Icon stroke width: **1.5**.
- **Hover targets ≥ 26 px tall** (compact density). Below that becomes hard to hit with a trackpad.
- **`StratumFontMono` everywhere mono** — paths, kbd hints (`⌘K`), durations (`02:14`), token counts (`4,820`), costs (`$0.031`), file deltas (`+8 −3`).
- **ControlTheme part names can drift** between Avalonia minor versions. The CheckBox example targets `Border#NormalRectangle` which is correct for Avalonia 11.0 Fluent theme — verify with **DevTools (F12)** on a running app if a style doesn't take.

---

## Verification checklist (per screen)

When porting a view, walk through:

- [ ] No hardcoded hex colors anywhere in the XAML
- [ ] No hardcoded font families
- [ ] All identifiers / paths / data use `StratumFontMono`
- [ ] Buttons use `Classes`, not inline `Background`
- [ ] Hover, focus, disabled all visible and animated
- [ ] Theme toggle (Light / Dark) swaps cleanly without restart
- [ ] Density toggle changes control heights live
- [ ] Tabular numerics on every number column
- [ ] Open the HTML reference side-by-side and spot-compare

---

## Open work / extrapolation needed

The starters cover the spine. These remain for Claude Code to fill in following the same patterns:

- `MenuFlyout` / `ContextMenu` — extrapolate from `Button.secondary` for items
- `ToolTip` — surface-inv background, `StratumR2` corner, `StratumTextSm` font
- `DataGrid` — header row uses eyebrow type; cells use `StratumTextMd`; numeric columns get `+tnum`; row hover = `StratumSurface1`
- `TreeViewItem` — extrapolate from `ListBoxItem.nav`; chevron rotation animates 140ms
- `Slider` — track = `StratumSurface2`, fill = `StratumAccent`, thumb = `StratumSurface0` with `StratumBorder3` ring
- `NumericUpDown` — same as `TextBox`, stepper buttons use `Button.ghost.icon`
- `NotificationCard` — `Background={StratumSurfaceInv}`, foreground inverted, `StratumR4` corner, `Shadow="0 4 14 0 #14000000"` equivalent
- Window-drag region wiring on the custom title bar
- Status bar live-binding (connection dot, model, branch, run count)

When in doubt: open the HTML reference, find the equivalent component, match the visual.

---

## Reference files

- `reference/vybedesk Design System.html` — open in Chrome / Edge / Firefox. Toggle theme + density at top-right. Tweaks panel (bottom-right) cycles accent colors.

## Questions?

If a token's intent is unclear from this README, the HTML reference is the tie-breaker — it's the authoritative visual specification. Everything in `tokens/` and `axaml/` was derived from it.
