namespace ClaudePM.App.ViewModels;

/// <summary>Base for any view model shown in the main content area / sidebar.</summary>
public abstract class PageViewModel : ViewModelBase
{
    public abstract string Title { get; }
    public abstract string Glyph { get; }
    public abstract string Description { get; }

    /// <summary>Planned-capability bullets shown by stub module views.</summary>
    public virtual IReadOnlyList<string> Highlights => Array.Empty<string>();
}
