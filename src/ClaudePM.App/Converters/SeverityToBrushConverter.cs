using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ClaudePM.Core.Models;

namespace ClaudePM.App.Converters;

/// <summary>Maps a <see cref="FindingSeverity"/> to a chip colour.</summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            FindingSeverity.Critical => new SolidColorBrush(Color.Parse("#E06C6C")),
            FindingSeverity.Warning => new SolidColorBrush(Color.Parse("#E0A95E")),
            FindingSeverity.Info => new SolidColorBrush(Color.Parse("#5E8FE0")),
            _ => new SolidColorBrush(Colors.Gray),
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
