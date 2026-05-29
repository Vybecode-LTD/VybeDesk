using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VybeDesk.Core.Models;

namespace VybeDesk.App.Converters;

/// <summary>
/// Maps a severity to a chip colour. Originally for
/// <see cref="FindingSeverity"/>; also accepts <see cref="BugSeverity"/> so
/// the Documentation findings and the Bug Tracker speak the same colour
/// language (red / amber / blue). Resolves brushes from the Stratum theme
/// dictionary so light/dark variants work automatically.
/// </summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            FindingSeverity.Critical => ResolveBrush("StratumDanger"),
            FindingSeverity.Warning  => ResolveBrush("StratumWarn"),
            FindingSeverity.Info     => ResolveBrush("StratumInfo"),
            BugSeverity.Critical     => ResolveBrush("StratumDanger"),
            BugSeverity.Major        => ResolveBrush("StratumWarn"),
            BugSeverity.Minor        => ResolveBrush("StratumInfo"),
            AlignmentRank.OffTrack   => ResolveBrush("StratumDanger"),
            AlignmentRank.AtRisk     => ResolveBrush("StratumWarn"),
            AlignmentRank.OnTrack    => ResolveBrush("StratumInfo"),
            _                        => Brushes.Gray,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static IBrush ResolveBrush(string key)
    {
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var v) == true
            && v is IBrush b)
            return b;
        return Brushes.Gray;
    }
}
