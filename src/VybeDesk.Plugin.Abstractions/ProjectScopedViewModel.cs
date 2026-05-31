using System.Collections.ObjectModel;
using Avalonia.Threading;
using VybeDesk.Core.Models;
using VybeDesk.Core.Services;

namespace VybeDesk.Plugin;

/// <summary>
/// Base class for ViewModels that operate within a user-selected project
/// (Documentation, Bug Tracker, Testing Manager, Vision Audit). Encapsulates
/// the shared project-selection lifecycle:
/// <list type="bullet">
///   <item><see cref="LoadProjectsAsync"/> — reload the picker while
///         suppressing the TwoWay binding null pulse from
///         <c>Projects.Clear()</c></item>
///   <item><see cref="HandleProjectChanged"/> — shared preamble for the
///         <c>OnSelectedProjectChanged</c> partial (guard, persist, sync
///         to <see cref="IActiveProjectContext"/>, same-ID early-out)</item>
///   <item><see cref="OnActivated"/> — restore <c>SelectedProject</c> from
///         module-local memory on navigation back</item>
/// </list>
///
/// Each derived class still declares its own
/// <c>[ObservableProperty] private Project? _selectedProject;</c> because
/// CommunityToolkit.Mvvm requires the field and the generated partial
/// <c>OnSelectedProjectChanged</c> to live in the same partial class.
/// The derived class's handler calls <see cref="HandleProjectChanged"/>
/// for the shared preamble, then does its per-module cleanup when
/// <see cref="HandleProjectChanged"/> returns <c>true</c> (meaning a
/// genuine project switch occurred).
///
/// <b>Not used by</b> PromptManagerViewModel (which has an AllProjectsSentinel
/// and no <see cref="IActiveProjectContext"/> sync) or NotebookViewModel
/// (which uses <c>ActiveProject</c> instead of <c>SelectedProject</c> and
/// has materially different lifecycle logic).
/// </summary>
public abstract class ProjectScopedViewModel : PageViewModel
{
    private readonly IProjectStore _projectStore;
    private readonly IActiveProjectContext _activeProjectContext;

    /// <summary>
    /// Set <c>true</c> while <see cref="LoadProjectsAsync"/> is rebuilding
    /// the <see cref="Projects"/> collection. During this window, the
    /// ComboBox TwoWay binding fires <c>SelectedProject = null</c>
    /// synchronously inside <c>Projects.Clear()</c> — the derived class's
    /// <c>OnSelectedProjectChanged</c> must early-out when this is set.
    /// </summary>
    protected bool ReloadingProjects { get; private set; }

    /// <summary>
    /// Module-local project memory. Survives the null pulse from
    /// ContentPresenter detachment and is NOT overwritten by other modules'
    /// project selections. <see cref="OnActivated"/> restores from this
    /// field — not from <see cref="IActiveProjectContext.Current"/> — so
    /// each module keeps its own independent selection (project isolation).
    /// </summary>
    protected Guid? LastSelectedProjectId { get; set; }

    /// <summary>
    /// Projects shown in the unified header's picker. Bound to
    /// <c>ModuleHeader.ProjectsSource</c> via each view's AXAML.
    /// </summary>
    public ObservableCollection<Project> Projects { get; } = new();

    /// <summary>Wires up the shared project-selection lifecycle; call from a derived constructor.</summary>
    protected ProjectScopedViewModel(
        IProjectStore projectStore,
        IActiveProjectContext activeProjectContext)
    {
        _projectStore = projectStore;
        _activeProjectContext = activeProjectContext;
        _projectStore.Changed += OnProjectsChanged;
        _ = LoadProjectsAsync();
    }

    private void OnProjectsChanged()
        => Dispatcher.UIThread.Post(async () => await LoadProjectsAsync());

    /// <summary>
    /// Reloads the <see cref="Projects"/> collection from the store,
    /// restoring the user's previous selection (if any) after the rebuild.
    /// The <see cref="ReloadingProjects"/> flag suppresses the transient
    /// null that the ComboBox TwoWay binding fires during
    /// <c>Projects.Clear()</c>.
    /// </summary>
    protected async Task LoadProjectsAsync()
    {
        var all = await _projectStore.GetAllAsync();

        // Capture keepId AFTER the async gap: use our current selection first,
        // then fall back to our own module-local memory (NOT the cross-module
        // context, which may reflect a different module's selection).
        var keepId = GetSelectedProjectId() ?? LastSelectedProjectId;

        // Guard: Projects.Clear() causes the ComboBox TwoWay binding to fire
        // SelectedProject = null synchronously while the collection is empty.
        // ReloadingProjects tells HandleProjectChanged to skip the write.
        ReloadingProjects = true;
        try
        {
            Projects.Clear();
            foreach (var p in all) Projects.Add(p);
            // Only restore a previous selection — do NOT auto-select the
            // first project when none was ever chosen. The view shows a
            // "Choose a project" landing until the user picks one.
            var restored = keepId is not null
                ? Projects.FirstOrDefault(p => p.Id == keepId)
                : null;
            SetSelectedProject(restored);

            // Explicitly save to module-local memory because the
            // ReloadingProjects guard suppresses OnSelectedProjectChanged
            // during this window, so the normal save path doesn't run.
            LastSelectedProjectId = GetSelectedProjectId();
        }
        finally
        {
            Dispatcher.UIThread.Post(() => ReloadingProjects = false);
        }
    }

    /// <summary>
    /// Shared preamble for the <c>OnSelectedProjectChanged</c> partial
    /// method. Handles the reload guard, null-write suppression, module-local
    /// memory persistence, cross-module context sync, and same-ID early-out.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a genuine project switch occurred (the derived class
    /// should clear per-module transient state); <c>false</c> if the change
    /// was suppressed (the derived class should return immediately).
    /// </returns>
    protected bool HandleProjectChanged(Project? oldValue, Project? newValue)
    {
        // Suppress the transient null that arrives from the ComboBox TwoWay
        // binding when Projects.Clear() runs inside LoadProjectsAsync.
        if (ReloadingProjects) return false;

        // Ignore null writes from view detachment. When Avalonia's
        // ContentPresenter detaches this view on navigation, the ComboBox
        // TwoWay binding fires null back. We must NOT clear state for this
        // — OnActivated will restore the selection when we come back.
        if (newValue is null && oldValue is not null) return false;

        // Persist the user's selection for this module. Survives null
        // pulses from ContentPresenter detachment and is NOT overwritten
        // by other modules' project selections (project isolation).
        LastSelectedProjectId = newValue?.Id;

        // Keep the cross-module context in sync for AI model resolution
        // (AnthropicChatService reads IActiveProjectContext.Current).
        if (newValue?.Id != _activeProjectContext.Current?.Id)
            _activeProjectContext.SetCurrent(newValue);

        // Only clear per-module transient state on a genuine project switch,
        // not on same-ID refreshes from LoadProjectsAsync re-creating object refs.
        return oldValue?.Id != newValue?.Id;
    }

    /// <summary>
    /// Re-sync SelectedProject from this module's own local memory on
    /// every navigation back to this page. Necessary because Avalonia's
    /// ContentPresenter detaches the old view on navigate-away, which
    /// causes the ModuleHeader ComboBox's TwoWay binding to null out
    /// the backing field. Uses <see cref="LastSelectedProjectId"/>
    /// instead of <see cref="IActiveProjectContext.Current"/> so each
    /// module keeps its own independent project selection.
    /// </summary>
    public override void OnActivated()
    {
        if (LastSelectedProjectId is null) return;
        if (GetSelectedProjectId() == LastSelectedProjectId) return;
        var found = Projects.FirstOrDefault(p => p.Id == LastSelectedProjectId);
        if (found is not null)
            SetSelectedProject(found);
    }

    /// <summary>
    /// Returns the ID of the currently selected project, or <c>null</c>
    /// if none is selected. Implemented by each derived VM to read its
    /// own <c>[ObservableProperty]</c> backing field.
    /// </summary>
    protected abstract Guid? GetSelectedProjectId();

    /// <summary>
    /// Sets the currently selected project. Implemented by each derived VM
    /// to write its own <c>[ObservableProperty]</c> backing field.
    /// </summary>
    protected abstract void SetSelectedProject(Project? project);
}
