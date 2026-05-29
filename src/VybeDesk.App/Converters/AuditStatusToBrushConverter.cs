using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VybeDesk.Core.Models;

namespace VybeDesk.App.Converters;

/// <summary>Maps an <see cref="AuditItemStatus"/> to a status-badge brush.
/// Resolves from Stratum theme tokens for light/dark support.</summary>
public sealed class AuditStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            AuditItemStatus.Complete   => ResolveBrush("StratumSuccess"),
            AuditItemStatus.Incomplete => ResolveBrush("StratumWarn"),
            _                          => ResolveBrush("StratumText3"),
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
