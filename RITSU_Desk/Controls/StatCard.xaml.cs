using System.Windows;
using System.Windows.Controls;
using RITSU_Desk.Services;

namespace RITSU_Desk.Controls;

public partial class StatCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CaptionProperty =
        DependencyProperty.Register(nameof(Caption), typeof(string), typeof(StatCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(BadgeKind), typeof(StatCard), new PropertyMetadata(BadgeKind.Primary, (d, _) => ((StatCard)d).ApplyKind()));

    public StatCard()
    {
        InitializeComponent();
        ApplyKind();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public BadgeKind Kind
    {
        get => (BadgeKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private void ApplyKind()
    {
        if (Chip is null) return;
        Chip.SetResourceReference(Border.BackgroundProperty, $"Soft.{Kind}.Bg");
        ValueText.SetResourceReference(TextBlock.ForegroundProperty, $"Soft.{Kind}.Fg");
    }
}
