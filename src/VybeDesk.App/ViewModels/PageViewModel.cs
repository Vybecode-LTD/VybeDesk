using CommunityToolkit.Mvvm.Input;

namespace VybeDesk.App.ViewModels;

/// <summary>Base for any view model shown in the main content area / sidebar.</summary>
public abstract class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }
    public abstract string Glyph { get; }
    public abstract string Description { get; }

    /// <summary>Planned-capability bullets shown by stub module views.</summary>
    public virtual IReadOnlyList<string> Highlights => Array.Empty<string>();

    // ===== Unified module header surface (v0.31) =====

    /// <summary>
    /// Optional breadcrumb crumbs displayed after the module title in the
    /// unified header (e.g. <c>Module &#x203A; Sub-page &#x203A; Stage</c>).
    /// The module title is always rendered first by the header control;
    /// these crumbs append after it in left-to-right order.
    /// Default: empty (no breadcrumbs shown). Override in a concrete
    /// view model to expose stage/sub-page state, and remember to raise
    /// <c>OnPropertyChanged(nameof(Breadcrumbs))</c> (or wire it up via
    /// <c>[NotifyPropertyChangedFor]</c>) whenever the underlying state
    /// changes so the header refreshes.
    /// </summary>
    public virtual IReadOnlyList<string> Breadcrumbs => Array.Empty<string>();

    /// <summary>
    /// Returns to the module's home state WITHOUT discarding data
    /// (e.g. for a multi-stage wizard: jump back to step 1 while keeping
    /// every answer the user has entered so far intact). Use this for
    /// navigation, never for clearing state.
    /// Default: <c>null</c> (the home icon is hidden in the header).
    /// Override in a concrete view model to point at a generated
    /// <c>[RelayCommand]</c> property to expose the icon.
    /// </summary>
    public virtual IRelayCommand? GoModuleHomeCommand => null;

    /// <summary>
    /// Clears the input fields on the CURRENT page or stage only. Does
    /// NOT change which stage or sub-page is active &mdash; the user
    /// stays exactly where they are. This is the non-destructive option
    /// for "I mistyped, let me start this form over". Saved data on
    /// other stages, the active stage selection, and persisted history
    /// are all preserved.
    /// Default: <c>null</c> (the Reset chip is hidden in the header).
    /// Override in a concrete view model to point at a generated
    /// <c>[RelayCommand]</c> property to expose the chip.
    /// </summary>
    public virtual IRelayCommand? ResetCommand => null;

    /// <summary>
    /// Clears ALL module state AND returns to the first stage. This is
    /// the destructive "start over from scratch" option &mdash; in-memory
    /// drafts, verdicts, generated outputs, every transient field gets
    /// wiped, and the user is placed back at stage 1. Persisted
    /// cross-run history (e.g. saved audit history, the project list)
    /// is NOT touched.
    /// Default: <c>null</c> (the Restart chip is hidden in the header).
    /// Override in a concrete view model to point at a generated
    /// <c>[RelayCommand]</c> property to expose the chip.
    /// </summary>
    public virtual IRelayCommand? RestartCommand => null;

    // ===== Sidebar submenu surface (v0.32) =====

    /// <summary>
    /// Optional submenu children shown nested under this page in the sidebar
    /// TreeView. Default: empty (this page is a leaf node). Override to expose
    /// sub-pages (e.g. <see cref="SkillSectionViewModel"/> returns
    /// <c>[Manager, Builder]</c>).
    ///
    /// Pages that return a non-empty <see cref="Children"/> collection are
    /// treated as group nodes &mdash; they still appear in the sidebar with
    /// their own <see cref="Title"/>/<see cref="Glyph"/>, they expand to
    /// reveal Children, and selecting them triggers the default-child-routing
    /// logic in <c>MainWindowViewModel.OnCurrentPageChanged</c> (which
    /// re-routes the selection to the first child so the content area never
    /// renders a bare group node).
    /// </summary>
    public virtual IReadOnlyList<PageViewModel> Children =>
        Array.Empty<PageViewModel>();

    // ===== Activation lifecycle (v0.32 persistence fix) =====

    /// <summary>
    /// Called by <see cref="MainWindowViewModel"/> every time this page becomes
    /// the active <c>CurrentPage</c> — including on the FIRST navigation AND on
    /// every subsequent return visit.
    ///
    /// Project-scoped VMs override this to re-sync their <c>SelectedProject</c>
    /// from <see cref="IActiveProjectContext.Current"/>. This is necessary
    /// because Avalonia's <c>ContentPresenter.UpdateChild</c> detaches the old
    /// view on navigation, which causes the <c>ModuleHeader</c> ComboBox's
    /// TwoWay binding to write <c>null</c> back to the VM's backing field.
    /// When the user navigates back, <c>IActiveProjectContext.Current</c>
    /// hasn't changed (so <c>Changed</c> doesn't fire), and the VM's
    /// <c>SelectedProject</c> stays null without an explicit re-sync here.
    ///
    /// Default: no-op. Override only in VMs that need re-sync.
    /// </summary>
    public virtual void OnActivated() { }
}
