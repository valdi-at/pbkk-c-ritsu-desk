using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace RITSU_Desk.Controls;

public static class FocusOnLoad
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(FocusOnLoad), new PropertyMetadata(false, OnChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true) return;
        element.Loaded += (_, _) =>
            element.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                element.Focus();
                Keyboard.Focus(element);
            });
    }
}
