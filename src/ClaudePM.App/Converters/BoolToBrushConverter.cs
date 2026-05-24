using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ClaudePM.App.Converters;

/// <summary>
/// Maps a bool to one of two colours. <c>ConverterParameter</c> is a
/// pipe-separated <c>"true|false"</c> hex string, e.g. <c>"#9ABEE0|#444"</c>.
/// Used by the Testing Manager wizard progress dots and any future
/// answered/unanswered visual indicator.
/// </summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || parameter is not string spec)
            return new SolidColorBrush(Colors.Gray);

        var parts = spec.Split('|');
        if (parts.Length != 2)
            return new SolidColorBrush(Colors.Gray);

        return new SolidColorBrush(Color.Parse(b ? parts[0] : parts[1]));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
