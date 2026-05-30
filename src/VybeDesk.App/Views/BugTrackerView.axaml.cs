using Avalonia.Controls;
using VybeDesk.App.ViewModels;
using VybeDesk.Core.Models;

namespace VybeDesk.App.Views;

public partial class BugTrackerView : UserControl
{
    public BugTrackerView() => InitializeComponent();

    private void OnBugSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not BugTrackerViewModel vm) return;

        vm.SelectedBugs.Clear();
        if (BugList.SelectedItems is null) return;
        foreach (var item in BugList.SelectedItems)
        {
            if (item is Bug bug)
                vm.SelectedBugs.Add(bug);
        }
    }
}
