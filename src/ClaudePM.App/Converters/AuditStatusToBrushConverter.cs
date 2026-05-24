using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ClaudePM.Core.Models;

namespace ClaudePM.App.Converters;

/// <summary>Maps an <see cref="AuditItemStatus"/> to a status-badge brush.</summary>
public sealed class AuditStatusToBrushConverter : IValueConverter
{
    private static readonly IBrush Complete   = new SolidColorBrush(Color.Parse("#7FD18B"));
    private static readonly IBrush Incomplete = new SolidColorBrush(Color.Parse("#E0A95E"));
    private static readonly IBrush Unknown    = new SolidColorBrush(Color.Parse("#7F7F8A"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            AuditItemStatus.Complete   => Complete,
            AuditItemStatus.Incomplete => Incomplete,
            _                          => Unknown,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
