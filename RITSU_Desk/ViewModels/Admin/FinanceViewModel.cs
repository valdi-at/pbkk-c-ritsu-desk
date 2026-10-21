using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Admin;

public sealed class PaymentShareItem
{
    public PaymentShareItem(PaymentBreakdownDto dto, decimal totalRevenue)
    {
        Dto = dto;
        Share = totalRevenue > 0 ? dto.Revenue / totalRevenue : 0;
    }

    public PaymentBreakdownDto Dto { get; }

    public string Name => Dto.Name;

    public decimal Share { get; }

    public double ShareRatio => (double)Share;

    public string RevenueText => Format.Rupiah(Dto.Revenue);

    public string CountText => $"{Format.Number(Dto.Count)} transaksi";

    public string ShareText => Format.Percent(Share);
}

public sealed class RecentPaidItem
{
    public RecentPaidItem(RecentPaidBookingDto dto) => Dto = dto;

    public RecentPaidBookingDto Dto { get; }

    public string ShortId => Format.ShortId(Dto.Id);

    public string CreatedText => Format.DateTime(Dto.CreatedAt);

    public string MethodText => Dto.PaymentMethod ?? Format.Dash;

    public string PriceText => Format.Rupiah(Dto.Price);
}

public sealed class FinanceViewModel : PageViewModel
{
    private readonly IRitsuApiClient _api;
    private readonly IPlatformService _platform;
    private readonly INotificationService _notifications;
    private FinanceDto? _finance;
    private IReadOnlyList<TopDriverDto> _topDriverDtos = [];
    private IReadOnlyList<StatCardModel> _cards = [];
    private IReadOnlyList<ChartPoint> _monthlyTrend = [];
    private string? _topDriversError;

    public FinanceViewModel(IRitsuApiClient api, IPlatformService platform, INotificationService notifications)
        : base("Laporan Keuangan", "Pendapatan, piutang, dan sebaran metode pembayaran")
    {
        _api = api;
        _platform = platform;
        _notifications = notifications;
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => _finance is not null);
    }

    public IReadOnlyList<StatCardModel> Cards
    {
        get => _cards;
        private set => SetProperty(ref _cards, value);
    }

    public IReadOnlyList<ChartPoint> MonthlyTrend
    {
        get => _monthlyTrend;
        private set => SetProperty(ref _monthlyTrend, value);
    }

    public ObservableCollection<PaymentShareItem> PaymentBreakdown { get; } = [];

    public ObservableCollection<TopDriverItem> TopDrivers { get; } = [];

    public bool HasTopDrivers => TopDrivers.Count > 0;

    public string? TopDriversError
    {
        get => _topDriversError;
        private set => SetProperty(ref _topDriversError, value);
    }

    public ObservableCollection<RecentPaidItem> RecentPaid { get; } = [];

    public bool HasRecentPaid => RecentPaid.Count > 0;

    public ICommand ExportCommand { get; }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var topTask = LoadTopDriversAsync(ct);
        var f = await _api.GetFinanceAsync(ct);
        ct.ThrowIfCancellationRequested();
        _finance = f;

        Cards =
        [
            new("Total pendapatan", Format.Rupiah(f.AllTime.Revenue), $"{Format.Number(f.AllTime.Count)} pemesanan lunas", BadgeKind.Success),
            new("Bulan ini", Format.Rupiah(f.ThisMonth.Revenue), $"{Format.Number(f.ThisMonth.Count)} pemesanan lunas", BadgeKind.Primary),
            new("Tahun ini", Format.Rupiah(f.ThisYear.Revenue), $"{Format.Number(f.ThisYear.Count)} pemesanan lunas", BadgeKind.Primary),
            new("Outstanding", Format.Rupiah(f.Outstanding.Amount), $"{Format.Number(f.Outstanding.Count)} perjalanan belum dibayar", BadgeKind.Danger),
            new("Rata-rata per pemesanan", Format.Rupiah(f.AveragePerBooking), "Dari pemesanan lunas", BadgeKind.Primary),
        ];

        MonthlyTrend = ChartPoint.Scale(
            Enumerable.Reverse(f.MonthlyTrend),
            m => Format.MonthShort(m.Month),
            m => m.Revenue,
            m => Format.Compact(m.Revenue),
            m => $"{Format.Month(m.Month, shortName: false)}\n{Format.Rupiah(m.Revenue)}\n{Format.Number(m.Count)} pemesanan lunas");

        var totalRevenue = f.PaymentBreakdown.Sum(p => p.Revenue);
        PaymentBreakdown.Clear();
        foreach (var p in f.PaymentBreakdown) PaymentBreakdown.Add(new PaymentShareItem(p, totalRevenue));

        RecentPaid.Clear();
        foreach (var r in f.RecentPaidBookings) RecentPaid.Add(new RecentPaidItem(r));
        OnPropertyChanged(nameof(HasRecentPaid));

        await topTask;
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task LoadTopDriversAsync(CancellationToken ct)
    {
        try
        {
            var dashboard = await _api.GetAdminDashboardAsync(ct);
            ct.ThrowIfCancellationRequested();
            _topDriverDtos = dashboard.TopDrivers;
            TopDrivers.Clear();
            var rank = 1;
            foreach (var t in dashboard.TopDrivers) TopDrivers.Add(new TopDriverItem(rank++, t));
            TopDriversError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TopDrivers.Clear();
            _topDriverDtos = [];
            TopDriversError = ErrorMessages.For(ex);
        }
        OnPropertyChanged(nameof(HasTopDrivers));
    }

    private async Task ExportAsync()
    {
        if (_finance is not { } f) return;

        var path = _platform.AskSaveCsvPath($"laporan-keuangan-ritsu-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        if (path is null) return;

        try
        {
            await Csv.WriteAsync(path, ["Bagian", "Keterangan", "Pendapatan (Rp)", "Jumlah"], BuildRows(f, _topDriverDtos));
            _notifications.Success($"Laporan keuangan diekspor ke {Path.GetFileName(path)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("CSV export failed", ex);
            _notifications.Error("File CSV tidak dapat disimpan. Pastikan file tidak sedang dibuka di aplikasi lain.");
        }
    }

    internal static IEnumerable<IReadOnlyList<string?>> BuildRows(FinanceDto f, IReadOnlyList<TopDriverDto> topDrivers)
    {
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);

        yield return ["Ringkasan", "Total pendapatan", Csv.Number(f.AllTime.Revenue), N(f.AllTime.Count)];
        yield return ["Ringkasan", "Bulan ini", Csv.Number(f.ThisMonth.Revenue), N(f.ThisMonth.Count)];
        yield return ["Ringkasan", "Tahun ini", Csv.Number(f.ThisYear.Revenue), N(f.ThisYear.Count)];
        yield return ["Ringkasan", "Outstanding", Csv.Number(f.Outstanding.Amount), N(f.Outstanding.Count)];
        yield return ["Ringkasan", "Rata-rata per pemesanan", Csv.Number(f.AveragePerBooking), null];
        foreach (var m in Enumerable.Reverse(f.MonthlyTrend)) yield return ["Tren bulanan", m.Month, Csv.Number(m.Revenue), N(m.Count)];
        foreach (var p in f.PaymentBreakdown) yield return ["Metode pembayaran", p.Name, Csv.Number(p.Revenue), N(p.Count)];
        foreach (var t in topDrivers) yield return ["Top driver", t.Username, Csv.Number(t.TotalEarned), N(t.BookingCount)];
    }
}
