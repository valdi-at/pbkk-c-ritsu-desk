using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RITSU_Desk.Controls;

public sealed class LogoMark : Border
{
    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush), typeof(Brush), typeof(LogoMark), new PropertyMetadata(Brushes.White, (d, e) => ((LogoMark)d)._icon.Stroke = (Brush)e.NewValue));

    private static readonly Geometry Mark = Geometry.Parse(
        "M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12 Z M12,2 V5 M12,19 V22 M2,12 H5 M19,12 H22 " +
        "M6.34,6.34 L8.46,8.46 M15.54,15.54 L17.66,17.66 M6.34,17.66 L8.46,15.54 M15.54,8.46 L17.66,6.34");

    private readonly Path _icon;

    public LogoMark()
    {
        Width = 40;
        Height = 40;
        CornerRadius = new CornerRadius(14);
        SetResourceReference(BackgroundProperty, "Logo.Background");

        _icon = new Path
        {
            Data = Mark,
            Stroke = IconBrush,
            StrokeThickness = 2.2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = 24,
            Height = 24,
            Stretch = Stretch.None,
        };
        Child = new Viewbox { Child = new Canvas { Width = 24, Height = 24, Children = { _icon } } };
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Padding = new Thickness(Math.Round(sizeInfo.NewSize.Width * 0.2));
    }

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }
}
