using System.Windows;
using System.Windows.Controls;

namespace RITSU_Desk.Controls;

public partial class BookingFilterBar : UserControl
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(BookingFilterBar), new PropertyMetadata("Cari lokasi atau nama..."));

    public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(
        nameof(Extra), typeof(object), typeof(BookingFilterBar));

    public BookingFilterBar() => InitializeComponent();

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public object? Extra
    {
        get => GetValue(ExtraProperty);
        set => SetValue(ExtraProperty, value);
    }
}
