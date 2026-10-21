using System.Text.RegularExpressions;
using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels;

public enum ServerStatus
{
    Checking,
    Online,
    Offline,
    Incompatible,
}

public sealed partial class LoginViewModel : ObservableObject
{
    public const int SupportedApiVersion = 1;

    private readonly IRitsuApiClient _api;
    private readonly ISessionService _session;
    private readonly ISettingsService _settings;
    private readonly IPlatformService _platform;

    private string _email = string.Empty;
    private string _password = string.Empty;
    private string? _errorMessage;
    private string? _infoMessage;
    private string? _emailError;
    private string? _passwordError;
    private bool _isBusy;
    private ServerStatus _serverStatus = ServerStatus.Checking;
    private string _serverStatusText = "Memeriksa server...";
    private bool _isEditingServer;
    private string _serverInput = string.Empty;
    private string? _serverError;
    private CancellationTokenSource? _metaCts;

    public LoginViewModel(IRitsuApiClient api, ISessionService session, ISettingsService settings, IPlatformService platform, string? infoMessage = null)
    {
        _api = api;
        _session = session;
        _settings = settings;
        _platform = platform;
        _infoMessage = infoMessage;

        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy && ServerStatus != ServerStatus.Incompatible);
        RetryServerCommand = new AsyncRelayCommand(CheckServerAsync);
        EditServerCommand = new RelayCommand(() =>
        {
            ServerInput = _settings.ApiBaseUri.ToString().TrimEnd('/');
            ServerError = null;
            IsEditingServer = true;
        });
        CancelServerCommand = new RelayCommand(() => IsEditingServer = false);
        SaveServerCommand = new AsyncRelayCommand(SaveServerAsync);
        OpenSignupCommand = new RelayCommand(() => _platform.OpenInBrowser(new Uri(ServerAddress.WebOrigin(_settings.ApiBaseUri), "auth/register")));
    }

    public event EventHandler? LoggedIn;

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value)) EmailError = null;
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value)) PasswordError = null;
        }
    }

    public string? EmailError
    {
        get => _emailError;
        private set => SetProperty(ref _emailError, value);
    }

    public string? PasswordError
    {
        get => _passwordError;
        private set => SetProperty(ref _passwordError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string? InfoMessage
    {
        get => _infoMessage;
        private set => SetProperty(ref _infoMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ServerStatus ServerStatus
    {
        get => _serverStatus;
        private set
        {
            if (SetProperty(ref _serverStatus, value)) CommandManager.InvalidateRequerySuggested();
        }
    }

    public string ServerStatusText
    {
        get => _serverStatusText;
        private set => SetProperty(ref _serverStatusText, value);
    }

    public string ServerAddressText => ServerAddress.Display(_settings.ApiBaseUri);

    public bool IsEditingServer
    {
        get => _isEditingServer;
        set => SetProperty(ref _isEditingServer, value);
    }

    public string ServerInput
    {
        get => _serverInput;
        set
        {
            if (SetProperty(ref _serverInput, value)) ServerError = null;
        }
    }

    public string? ServerError
    {
        get => _serverError;
        private set => SetProperty(ref _serverError, value);
    }

    public IReadOnlyList<string> KnownServers => _settings.KnownServers;

    public ICommand LoginCommand { get; }

    public ICommand RetryServerCommand { get; }

    public ICommand EditServerCommand { get; }

    public ICommand CancelServerCommand { get; }

    public ICommand SaveServerCommand { get; }

    public ICommand OpenSignupCommand { get; }

    public event EventHandler? PasswordCleared;

    public async Task CheckServerAsync()
    {
        _metaCts?.Cancel();
        var cts = _metaCts = new CancellationTokenSource();

        ServerStatus = ServerStatus.Checking;
        ServerStatusText = "Memeriksa server...";
        try
        {
            var meta = await _api.GetMetaAsync(cts.Token);
            if (cts.IsCancellationRequested) return;

            if (meta.ApiVersion == SupportedApiVersion)
            {
                ServerStatus = ServerStatus.Online;
                ServerStatusText = $"Terhubung · API v{meta.ApiVersion}";
                if (ErrorMessage is not null && ErrorMessage.StartsWith("Versi API", StringComparison.Ordinal)) ErrorMessage = null;
            }
            else
            {
                ServerStatus = ServerStatus.Incompatible;
                ServerStatusText = $"API v{meta.ApiVersion} tidak didukung";
                ErrorMessage = $"Versi API server (v{meta.ApiVersion}) tidak cocok dengan RITSU Desk (v{SupportedApiVersion}). Perbarui aplikasi atau hubungi admin.";
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ServerStatus = ServerStatus.Offline;
            ServerStatusText = ex is ApiException { Kind: ApiErrorKind.InvalidResponse } ? "Bukan server RITSU" : "Tidak terjangkau";
        }
    }

    private async Task SaveServerAsync()
    {
        if (!ServerAddress.TryNormalize(ServerInput, out var uri, out var error))
        {
            ServerError = error;
            return;
        }
        _settings.SetApiBaseUri(uri!);
        IsEditingServer = false;
        ErrorMessage = null;
        OnPropertyChanged(nameof(ServerAddressText));
        await CheckServerAsync();
    }

    private async Task LoginAsync()
    {
        InfoMessage = null;
        ErrorMessage = null;
        if (!Validate()) return;

        IsBusy = true;
        try
        {
            if (ServerStatus != ServerStatus.Online)
            {
                await CheckServerAsync();
                if (ServerStatus == ServerStatus.Incompatible) return;
            }

            var login = await _api.LoginAsync(Email.Trim(), Password);
            Password = string.Empty;
            PasswordCleared?.Invoke(this, EventArgs.Empty);

            _session.Start(login);
            AppLog.Info($"Login succeeded, role {login.User.Role}");
            LoggedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ErrorMessages.For(ex);
            if (ex.IsConnectionProblem)
            {
                ServerStatus = ServerStatus.Offline;
                ServerStatusText = "Tidak terjangkau";
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Login failed unexpectedly", ex);
            ErrorMessage = ErrorMessages.For(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool Validate()
    {
        var email = Email.Trim();
        EmailError = email.Length == 0 ? "Email wajib diisi."
            : !EmailPattern().IsMatch(email) ? "Format email belum benar."
            : null;
        PasswordError = Password.Trim().Length == 0 ? "Password wajib diisi." : null;
        return EmailError is null && PasswordError is null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
