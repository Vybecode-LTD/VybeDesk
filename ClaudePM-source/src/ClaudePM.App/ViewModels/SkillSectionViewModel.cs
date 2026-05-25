namespace ClaudePM.App.ViewModels;

/// <summary>
/// The Skills sidebar entry. After v0.32 this is a pure GROUP NODE: it never
/// gets routed to the content area, it just appears in the sidebar TreeView
/// as a parent whose <see cref="Children"/> are the Skill Manager and Skill
/// Builder sub-pages. Selecting either child navigates to that page; selecting
/// the parent itself is intercepted by <c>MainWindowViewModel.
/// OnCurrentPageChanged</c> and re-routed to the first child (Manager) so
/// the content area never tries to render a bare section node.
///
/// Note: this view model intentionally has NO matching view file. The
/// ViewLocator never looks it up because the navigation logic above always
/// re-routes away from it. The previous <c>SkillSectionView.axaml</c> + its
/// in-pane Manager/Builder toggle bar were deleted in v0.32 — that toggle is
/// now the sidebar expansion.
/// </summary>
public sealed class SkillSectionViewModel : PageViewModel
{
    public override string Title => "Skills";
    public override string Glyph => "\U0001F9E9";
    public override string Description =>
        "Manage your skill library and build new skills.";

    /// <summary>The skill-management sub-page (first child).</summary>
    public SkillManagerViewModel Manager { get; }

    /// <summary>
    /// The skill-building sub-page. Nullable on purpose for backward
    /// compatibility: production wiring always passes a non-null Builder,
    /// but the optional parameter means the section still compiles and
    /// runs as Manager-only if the Builder is ever pulled out.
    /// </summary>
    public PageViewModel? Builder { get; }

    /// <summary>
    /// The sidebar sub-items. Manager is always present; Builder appears
    /// when its sub-page has been wired up. The Where-not-null filter
    /// keeps the contract safe even in the defensive Builder=null case.
    /// </summary>
    public override IReadOnlyList<PageViewModel> Children =>
        new[] { (PageViewModel)Manager, Builder! }
            .Where(x => x is not null)
            .ToArray();

    /// <summary>
    /// Constructor. The manager is required; the builder is optional so the
    /// section compiles and runs even if the Builder DI registration is
    /// removed in a future refactor.
    /// </summary>
    public SkillSectionViewModel(
        SkillManagerViewModel manager,
        PageViewModel? builder = null)
    {
        Manager = manager;
        Builder = builder;
    }
}
