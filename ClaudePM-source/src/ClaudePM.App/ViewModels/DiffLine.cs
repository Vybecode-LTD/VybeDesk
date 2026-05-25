namespace ClaudePM.App.ViewModels;

/// <summary>One line of an inline diff between two versions of a prompt body.</summary>
public sealed record DiffLine(string Text, DiffLineKind Kind)
{
    /// <summary>Glyph for the diff gutter (+ / − / space).</summary>
    public string Marker => Kind switch
    {
        DiffLineKind.Inserted => "+",
        DiffLineKind.Deleted  => "-",
        _                     => " ",
    };
}

public enum DiffLineKind
{
    Unchanged,
    Inserted,
    Deleted,
}
