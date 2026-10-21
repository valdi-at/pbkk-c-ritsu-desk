using System.Windows;
using System.Windows.Controls;
using RITSU_Desk.Services;

namespace RITSU_Desk.Controls;

public sealed class IconChip : Border
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(IconChip), new PropertyMetadata(string.Empty, (d, e) => ((IconChip)d)._icon.Text = (string?)e.NewValue ?? string.Empty));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(BadgeKind), typeof(IconChip), new PropertyMetadata(BadgeKind.Neutral, (d, _) => ((IconChip)d).ApplyKind()));

    private readonly TextBlock _icon;

    public IconChip()
    {
        Width = 34;
        Height = 34;
        CornerRadius = new CornerRadius(12);
        VerticalAlignment = VerticalAlignment.Center;
        _icon = new TextBlock
        {
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _icon.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Icons");
        Child = _icon;
        ApplyKind();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public BadgeKind Kind
    {
        get => (BadgeKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private void ApplyKind()
    {
        SetResourceReference(BackgroundProperty, $"Soft.{Kind}.Bg");
        _icon.SetResourceReference(TextBlock.ForegroundProperty, $"Soft.{Kind}.Fg");
    }
}
