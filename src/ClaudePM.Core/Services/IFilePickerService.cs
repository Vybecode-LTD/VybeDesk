namespace ClaudePM.Core.Services;

/// <summary>
/// Native folder / file pickers. Implemented in the App layer because it needs
/// Avalonia's storage provider; ViewModels depend on this interface only.
/// All methods return null when the user cancels the dialog.
/// </summary>
public interface IFilePickerService
{
    /// <summary>Prompt for a folder. Returns the local path, or null if cancelled.</summary>
    Task<string?> PickFolderAsync(string? title = null, string? startLocation = null);

    /// <summary>Prompt for a single file. Returns the local path, or null if cancelled.</summary>
    Task<string?> PickFileAsync(
        string? title = null,
        string? startLocation = null,
        IReadOnlyList<FilePickerFileType>? filters = null);
}

/// <summary>A named group of file patterns for the file picker (e.g. "Markdown" + "*.md").</summary>
public sealed record FilePickerFileType(string Name, IReadOnlyList<string> Patterns);
