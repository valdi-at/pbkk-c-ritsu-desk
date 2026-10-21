using System.Collections.ObjectModel;
using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Admin;

public sealed class TopDriverItem
{
    public TopDriverItem(int rank, TopDriverDto dto)
    {
        Rank = rank;
        Dto = dto;
    }

    public int Rank { get; }

    public TopDriverDto Dto { get; }

    public string Username => Dto.Username;

    public string Initial => string.IsNullOrEmpty(Dto.Username) ? "?" : Dto.Username[..1].ToUpperInvariant();

    public string EarnedText => Format.Rupiah(Dto.TotalEarned);

    public string TripsText => $"{Format.Number(Dto.BookingCount)} perjalanan lunas";
}

public sealed class AdminDashboardViewModel : PageViewModel
{
    private const int ActivityLimit = 15;

    private readonly IRitsuApiClient _api;
    private readonly Func<DateTimeOffset> _now;
    private IReadOnlyList<StatCardModel> _cards = [];
    private IReadOnlyList<ChartPoint> _dailyRevenue = [];
    private IReadOnlyList<ChartPoint> _methodRevenue = [];
    private string? _activityError;
    private int _pendingApplications;

    public AdminDashboardViewModel(IRitsuApiClient api, Action<NavKey> navigate, Func<DateTimeOffset>? now = null)
        : base("Dashboard Admin", "Kondisi operasional RITSU saat ini")
    {
        _api = api;
        _now = now ?? (() => DateTimeOffset.Now);
        OpenApplicationsCommand = new RelayCommand(() => navigate(NavKey.Applications));
        OpenFinanceCommand = new RelayCommand(() => navigate(NavKey.Finance));
    }

    public IReadOnlyList<StatCardModel> Cards
    {
        get => _cards;
        private set => SetProperty(ref _cards, value);
    }

    public IReadOnlyList<ChartPoint> DailyRevenue
    {
        get => _dailyRevenue;
        private set => SetProperty(ref _dailyRevenue, value);
    }

    public IReadOnlyList<ChartPoint> MethodRevenue
    {
        get => _methodRevenue;
        private set => SetProperty(ref _methodRevenue, value);
    }

    public ObservableCollection<TopDriverItem> TopDrivers { get; } = [];

    public bool HasTopDrivers => TopDrivers.Count > 0;

    public ObservableCollection<ActivityItem> Activity { get; } = [];

    public bool HasActivity => Activity.Count > 0;

    public string? ActivityError
    {
        get => _activityError;
        private set => SetProperty(ref _activityError, value);
    }

    public int PendingApplications
    {
        get => _pendingApplications;
        private set
        {
            if (!SetProperty(ref _pendingApplications, value)) return;
            OnPropertyChanged(nameof(HasPendingApplications));
            OnPropertyChanged(nameof(PendingApplicationsText));
        }
    }

    public bool HasPendingApplications => PendingApplications > 0;

    public string PendingApplicationsText => $"{Format.Number(PendingApplications)} pengajuan driver menunggu peninjauan";

    public ICommand OpenApplicationsCommand { get; }

    public ICommand OpenFinanceCommand { get; }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var activityTask = LoadActivityAsync(ct);
        var d = await _api.GetAdminDashboardAsync(ct);
        ct.ThrowIfCancellationRequested();

        int Role(string r) => d.UsersByRole.TryGetValue(r, out var n) ? n : 0;
        int Apps(string s) => d.ApplicationsByStatus.TryGetValue(s, out var n) ? n : 0;

        PendingApplications = Apps(ApplicationStatuses.Pending);
        Cards =
        [
            new("Total pengguna", Format.Number(d.TotalUsers),
                $"{Format.Number(Role(Roles.Customer))} penumpang · {Format.Number(Role(Roles.Driver))} driver · {Format.Number(Role(Roles.Admin))} admin",
                BadgeKind.Primary),
            new("Pengajuan menunggu", Format.Number(Apps(ApplicationStatuses.Pending)),
                $"{Format.Number(Apps(ApplicationStatuses.Approved))} disetujui · {Format.Number(Apps(ApplicationStatuses.Declined))} ditolak",
                BadgeKind.Warning),
            new("Total pendapatan", Format.Rupiah(d.Revenue.Total),
                $"Dari {Format.Number(d.Revenue.PaidCount)} pemesanan lunas", BadgeKind.Success),
            new("Outstanding", Format.Rupiah(d.Outstanding.Amount),
                $"{Format.Number(d.Outstanding.Count)} perjalanan belum dibayar", BadgeKind.Danger),
            new("Pemesanan hari ini", Format.Number(d.BookingVolume.Today.Count),
                $"{Format.Number(d.BookingVolume.Today.PaidCount)} sudah lunas", BadgeKind.Neutral),
            new("Pemesanan minggu ini", Format.Number(d.BookingVolume.Week.Count),
                $"{Format.Number(d.BookingVolume.Week.PaidCount)} sudah lunas", BadgeKind.Neutral),
            new("Pemesanan bulan ini", Format.Number(d.BookingVolume.Month.Count),
                $"{Format.Number(d.BookingVolume.Month.PaidCount)} sudah lunas", BadgeKind.Neutral),
            new("Rata-rata nilai pemesanan", Format.Rupiah(d.Revenue.Average),
                "Dari pemesanan lunas", BadgeKind.Primary),
        ];

        DailyRevenue = ChartPoint.Scale(
            Enumerable.Reverse(d.DailyRevenue),
            r => Format.Day(r.Day),
            r => r.Revenue,
            r => Format.Compact(r.Revenue),
            r => $"{Format.Day(r.Day, withYear: true)}\n{Format.Rupiah(r.Revenue)}\n{Format.Number(r.Count)} pemesanan lunas");

        MethodRevenue = ChartPoint.Scale(
            d.RevenueByPaymentMethod,
            m => m.Name,
            m => m.Revenue,
            m => Format.Rupiah(m.Revenue),
            m => $"{m.Name}: {Format.Number(m.Count)} pemesanan lunas");

        TopDrivers.Clear();
        var rank = 1;
        foreach (var t in d.TopDrivers) TopDrivers.Add(new TopDriverItem(rank++, t));
        OnPropertyChanged(nameof(HasTopDrivers));

        await activityTask;
    }

    private async Task LoadActivityAsync(CancellationToken ct)
    {
        try
        {
            var items = await _api.GetActivityAsync(ActivityLimit, ct);
            ct.ThrowIfCancellationRequested();
            var now = _now();
            Activity.Clear();
            foreach (var a in items) Activity.Add(new ActivityItem(a, now));
            ActivityError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Activity.Clear();
            ActivityError = ErrorMessages.For(ex);
        }
        OnPropertyChanged(nameof(HasActivity));
    }
}
