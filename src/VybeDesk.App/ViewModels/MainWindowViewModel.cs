using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using VybeDesk.App.Modules;

namespace VybeDesk.App.ViewModels;

/// <summary>The shell. Owns the navigable page list and the current page.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<PageViewModel> Pages { get; }

    [ObservableProperty]
    private PageViewModel _currentPage;

    public MainWindowViewModel(IModuleCatalog catalog)
    {
        Pages = new ObservableCollection<PageViewModel>(catalog.Pages);
        _currentPage = Pages[0]; // Home — the catalog guarantees it leads.
    }

    /// <summary>
    /// Intercepts selection of group nodes (pages with non-empty
    /// <see cref="PageViewModel.Children"/>). When the user clicks a group
    /// node in the sidebar TreeView (e.g. the Skills parent row), we
    /// re-route the selection to the group's first child so the content
    /// area never tries to render a bare group node that has no view.
    ///
    /// <see cref="Dispatcher.UIThread"/> <c>Post</c> is used rather than a
    /// direct assignment because mutating <see cref="CurrentPage"/> inside
    /// its own setter would re-enter the source-generator's change-
    /// notification path and confuse the TreeView's two-way binding. Posting
    /// to the dispatcher lets the current setter complete first, then the
    /// re-route runs on the next UI tick — the user perceives a single
    /// instant click.
    /// </summary>
    partial void OnCurrentPageChanged(PageViewModel? oldValue, PageViewModel newValue)
    {
        // _currentPage's field type is non-nullable PageViewModel, so the
        // source generator emits this with a non-nullable second parameter.
        if (newValue.Children.Count > 0)
        {
            var defaultChild = newValue.Children[0];
            Dispatcher.UIThread.Post(() => CurrentPage = defaultChild);
            return; // OnActivated will fire when the child page becomes CurrentPage
        }

        // Re-sync project-scoped VMs with IActiveProjectContext on every
        // navigation. When Avalonia's ContentPresenter detaches the old view,
        // the ModuleHeader ComboBox's TwoWay binding writes null to the VM's
        // SelectedProject backing field. On re-activation, the VM needs to
        // restore from the cross-module context. Changed doesn't fire because
        // context.Current is unchanged — so we call OnActivated explicitly.
        newValue.OnActivated();
    }
}
