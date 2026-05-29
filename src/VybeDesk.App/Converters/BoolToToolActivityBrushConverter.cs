using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace VybeDesk.App.Converters;

/// <summary>
/// Maps the <c>Success</c> bool on a Notebook ToolActivity chip to a colour
/// — soft green for success, soft red for failure / blocked. Resolves from
/// Stratum theme tokens for light/dark support.
/// </summary>
public sealed class BoolToToolActivityBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? ResolveBrush("StratumSuccess") : ResolveBrush("StratumDanger");

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
