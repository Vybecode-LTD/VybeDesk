namespace VybeDesk.App.ViewModels;

/// <summary>
/// The Settings sidebar entry — a GROUP NODE (mirrors <see cref="SkillSectionViewModel"/>).
/// It is never rendered itself; selecting it re-routes to the first child
/// (<see cref="General"/>) via <c>MainWindowViewModel.OnCurrentPageChanged</c>.
/// Children: <b>General</b> (the existing app settings, unchanged) and
/// <b>Plugins</b> (manage installed plugins). Has no view file of its own.
/// </summary>
public sealed class SettingsSectionViewModel : PageViewModel
{
    public override string Title => "Settings";
    public override string Glyph => "⚙"; // ⚙
    public override string Description => "App settings and plugins.";

    /// <summary>The existing settings page (API key, model, output, AI activity).</summary>
    public SettingsViewModel General { get; }

    /// <summary>The plugin management sub-page.</summary>
    public PluginsViewModel Plugins { get; }

    public override IReadOnlyList<PageViewModel> Children =>
        new PageViewModel[] { General, Plugins };

    public SettingsSectionViewModel(SettingsViewModel general, PluginsViewModel plugins)
    {
        General = general;
        Plugins = plugins;
    }
}
