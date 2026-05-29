using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using VybeDesk.Core.Services;

namespace VybeDesk.App.Services;

/// <summary>
/// IClipboardService backed by Avalonia's <c>TopLevel.Clipboard</c>.
/// Resolves the active MainWindow lazily so ViewModels never see an
/// Avalonia type.
/// </summary>
public sealed class AvaloniaClipboardService : IClipboardService
{
    public async Task<bool> SetTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        var top = Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (top?.Clipboard is null) return false;

        try
        {
            await top.Clipboard.SetTextAsync(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
