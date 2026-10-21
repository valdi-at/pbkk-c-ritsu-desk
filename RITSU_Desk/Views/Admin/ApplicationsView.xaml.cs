using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RITSU_Desk.ViewModels.Admin;

namespace RITSU_Desk.Views.Admin;

public partial class ApplicationsView : UserControl
{
    public ApplicationsView() => InitializeComponent();

    private void OnLicenseClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ApplicationsViewModel { LicenseImage: { } image, SelectedItem: { } item }) return;

        var window = new Window
        {
            Title = $"Foto SIM · {item.Username}",
            Owner = Window.GetWindow(this),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 900,
            Height = 640,
            ShowInTaskbar = false,
            Content = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new Image { Source = image, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(12) },
            },
        };
        window.SetResourceReference(BackgroundProperty, "App.Background");
        window.ShowDialog();
    }
}
