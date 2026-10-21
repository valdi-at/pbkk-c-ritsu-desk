using System.Windows;
using System.Windows.Controls;
using RITSU_Desk.ViewModels;

namespace RITSU_Desk.Controls;

public partial class ColumnChart : UserControl
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<ChartPoint>), typeof(ColumnChart),
        new PropertyMetadata(null, (d, _) => ((ColumnChart)d).UpdateEmpty()));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(ColumnChart), new PropertyMetadata("Belum ada data untuk ditampilkan."));

    public ColumnChart()
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
        if (Plot is null) return;
        var empty = Points is null || Points.Count == 0 || Points.All(p => p.Value == 0);
        Plot.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        Empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }
}
