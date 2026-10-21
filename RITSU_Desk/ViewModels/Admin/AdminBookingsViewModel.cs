using System.IO;
using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Admin;

public sealed class AdminBookingsViewModel : BookingListViewModelBase
{
    internal const int MaxExportPages = 50;

    private readonly IRitsuApiClient _api;
    private readonly IPlatformService _platform;
    private readonly INotificationService _notifications;
    private IReadOnlyList<Option<Guid?>> _methodOptions;
    private Option<Guid?> _selectedMethod;
    private bool _methodsLoaded;
    private bool _isExporting;

    public AdminBookingsViewModel(IRitsuApiClient api, IPlatformService platform, INotificationService notifications, Func<DateTimeOffset>? now = null)
        : base("Pemesanan", "Semua pemesanan RITSU (hanya baca)", now)
    {
        _api = api;
        _platform = platform;
        _notifications = notifications;
        _methodOptions = [new("Semua metode", null)];
        _selectedMethod = _methodOptions[0];
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsExporting);
    }

    public IReadOnlyList<Option<Guid?>> PaymentMethodOptions
    {
        get => _methodOptions;
        private set => SetProperty(ref _methodOptions, value);
    }

    public Option<Guid?> SelectedPaymentMethod
    {
        get => _selectedMethod;
        set
        {
            if (value is not null && SetProperty(ref _selectedMethod, value)) ApplyFilterChange();
        }
    }

    public override bool HasActiveFilters => base.HasActiveFilters || SelectedPaymentMethod.Value is not null;

    protected override string EmptyTextNoFilters => "Belum ada pemesanan.";

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value)) CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand ExportCommand { get; }

    protected override BookingQuery BuildQuery(int page, int pageSize) =>
        base.BuildQuery(page, pageSize) with { PaymentMethodId = SelectedPaymentMethod.Value };

    protected override async Task<PagedResult<BookingDto>> FetchAsync(BookingQuery query, CancellationToken ct)
    {
        if (!_methodsLoaded) await LoadPaymentMethodsAsync(ct);
        return await _api.GetAdminBookingsAsync(query, ct);
    }

    protected override Task<BookingDto> FetchDetailAsync(Guid id, CancellationToken ct) =>
        _api.GetAdminBookingAsync(id, ct);

    protected override void ResetExtraFilters() => _selectedMethod = PaymentMethodOptions[0];

    private async Task LoadPaymentMethodsAsync(CancellationToken ct)
    {
        try
        {
            var methods = await _api.GetPaymentMethodsAsync(ct);
            PaymentMethodOptions =
            [
                new("Semua metode", null),
                .. methods.Select(m => new Option<Guid?>(m.Name, m.Id)),
            ];
            _selectedMethod = PaymentMethodOptions.FirstOrDefault(o => o.Value == _selectedMethod.Value) ?? PaymentMethodOptions[0];
            OnPropertyChanged(nameof(SelectedPaymentMethod));
            _methodsLoaded = true;
        }
        catch (ApiException ex) when (ex.Code != ApiErrorCodes.Unauthorized)
        {
            AppLog.Warn($"Payment method filter unavailable: {ex.Code}");
        }
    }

    private async Task ExportAsync()
    {
        var path = _platform.AskSaveCsvPath($"pemesanan-ritsu-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        if (path is null) return;

        IsExporting = true;
        try
        {
            var rows = new List<BookingDto>();
            var total = 0;
            for (var page = 1; page <= MaxExportPages; page++)
            {
                var result = await _api.GetAdminBookingsAsync(BuildQuery(page, PagerViewModel.MaxPageSize));
                rows.AddRange(result.Items);
                total = result.Total;
                if (rows.Count >= total || result.Items.Count == 0) break;
            }

            await Csv.WriteAsync(path, BookingCsv.Header, rows.Select(BookingCsv.Row));

            var name = System.IO.Path.GetFileName(path);
            if (rows.Count < total)
            {
                _notifications.Warning($"Hanya {Format.Number(rows.Count)} dari {Format.Number(total)} pemesanan diekspor ke {name}. Persempit filter untuk mengekspor sisanya.");
            }
            else
            {
                _notifications.Success($"{Format.Number(rows.Count)} pemesanan diekspor ke {name}.");
            }
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.Unauthorized)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("CSV export failed", ex);
            _notifications.Error("File CSV tidak dapat disimpan. Pastikan file tidak sedang dibuka di aplikasi lain.");
        }
        catch (Exception ex)
        {
            _notifications.Error(ErrorMessages.For(ex));
        }
        finally
        {
            IsExporting = false;
        }
    }
}

public static class BookingCsv
{
    public static readonly IReadOnlyList<string> Header =
    [
        "ID", "Dibuat (WIB)", "Titik jemput", "Tujuan", "Jarak (km)", "Harga (Rp)", "Metode pembayaran",
        "Stage", "Status stage", "Status bayar", "Penumpang", "Driver", "Selesai (WIB)",
    ];

    public static IReadOnlyList<string?> Row(BookingDto b) =>
    [
        b.Id.ToString(),
        Wib(b.CreatedAt),
        Format.Location(b.Pickup),
        Format.Location(b.Destination),
        Csv.Number(b.DistanceKm),
        Csv.Number(b.Price),
        b.PaymentMethod?.Name,
        b.Stage.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Labels.Stage(b.Stage),
        Labels.Paid(b.PaymentStatus),
        b.Customer?.Username,
        b.Driver?.Username,
        b.DeliveryEndTime is { } end ? Wib(end) : null,
    ];

    private static string Wib(DateTimeOffset value) =>
        Format.ToWib(value).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
