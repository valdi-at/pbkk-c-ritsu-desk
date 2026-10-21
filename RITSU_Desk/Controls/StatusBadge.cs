using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RITSU_Desk.Services;

namespace RITSU_Desk.Controls;

public sealed class StatusBadge : Border
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata(string.Empty, (d, e) => ((StatusBadge)d)._label.Text = (string?)e.NewValue ?? string.Empty));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(BadgeKind), typeof(StatusBadge), new PropertyMetadata(BadgeKind.Neutral, (d, _) => ((StatusBadge)d).ApplyKind()));

    private readonly TextBlock _label;

    public StatusBadge()
    {
        Padding = new Thickness(9, 2, 9, 3);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        SnapsToDevicePixels = true;

        _label = new TextBlock
        {
            FontSize = 11.5,
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        _label.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Text");

        Child = _label;

        ApplyKind();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeKind Kind
    {
        get => (BadgeKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        CornerRadius = new CornerRadius(sizeInfo.NewSize.Height / 2);
    }

    private void ApplyKind()
    {
        SetResourceReference(BackgroundProperty, $"Badge.{Kind}.Bg");
        _label.SetResourceReference(TextBlock.ForegroundProperty, $"Badge.{Kind}.Fg");
    }
}
