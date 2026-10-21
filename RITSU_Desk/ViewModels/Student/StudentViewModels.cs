using System.Collections.ObjectModel;
using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Student;

public sealed class SideSwitch : ObservableObject
{
    private Option<string> _selected;

    public SideSwitch(string role, Action changed)
    {
        IsAvailable = role == Roles.Driver;
        Options = [new("Sebagai driver", BookingSides.Driver), new("Sebagai penumpang", BookingSides.Customer)];
        _selected = IsAvailable ? Options[0] : Options[1];
        Changed = changed;
    }

    private Action Changed { get; }

    public bool IsAvailable { get; }

    public IReadOnlyList<Option<string>> Options { get; }

    public Option<string> Selected
    {
        get => _selected;
        set
        {
            if (value is null || !SetProperty(ref _selected, value)) return;
            OnPropertyChanged(nameof(IsDriverSide));
            Changed();
        }
    }

    public string Side => Selected.Value;

    public bool IsDriverSide => Side == BookingSides.Driver;
}

public sealed class MyDashboardViewModel : PageViewModel
{
    private readonly IRitsuApiClient _api;
    private IReadOnlyList<ChartPoint> _monthly = [];
    private IReadOnlyList<StatCardModel> _cards = [];

    public MyDashboardViewModel(IRitsuApiClient api, ISessionService session, Action<NavKey> navigate)
        : base("Dashboard Saya", "Ringkasan perjalanan dan pembayaran Anda")
    {
        _api = api;
        Username = session.User?.Username ?? string.Empty;
        Side = new SideSwitch(session.User?.Role ?? Roles.Customer, () => _ = RefreshAsync());
        OpenHistoryCommand = new RelayCommand(() => navigate(NavKey.MyBookings));
    }

    public string Greeting => $"Halo, {Username}";

    public string Username { get; }

    public SideSwitch Side { get; }

    public IReadOnlyList<StatCardModel> Cards
    {
        get => _cards;
        private set => SetProperty(ref _cards, value);
    }

    public IReadOnlyList<ChartPoint> Monthly
    {
        get => _monthly;
        private set => SetProperty(ref _monthly, value);
    }

    public ObservableCollection<BookingItem> RecentTrips { get; } = [];

    public bool HasRecentTrips => RecentTrips.Count > 0;

    public string ChartTitle => Side.IsDriverSide ? "Perjalanan yang Anda antar per bulan" : "Perjalanan Anda per bulan";

    public string RecentTitle => Side.IsDriverSide ? "Perjalanan terbaru sebagai driver" : "Perjalanan terbaru";

    public ICommand OpenHistoryCommand { get; }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var side = Side.Side;
        var summaryTask = _api.GetMySummaryAsync(side, ct);
        var recentTask = _api.GetMyBookingsAsync(new BookingQuery { Side = side, Page = 1, PageSize = 5 }, ct);
        await Task.WhenAll(summaryTask, recentTask);
        ct.ThrowIfCancellationRequested();

        var s = summaryTask.Result;
        var driver = side == BookingSides.Driver;
        Cards =
        [
            new("Total perjalanan", Format.Number(s.TotalTrips), "Semua stage", BadgeKind.Primary),
            new("Perjalanan selesai", Format.Number(s.CompletedTrips), "Stage 5", BadgeKind.Success),
            driver
                ? new("Total pendapatan", Format.Rupiah(s.PaidAmount), "Dari perjalanan yang sudah dibayar", BadgeKind.Success)
                : new("Total pengeluaran", Format.Rupiah(s.PaidAmount), "Dari perjalanan yang sudah dibayar", BadgeKind.Success),
            new("Belum dibayar", Format.Rupiah(s.UnpaidAmount), $"{Format.Number(s.UnpaidCount)} perjalanan stage 4–5", BadgeKind.Warning),
        ];

        Monthly = ChartPoint.Scale(
            s.Monthly,
            m => Format.MonthShort(m.Month),
            m => m.Count,
            m => m.Count == 0 ? string.Empty : Format.Number(m.Count),
            m => $"{Format.Month(m.Month, shortName: false)}\n{Format.Number(m.Count)} perjalanan\n{Format.Rupiah(m.Amount)} {(driver ? "pendapatan" : "dibayar")}");

        RecentTrips.Clear();
        foreach (var b in recentTask.Result.Items) RecentTrips.Add(new BookingItem(b));

        OnPropertyChanged(nameof(HasRecentTrips));
        OnPropertyChanged(nameof(ChartTitle));
        OnPropertyChanged(nameof(RecentTitle));
    }
}

public sealed class MyBookingsViewModel : BookingListViewModelBase
{
    private readonly IRitsuApiClient _api;

    public MyBookingsViewModel(IRitsuApiClient api, ISessionService session, Func<DateTimeOffset>? now = null)
        : base("Riwayat Perjalanan", "Semua perjalanan Anda, termasuk yang masih berjalan", now)
    {
        _api = api;
        Side = new SideSwitch(session.User?.Role ?? Roles.Customer, () =>
        {
            SelectedItem = null;
            ApplyFilterChange();
        });
    }

    public SideSwitch Side { get; }

    public override bool ShowCustomerColumn => Side.IsDriverSide;

    public override bool ShowDriverColumn => !Side.IsDriverSide;

    public override bool ShowPartiesColumn => false;

    protected override string EmptyTextNoFilters => Side.IsDriverSide
        ? "Anda belum pernah mengantar penumpang."
        : "Anda belum pernah memesan perjalanan. Pesan lewat RITSU web.";

    protected override BookingQuery BuildQuery(int page, int pageSize) =>
        base.BuildQuery(page, pageSize) with { Side = Side.Side };

    protected override Task<PagedResult<BookingDto>> FetchAsync(BookingQuery query, CancellationToken ct) =>
        _api.GetMyBookingsAsync(query, ct);

    protected override Task<BookingDto> FetchDetailAsync(Guid id, CancellationToken ct) =>
        _api.GetMyBookingAsync(id, ct);

    protected override void OnStateChanged()
    {
        base.OnStateChanged();
        OnPropertyChanged(nameof(ShowCustomerColumn));
        OnPropertyChanged(nameof(ShowDriverColumn));
    }
}

public sealed class DriverStatusViewModel : PageViewModel
{
    private readonly IRitsuApiClient _api;
    private readonly ISettingsService _settings;
    private ApplicationItem? _application;

    public DriverStatusViewModel(IRitsuApiClient api, ISettingsService settings, IPlatformService platform)
        : base("Status Driver", "Hasil pengajuan Anda untuk menjadi driver RITSU")
    {
        _api = api;
        _settings = settings;
        OpenWebCommand = new RelayCommand(() => platform.OpenInBrowser(WebUri));
    }

    public ApplicationItem? Application
    {
        get => _application;
        private set
        {
            if (!SetProperty(ref _application, value)) return;
            OnPropertyChanged(nameof(HasApplication));
            OnPropertyChanged(nameof(HasNoApplication));
            OnPropertyChanged(nameof(StatusHeadline));
            OnPropertyChanged(nameof(StatusDescription));
        }
    }

    public bool HasApplication => Application is not null;

    public bool HasNoApplication => HasLoaded && !HasError && Application is null;

    public string StatusHeadline => Application?.Dto.Status switch
    {
        ApplicationStatuses.Pending => "Pengajuan sedang ditinjau",
        ApplicationStatuses.Approved => "Pengajuan disetujui",
        ApplicationStatuses.Declined => "Pengajuan ditolak",
        _ => string.Empty,
    };

    public string StatusDescription => Application?.Dto.Status switch
    {
        ApplicationStatuses.Pending => "Admin RITSU akan meninjau data dan foto SIM Anda. Muat ulang halaman ini untuk melihat hasilnya.",
        ApplicationStatuses.Approved => "Akun Anda sudah menjadi driver. Terima pesanan lewat RITSU web.",
        ApplicationStatuses.Declined => "Perbaiki data sesuai alasan dari admin, lalu ajukan lagi lewat RITSU web.",
        _ => string.Empty,
    };

    public Uri WebUri => new(ServerAddress.WebOrigin(_settings.ApiBaseUri), "driver/application");

    public string WebUrlText => WebUri.ToString();

    public ICommand OpenWebCommand { get; }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var dto = await _api.GetMyDriverApplicationAsync(ct);
        ct.ThrowIfCancellationRequested();
        Application = dto is null ? null : new ApplicationItem(dto);
    }

    protected override void OnStateChanged() => OnPropertyChanged(nameof(HasNoApplication));
}

public sealed class ProfileViewModel : PageViewModel
{
    private readonly IRitsuApiClient _api;
    private readonly ISessionService _session;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private UserItem? _user;
    private MetaDto? _meta;
    private string? _metaError;

    public ProfileViewModel(IRitsuApiClient api, ISessionService session, ISettingsService settings, IThemeService theme)
        : base("Profil & Pengaturan", "Data akun RITSU dan preferensi aplikasi")
    {
        _api = api;
        _session = session;
        _settings = settings;
        _theme = theme;
        if (session.User is { } u) _user = new UserItem(u, null);

        _theme.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
        };
    }

    public UserItem? User
    {
        get => _user;
        private set => SetProperty(ref _user, value);
    }

    public bool IsLightTheme
    {
        get => _theme.Current == AppTheme.Light;
        set
        {
            if (value && _theme.Current != AppTheme.Light) _theme.Apply(AppTheme.Light);
        }
    }

    public bool IsDarkTheme
    {
        get => _theme.Current == AppTheme.Dark;
        set
        {
            if (value && _theme.Current != AppTheme.Dark) _theme.Apply(AppTheme.Dark);
        }
    }

    public string ServerText => ServerAddress.Display(_settings.ApiBaseUri);

    public string ApiBaseText => _settings.ApiBaseUri.ToString();

    public string SessionExpiresText => Format.DateTime(_session.ExpiresAt);

    public MetaDto? Meta
    {
        get => _meta;
        private set
        {
            if (!SetProperty(ref _meta, value)) return;
            OnPropertyChanged(nameof(ApiVersionText));
            OnPropertyChanged(nameof(SchemaText));
            OnPropertyChanged(nameof(ServerTimeText));
            OnPropertyChanged(nameof(DbTimezoneText));
        }
    }

    public string? MetaError
    {
        get => _metaError;
        private set => SetProperty(ref _metaError, value);
    }

    public string ApiVersionText => Meta is null ? Format.Dash : $"v{Meta.ApiVersion}";

    public string SchemaText => Meta?.SchemaVersion ?? Format.Dash;

    public string ServerTimeText => Meta is null ? Format.Dash : Format.DateTime(Meta.ServerTime);

    public string DbTimezoneText => Meta?.DbTimezone ?? Format.Dash;

    public string AppVersionText => typeof(ProfileViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public string LogPathText => AppLog.FilePath ?? Format.Dash;

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var metaTask = LoadMetaAsync(ct);
        var user = await _api.GetCurrentUserAsync(ct);
        ct.ThrowIfCancellationRequested();

        User = new UserItem(user, null);
        _session.UpdateUser(user);
        OnPropertyChanged(nameof(SessionExpiresText));
        await metaTask;
    }

    private async Task LoadMetaAsync(CancellationToken ct)
    {
        try
        {
            Meta = await _api.GetMetaAsync(ct);
            MetaError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MetaError = ErrorMessages.For(ex);
        }
    }
}
