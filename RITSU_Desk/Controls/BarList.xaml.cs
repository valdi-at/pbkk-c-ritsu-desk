using System.Windows;
using System.Windows.Controls;
using RITSU_Desk.ViewModels;

namespace RITSU_Desk.Controls;

public partial class BarList : UserControl
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<ChartPoint>), typeof(BarList),
        new PropertyMetadata(null, (d, _) => ((BarList)d).UpdateEmpty()));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(BarList), new PropertyMetadata("Belum ada data untuk ditampilkan."));

    public BarList()
    {
        InitializeComponent();
        UpdateEmpty();
    }

    public IReadOnlyList<ChartPoint>? Points
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    private void UpdateEmpty()
    {
        if (List is null) return;
        var empty = Points is null || Points.Count == 0;
        List.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }
}
