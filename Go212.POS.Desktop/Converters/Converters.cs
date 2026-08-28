using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Go212.POS.Desktop.Converters;

/// <summary>Returns Collapsed if value is null or empty string, Visible otherwise.</summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null || (value is string s && string.IsNullOrEmpty(s))
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Returns Visible if bool is true, Collapsed otherwise.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (Visibility)value == Visibility.Visible;
}

/// <summary>Formats decimal amounts as MAD currency: 1 234,00 MAD</summary>
public class MadCurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d) return $"{d:N2} MAD";
        if (value is double db)  return $"{db:N2} MAD";
        return "0,00 MAD";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Inverts a boolean value.</summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

/// <summary>Returns Visible if bool is false, Collapsed if true.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Formats a DateTime to French locale date string.</summary>
public class FrenchDateConverter : IValueConverter
{
    private static readonly CultureInfo Fr = new("fr-FR");

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            var format = parameter as string ?? "dd/MM/yyyy HH:mm";
            return dt.ToLocalTime().ToString(format, Fr);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Maps SaleStatus enum to a color brush name for status badges.</summary>
public class SaleStatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Domain.Enums.SaleStatus status)
        {
            return status switch
            {
                Domain.Enums.SaleStatus.Completed  => "#FF10B981",  // Green
                Domain.Enums.SaleStatus.Open       => "#FF3B82F6",  // Blue
                Domain.Enums.SaleStatus.Cancelled  => "#FFEF4444",  // Red
                Domain.Enums.SaleStatus.Refunded   => "#FFF59E0B",  // Yellow
                _ => "#FF94A3B8"
            };
        }
        return "#FF94A3B8";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
