using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Services;
using RITSU_Desk.ViewModels;

namespace RITSU_Desk;

public partial class App : Application
{
    private HttpClient? _http;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RITSU Desk");
        var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RITSU Desk");
        AppLog.Initialize(Path.Combine(localData, "logs"));
        AppLog.Info("RITSU Desk starting");

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        var settings = new SettingsService(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(appData, "settings.json"));
        var theme = new ThemeService(settings);
        theme.Apply(settings.Theme);

        var session = new SessionService();
        _http = RitsuApiClient.CreateHttpClient();
        var api = new RitsuApiClient(_http, session, () => settings.ApiBaseUri);

        var main = new MainViewModel(api, session, settings, theme, new DialogService(), new NotificationService(), new PlatformService());
        var window = new MainWindow { DataContext = main };
        MainWindow = window;
        main.Start();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _http?.Dispose();
        AppLog.Info("RITSU Desk closed");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            "Terjadi kesalahan tak terduga. Aplikasi tetap berjalan; coba ulangi tindakan terakhir.\n\nDetail teknis tersimpan di log aplikasi.",
            "RITSU Desk",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
