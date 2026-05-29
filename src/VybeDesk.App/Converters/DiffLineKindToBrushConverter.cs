using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VybeDesk.App.ViewModels;

namespace VybeDesk.App.Converters;

/// <summary>Maps a <see cref="DiffLineKind"/> to a translucent row background.
/// Resolves from Stratum theme tokens for light/dark support.</summary>
public sealed class DiffLineKindToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DiffLineKind.Inserted => ResolveBrush("StratumSuccessBg"),
            DiffLineKind.Deleted  => ResolveBrush("StratumDangerBg"),
            _                     => Brushes.Transparent,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static IBrush ResolveBrush(string key)
    {
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var v) == true
            && v is IBrush b)
            return b;
        return Brushes.Transparent;
    }
}
