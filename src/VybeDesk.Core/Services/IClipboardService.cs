namespace VybeDesk.Core.Services;

/// <summary>
/// Writes text to the OS clipboard. Implemented in the App layer because
/// it needs Avalonia's TopLevel. Returns false when the clipboard is
/// unavailable (e.g. no active window) rather than throwing.
/// </summary>
public interface IClipboardService
{
    Task<bool> SetTextAsync(string text);
}
