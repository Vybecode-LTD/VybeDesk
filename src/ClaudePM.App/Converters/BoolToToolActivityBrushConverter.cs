using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ClaudePM.App.Converters;

/// <summary>
/// Maps the <c>Success</c> bool on a Notebook ToolActivity chip to a colour
/// — soft green for success, soft red for failure / blocked.
/// </summary>
public sealed class BoolToToolActivityBrushConverter : IValueConverter
{
    private static readonly IBrush Success = new SolidColorBrush(Color.Parse("#7FD18B"));
    private static readonly IBrush Failure = new SolidColorBrush(Color.Parse("#E08585"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Success : Failure;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
