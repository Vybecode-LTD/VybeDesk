using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ClaudePM.Core.Services;
using AvaloniaFilePickerFileType = Avalonia.Platform.Storage.FilePickerFileType;
using CoreFilePickerFileType = ClaudePM.Core.Services.FilePickerFileType;

namespace ClaudePM.App.Services;

/// <summary>
/// IFilePickerService backed by Avalonia's StorageProvider. The active
/// MainWindow is resolved lazily so the picker survives reassignment and so
/// ViewModels never see an Avalonia type.
/// </summary>
public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickFolderAsync(string? title = null, string? startLocation = null)
    {
        var top = GetTopLevel();
        if (top is null) return null;

        var result = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title ?? "Pick a folder",
            AllowMultiple = false,
            SuggestedStartLocation = await TryGetStartFolderAsync(top, startLocation),
        });

        return result.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickFileAsync(
        string? title = null,
        string? startLocation = null,
        IReadOnlyList<CoreFilePickerFileType>? filters = null)
    {
        var top = GetTopLevel();
        if (top is null) return null;

        var result = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Pick a file",
            AllowMultiple = false,
            FileTypeFilter = MapFilters(filters),
            SuggestedStartLocation = await TryGetStartFolderAsync(top, startLocation),
        });

        return result.FirstOrDefault()?.TryGetLocalPath();
    }

    private static TopLevel? GetTopLevel()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    private static async Task<IStorageFolder?> TryGetStartFolderAsync(TopLevel top, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return await top.StorageProvider.TryGetFolderFromPathAsync(path); }
        catch { return null; }
    }

    private static IReadOnlyList<AvaloniaFilePickerFileType>? MapFilters(
        IReadOnlyList<CoreFilePickerFileType>? filters)
    {
        if (filters is null || filters.Count == 0) return null;
        return filters
            .Select(f => new AvaloniaFilePickerFileType(f.Name) { Patterns = f.Patterns.ToList() })
            .ToList();
    }
}
