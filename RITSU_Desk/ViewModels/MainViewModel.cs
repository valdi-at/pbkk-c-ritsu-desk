using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IRitsuApiClient _api;
    private readonly ISessionService _session;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IPlatformService _platform;
    private readonly SynchronizationContext? _ui;
    private object? _current;

    public MainViewModel(
        IRitsuApiClient api,
        ISessionService session,
        ISettingsService settings,
        IThemeService theme,
        DialogService dialogs,
        NotificationService notifications,
        IPlatformService platform)
    {
        _api = api;
        _session = session;
        _settings = settings;
        _theme = theme;
        _platform = platform;
        Dialogs = dialogs;
        Notifications = notifications;
        _ui = SynchronizationContext.Current;

        _session.Expired += (_, _) => OnUi(() => ShowLogin(ErrorMessages.SessionExpired));
        _session.UserChanged += (_, _) => OnUi(OnUserChanged);
    }

    public DialogService Dialogs { get; }

    public NotificationService Notifications { get; }

    public object? Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public void Start() => ShowLogin(null);

    private void ShowLogin(string? message)
    {
        Dialogs.CloseAll();
        if (Current is ShellViewModel shell) shell.Detach();

        var login = new LoginViewModel(_api, _session, _settings, _platform, message);
        login.LoggedIn += (_, _) => ShowShell();
        Current = login;
        _ = login.CheckServerAsync();
    }

    private void ShowShell()
    {
        var user = _session.User;
        if (user is null)
        {
            ShowLogin(null);
            return;
        }

        if (Current is ShellViewModel old) old.Detach();
        var pages = new PageFactory(_api, _session, _settings, _theme, Dialogs, Notifications, _platform);
        Current = new ShellViewModel(user, pages, _session, _theme, LogoutAsync);
    }

    private async Task LogoutAsync()
    {
        if (_session.IsAuthenticated)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _api.LogoutAsync(cts.Token);
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Logout request failed: {ex.Message}");
            }
        }
        _session.Clear();
        ShowLogin("Anda sudah logout.");
    }

    private void OnUserChanged()
    {
        if (Current is not ShellViewModel shell || _session.User is not { } user) return;

        if (user.Role != shell.Role)
        {
            ShowShell();
            Notifications.Show(ToastKind.Info, $"Role Anda sekarang {Labels.Role(user.Role)}. Menu sudah disesuaikan.");
        }
        else
        {
            shell.UpdateUser(user);
        }
    }

    private void OnUi(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }
}
