using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudePM.App.ViewModels;

/// <summary>The shell. Owns the navigable page list and the current page.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<PageViewModel> Pages { get; }

    [ObservableProperty]
    private PageViewModel _currentPage;

    public MainWindowViewModel(
        HomeViewModel home,
        ProjectsViewModel projects,
        DocumentationViewModel documentation,
        PromptManagerViewModel prompts,
        SessionBuilderViewModel sessionBuilder,
        NotebookViewModel notebook,
        SkillSectionViewModel skills,
        BugTrackerViewModel bugs,
        TestingManagerViewModel testing,
        VisionAuditViewModel vision,
        SettingsViewModel settings)
    {
        Pages = new ObservableCollection<PageViewModel>
        {
            home, projects, documentation, prompts, sessionBuilder, notebook, skills, bugs, testing, vision, settings,
        };
        _currentPage = home;
    }
}
