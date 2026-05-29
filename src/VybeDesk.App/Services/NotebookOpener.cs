using VybeDesk.App.ViewModels;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace VybeDesk.App.Services;

/// <summary>
/// Default INotebookOpener. Resolves NotebookViewModel + MainWindowViewModel
/// lazily through the service provider so this class can be constructed
/// (and registered) without itself depending on the shell VM — that would
/// create a DI cycle, since the shell transitively depends on every page
/// (including DocumentationViewModel, which depends on this).
///
/// Threading: all mutations target ViewModel observable properties, which
/// must run on the UI thread. Callers are already on the UI thread when
/// they invoke an [RelayCommand] method, so no Dispatcher.Post is needed
/// here. (Add one if a non-UI caller ever needs to call this — none
/// today.)
/// </summary>
public sealed class NotebookOpener(IServiceProvider services) : INotebookOpener
{
    public void OpenWithFixPrompt(Project project, string prompt)
    {
        // Both VMs are singletons (see Program.cs) — same instance the shell
        // holds in its Pages collection, so navigating + populating the
        // Notebook here surfaces the change immediately in the UI.
        var notebook = services.GetRequiredService<NotebookViewModel>();
        var main = services.GetRequiredService<MainWindowViewModel>();

        // Look up the project in the Notebook's OWN Projects collection by
        // Id rather than using the caller's instance directly — the ComboBox
        // in Notebook matches SelectedItem against ItemsSource by reference
        // equality, and the IProjectStore returns fresh Project instances on
        // each GetAllAsync call, so the caller's instance and the Notebook's
        // local instance are different .NET references even when they
        // describe the same project. Without this lookup, ActiveProject is
        // set but the ComboBox shows nothing selected. Falls back to the
        // caller's instance if Notebook hasn't loaded its Projects yet, OR
        // if the project isn't in Notebook's filtered list (e.g. it has no
        // FolderPath — agent file actions would fail anyway in that case).
        var localProject = notebook.Projects.FirstOrDefault(p => p.Id == project.Id)
                           ?? project;

        // Reset the conversation BEFORE injecting the new prompt. If the
        // user had unresolved tool_use blocks from a prior turn, appending
        // a new user message to that history violates Anthropic's tool_use
        // protocol (every tool_use REQUIRES a tool_result in the next
        // message — see the 400 error users see otherwise). Apply-with-AI
        // is also a focused new task, not a continuation, so a fresh slate
        // is the right semantic.
        notebook.BeginFreshConversation();

        notebook.ActiveProject = localProject;
        notebook.ChatInput = prompt;
        notebook.StatusMessage = "Fix prompt loaded — review and click Send to apply.";
        main.CurrentPage = notebook;
    }

}
