using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VybeDesk.App.ViewModels;

namespace VybeDesk.App.Converters;

/// <summary>Maps a <see cref="DiffLineKind"/> to a translucent row background.</summary>
public sealed class DiffLineKindToBrushConverter : IValueConverter
{
    private static readonly IBrush Inserted = new SolidColorBrush(Color.Parse("#1F3D24"));
    private static readonly IBrush Deleted  = new SolidColorBrush(Color.Parse("#4A1F25"));
    private static readonly IBrush Unchanged = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DiffLineKind.Inserted => Inserted,
            DiffLineKind.Deleted  => Deleted,
            _                     => Unchanged,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
