using ClaudePM.App.ViewModels;
using ClaudePM.Core.Models;
using ClaudePM.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudePM.App.Services;

/// <summary>
/// Default IHomeNavigator. Resolves DocumentationViewModel + MainWindowViewModel
/// lazily through the service provider so this class can be constructed
/// (and registered) without itself depending on the shell VM — that would
/// create a DI cycle, since the shell transitively depends on every page
/// (including HomeViewModel, which depends on this).
///
/// Mirrors <see cref="NotebookOpener"/>'s lookup pattern: re-resolve the
/// project from the destination VM's own Projects collection by Id so the
/// ComboBox selection sticks even though the IProjectStore returns fresh
/// Project instances on each GetAllAsync call. Without this lookup,
/// SelectedProject is set but the ComboBox shows nothing selected.
/// </summary>
public sealed class HomeNavigator(IServiceProvider services) : IHomeNavigator
{
    public void NavigateToDocumentation(Project project)
    {
        var main = services.GetRequiredService<MainWindowViewModel>();
        var docs = services.GetRequiredService<DocumentationViewModel>();
        var local = docs.Projects.FirstOrDefault(p => p.Id == project.Id) ?? project;
        docs.SelectedProject = local;
        main.CurrentPage = docs;
        if (docs.ScanCommand.CanExecute(null))
            docs.ScanCommand.Execute(null);
    }
}
