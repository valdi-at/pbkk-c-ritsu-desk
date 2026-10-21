using System.Collections.ObjectModel;
using System.Text.Json;
using RITSU_Desk.Api;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Admin;

public sealed class PaymentMethodItem
{
    public PaymentMethodItem(PaymentMethodDto dto) => Dto = dto;

    public PaymentMethodDto Dto { get; }

    public string Name => Dto.Name;

    public bool IsCash => Labels.IsCash(Dto.Name);

    public string TypeLabel => IsCash ? "Tunai" : "Nontunai";

    public BadgeKind TypeKind => IsCash ? BadgeKind.Neutral : BadgeKind.Info;

    public string BookingCountText => Format.Number(Dto.BookingCount);

    public string CreatedText => Format.Date(Dto.CreatedAt);

    public string PaymentInfoText => Describe(Dto.PaymentInfo);

    private static string Describe(JsonElement? info)
    {
        if (info is not { } e || e.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return Format.Dash;
        if (e.ValueKind != JsonValueKind.Object) return e.ToString();

        var parts = e.EnumerateObject()
            .Select(p => $"{p.Name}: {(p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } s ? s : p.Value.ValueKind == JsonValueKind.String ? "(kosong)" : p.Value.ToString())}")
            .ToList();
        return parts.Count == 0 ? Format.Dash : string.Join(", ", parts);
    }
}

public sealed class PaymentMethodsViewModel : PageViewModel
{
    private readonly IRitsuApiClient _api;

    public PaymentMethodsViewModel(IRitsuApiClient api)
        : base("Metode Pembayaran", "Metode yang bisa dipilih penumpang dan jumlah pemakaiannya")
    {
        _api = api;
    }

    public ObservableCollection<PaymentMethodItem> Items { get; } = [];

    public bool IsEmpty => HasLoaded && !IsBusy && !HasError && Items.Count == 0;

    public string TotalBookingsText => Format.Number(Items.Sum(i => i.Dto.BookingCount));

    public string CountText => Format.Number(Items.Count);

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var methods = await _api.GetPaymentMethodsAsync(ct);
        ct.ThrowIfCancellationRequested();

        Items.Clear();
        foreach (var m in methods) Items.Add(new PaymentMethodItem(m));
        OnPropertyChanged(nameof(TotalBookingsText));
        OnPropertyChanged(nameof(CountText));
        OnStateChanged();
    }

    protected override void OnStateChanged() => OnPropertyChanged(nameof(IsEmpty));
}
