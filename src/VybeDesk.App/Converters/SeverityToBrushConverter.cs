using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VybeDesk.Core.Models;

namespace VybeDesk.App.Converters;

/// <summary>
/// Maps a severity to a chip colour. Originally for
/// <see cref="FindingSeverity"/>; also accepts <see cref="BugSeverity"/> so
/// the Documentation findings and the Bug Tracker speak the same colour
/// language (red / amber / blue).
/// </summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Red = new(Color.Parse("#E06C6C"));
    private static readonly SolidColorBrush Amber = new(Color.Parse("#E0A95E"));
    private static readonly SolidColorBrush Blue = new(Color.Parse("#5E8FE0"));
    private static readonly SolidColorBrush Gray = new(Colors.Gray);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            FindingSeverity.Critical => Red,
            FindingSeverity.Warning => Amber,
            FindingSeverity.Info => Blue,
            BugSeverity.Critical => Red,
            BugSeverity.Major => Amber,
            BugSeverity.Minor => Blue,
            AlignmentRank.OffTrack => Red,
            AlignmentRank.AtRisk => Amber,
            AlignmentRank.OnTrack => Blue,
            _ => Gray,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
