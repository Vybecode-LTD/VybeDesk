using VybeDesk.Core.Models;

namespace VybeDesk.Core.Services;

/// <summary>
/// Cross-module coordinator: lets the Home dashboard hand a project to the
/// Documentation tab and surface that page so the user can immediately start
/// working with the selected project. Mirrors the <see cref="INotebookOpener"/>
/// pattern — a tiny one-method shared surface so Home and Documentation share
/// nothing of each other's internals.
/// </summary>
public interface IHomeNavigator
{
    /// <summary>Jump to the Documentation tab and set its SelectedProject.</summary>
    void NavigateToDocumentation(Project project);
}
