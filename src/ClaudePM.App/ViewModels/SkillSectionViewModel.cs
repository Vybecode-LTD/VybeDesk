using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// The single sidebar page for the Skill area. It does no skill work itself —
/// it is a thin container that hosts the skill sub-pages and switches between
/// them with an in-pane toggle, so the left sidebar needs no submenu support.
///
/// This mirrors, one level down, what MainWindowViewModel does for the whole
/// app: it owns a set of sub-pages and a "current" one. Today it hosts the
/// Skill Manager; when the Skill Builder module is built, that becomes the
/// second sub-page with no change to the sidebar or the shell.
/// </summary>
public sealed partial class SkillSectionViewModel : PageViewModel
{
    public override string Title => "Skills";
    public override string Glyph => "\U0001F9E9";
    public override string Description =>
        "Manage your skill library and build new skills.";

    /// <summary>The skill-management sub-page.</summary>
    public SkillManagerViewModel Manager { get; }

    /// <summary>
    /// The skill-building sub-page. Nullable on purpose: the Skill Builder
    /// module may not be built yet. When it is null the builder tab is simply
    /// hidden, and the section still works as a manager-only page. Once the
    /// SkillBuilderViewModel exists, accept it through the constructor and the
    /// builder tab lights up automatically.
    /// </summary>
    public PageViewModel? Builder { get; }

    /// <summary>
    /// The sub-page currently shown in the lower area of the section. The
    /// in-pane toggle at the top of SkillSectionView is bound to the commands
    /// below, which set this.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManagerActive))]
    [NotifyPropertyChangedFor(nameof(IsBuilderActive))]
    private PageViewModel _activePage;

    /// <summary>True when the builder sub-page exists and can be shown.</summary>
    public bool HasBuilder => Builder is not null;

    /// <summary>Bound by the toggle buttons to show which tab is selected.</summary>
    public bool IsManagerActive => ReferenceEquals(ActivePage, Manager);
    public bool IsBuilderActive => Builder is not null && ReferenceEquals(ActivePage, Builder);

    /// <summary>
    /// Constructor. The manager is required; the builder is optional so the
    /// section compiles and runs before the Skill Builder module is built.
    /// </summary>
    public SkillSectionViewModel(
        SkillManagerViewModel manager,
        PageViewModel? builder = null)
    {
        Manager = manager;
        Builder = builder;

        // Default to the manager — it is the sub-page that always exists.
        _activePage = manager;
    }

    /// <summary>Switches the in-pane content to the Skill Manager.</summary>
    [RelayCommand]
    private void ShowManager() => ActivePage = Manager;

    /// <summary>
    /// Switches the in-pane content to the Skill Builder. Does nothing if the
    /// builder has not been built yet, so the command is always safe to bind.
    /// </summary>
    [RelayCommand]
    private void ShowBuilder()
    {
        if (Builder is not null)
            ActivePage = Builder;
    }
}
