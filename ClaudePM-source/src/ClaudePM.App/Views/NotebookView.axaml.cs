using Avalonia.Controls;
using Avalonia.Input;
using ClaudePM.App.ViewModels;

namespace ClaudePM.App.Views;

public partial class NotebookView : UserControl
{
    public NotebookView() => InitializeComponent();

    private void OnTogglePathClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock { DataContext: ScopeProjectRow row })
            row.TogglePathCommand.Execute(null);
    }

    private void OnOpenExplorerClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock { DataContext: ScopeProjectRow row })
            row.OpenInExplorerCommand.Execute(null);
    }
}
