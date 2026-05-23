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
        DocumentationViewModel documentation,
        PromptManagerViewModel prompts,
        SessionBuilderViewModel sessionBuilder,
        NotebookViewModel notebook,
        SkillLibraryViewModel skills,
        SettingsViewModel settings)
    {
        Pages = new ObservableCollection<PageViewModel>
        {
            home, documentation, prompts, sessionBuilder, notebook, skills, settings,
        };
        _currentPage = home;
    }
}
