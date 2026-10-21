using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RITSU_Desk.Converters;

public sealed class IsVisibleConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            ICollection c => c.Count > 0,
            _ => true,
        };
        return visible != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class UpperCaseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string)?.ToUpper(culture) ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
