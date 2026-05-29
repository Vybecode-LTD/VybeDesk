using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace VybeDesk.App.Controls;

/// <summary>
/// Unified module header (v0.31 redesign). One single-band control replacing
/// the previous header + sub-header stack. Total height fixed at 105px.
///
/// Two surfaces of data:
///
///   1) DataContext-bound (inherited from the parent view's PageViewModel):
///      Title / Glyph / Description / Breadcrumbs / GoModuleHomeCommand /
///      ResetCommand / RestartCommand. Compiled bindings against
///      PageViewModel resolve these polymorphically.
///
///   2) StyledProperties set per call site:
///      <see cref="ShowPicker"/> / <see cref="ProjectsSource"/> /
///      <see cref="PickerSelectedItem"/> / <see cref="StatusMessage"/>.
///      These let the SAME control work for VMs with different picker
///      property names (e.g. Notebook's <c>ActiveProject</c> vs other
///      modules' <c>SelectedProject</c>) without a base-class virtual.
///
/// Intentionally does NOT set <c>DataContext = this</c> — the control
/// inherits its parent view's DataContext (the concrete PageViewModel).
/// Internal bindings against the StyledProperties use the
/// <c>{Binding #Root.X}</c> ElementName form (the UserControl is named
/// "Root") because <c>{Binding $parent[UserControl].X}</c> resolves
/// <c>$parent</c> to the base <c>UserControl</c> type and fails AVLN2000.
/// </summary>
public partial class ModuleHeader : UserControl
{
    /// <summary>
    /// When true, renders the "Project:" label + ComboBox in the middle
    /// column. Default false — non-project modules omit the picker entirely
    /// and the middle column collapses to 0 width.
    /// </summary>
    public static readonly StyledProperty<bool> ShowPickerProperty =
        AvaloniaProperty.Register<ModuleHeader, bool>(nameof(ShowPicker));

    /// <summary>
    /// The project list bound to the picker's ItemsSource. Required when
    /// <see cref="ShowPicker"/> is true; ignored otherwise.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ProjectsSourceProperty =
        AvaloniaProperty.Register<ModuleHeader, IEnumerable?>(nameof(ProjectsSource));

    /// <summary>
    /// The picker's SelectedItem, two-way bound by default so changes flow
    /// back to the VM. Typed as <c>object?</c> so Notebook's
    /// <c>ActiveProject</c> and every other module's <c>SelectedProject</c>
    /// bind without a base class.
    /// </summary>
    public static readonly StyledProperty<object?> PickerSelectedItemProperty =
        AvaloniaProperty.Register<ModuleHeader, object?>(
            nameof(PickerSelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The status message displayed in the lighter-background bottom band.
    /// Always rendered (empty string = blank line, same height) — that's
    /// the whole point of the band. Capped at one line via MaxLines +
    /// ellipsis.
    /// </summary>
    public static readonly StyledProperty<string> StatusMessageProperty =
        AvaloniaProperty.Register<ModuleHeader, string>(
            nameof(StatusMessage),
            defaultValue: string.Empty);

    public bool ShowPicker
    {
        get => GetValue(ShowPickerProperty);
        set => SetValue(ShowPickerProperty, value);
    }

    public IEnumerable? ProjectsSource
    {
        get => GetValue(ProjectsSourceProperty);
        set => SetValue(ProjectsSourceProperty, value);
    }

    public object? PickerSelectedItem
    {
        get => GetValue(PickerSelectedItemProperty);
        set => SetValue(PickerSelectedItemProperty, value);
    }

    public string StatusMessage
    {
        get => GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    public ModuleHeader() => InitializeComponent();
}
