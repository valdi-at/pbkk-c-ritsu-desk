using RITSU_Desk.Api;
using RITSU_Desk.Services;
using RITSU_Desk.ViewModels.Admin;
using RITSU_Desk.ViewModels.Student;

namespace RITSU_Desk.ViewModels;

public sealed class PageFactory
{
    private readonly IRitsuApiClient _api;
    private readonly ISessionService _session;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly IPlatformService _platform;

    public PageFactory(
        IRitsuApiClient api,
        ISessionService session,
        ISettingsService settings,
        IThemeService theme,
        IDialogService dialogs,
        INotificationService notifications,
        IPlatformService platform)
    {
        _api = api;
        _session = session;
        _settings = settings;
        _theme = theme;
        _dialogs = dialogs;
        _notifications = notifications;
        _platform = platform;
    }

    public Action<NavKey> Navigate { get; set; } = _ => { };

    public PageViewModel MyDashboard() => new MyDashboardViewModel(_api, _session, key => Navigate(key));

    public PageViewModel MyBookings() => new MyBookingsViewModel(_api, _session);

    public PageViewModel DriverStatus() => new DriverStatusViewModel(_api, _settings, _platform);

    public PageViewModel Profile() => new ProfileViewModel(_api, _session, _settings, _theme);

    public PageViewModel AdminDashboard() => new AdminDashboardViewModel(_api, key => Navigate(key));

    public PageViewModel Users() => new UsersViewModel(_api, _session, _dialogs, _notifications);

    public PageViewModel Applications() => new ApplicationsViewModel(_api, _dialogs, _notifications);

    public PageViewModel AdminBookings() => new AdminBookingsViewModel(_api, _platform, _notifications);

    public PageViewModel PaymentMethods() => new PaymentMethodsViewModel(_api);

    public PageViewModel Finance() => new FinanceViewModel(_api, _platform, _notifications);
}
