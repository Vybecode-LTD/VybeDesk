using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ClaudePM.App.ViewModels;

namespace ClaudePM.App.Views;

public partial class SessionBuilderView : UserControl
{
    public SessionBuilderView()
    {
        InitializeComponent();
        FileDropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        FileDropZone.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not SessionBuilderViewModel vm) return;
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles();
        if (files is null) return;

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToList();

        if (paths.Count > 0) vm.AddFiles(paths);
    }
}
