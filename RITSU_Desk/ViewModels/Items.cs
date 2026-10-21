using System.Windows;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels;

public sealed record Option<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

public sealed class BookingItem
{
    public BookingItem(BookingDto dto) => Dto = dto;

    public BookingDto Dto { get; }

    public Guid Id => Dto.Id;

    public string ShortId => Format.ShortId(Dto.Id);

    public string CreatedText => Format.DateTime(Dto.CreatedAt);

    public DateTimeOffset CreatedAt => Dto.CreatedAt;

    public string CreatedDateText => Format.Date(Dto.CreatedAt);

    public string CreatedTimeText => Format.Time(Dto.CreatedAt);

    public string PickupText => Format.Location(Dto.Pickup);

    public string DestinationText => Format.Location(Dto.Destination);

    public string RouteText => $"{PickupText} → {DestinationText}";

    public IReadOnlyList<string> StopTexts => (Dto.Stops ?? []).Select(Format.Location).ToList();

    public bool HasStops => Dto.Stops is { Count: > 0 };

    public string DistanceText => Format.Km(Dto.DistanceKm);

    public decimal Price => Dto.Price;

    public string PriceText => Format.Rupiah(Dto.Price);

    public string PaymentMethodText => Dto.PaymentMethod?.Name ?? Format.Dash;

    public string StageLabel => Labels.Stage(Dto.Stage);

    public BadgeKind StageKind => Labels.StageKind(Dto.Stage);

    public string PaidLabel => Labels.Paid(Dto.PaymentStatus);

    public BadgeKind PaidKind => Labels.PaidKind(Dto.PaymentStatus);

    public string CustomerName => Dto.Customer?.Username ?? Format.Dash;

    public string DriverName => Dto.Driver?.Username ?? "Belum ada driver";

    public string DriverLine => Dto.Driver?.Username is { } name ? $"Driver: {name}" : "Belum ada driver";

    public string DriverReadyText => Format.DateTime(Dto.DriverReadyAt);

    public string StartText => Format.DateTime(Dto.DeliveryStartTime);

    public string EndText => Format.DateTime(Dto.DeliveryEndTime);

    public string UpdatedText => Format.DateTime(Dto.UpdatedAt);
}

public sealed class UserItem
{
    public UserItem(UserDto dto, Guid? currentUserId)
    {
        Dto = dto;
        IsSelf = currentUserId == dto.Id;
    }

    public UserDto Dto { get; }

    public Guid Id => Dto.Id;

    public string Username => Dto.Username;

    public string Email => Dto.Email;

    public string Initial => string.IsNullOrEmpty(Dto.Username) ? "?" : Dto.Username[..1].ToUpperInvariant();

    public string RoleLabel => Labels.Role(Dto.Role);

    public BadgeKind RoleKind => Labels.RoleKind(Dto.Role);

    public string JoinedText => Format.Date(Dto.CreatedAt);

    public string UpdatedText => Format.DateTime(Dto.UpdatedAt);

    public bool IsSelf { get; }

    public string DisplayName => IsSelf ? $"{Dto.Username} (Anda)" : Dto.Username;
}

public sealed class ApplicationItem
{
    public ApplicationItem(DriverApplicationDto dto) => Dto = dto;

    public DriverApplicationDto Dto { get; }

    public Guid Id => Dto.Id;

    public string Username => Dto.Username;

    public string Email => Dto.Email;

    public string Phone => string.IsNullOrWhiteSpace(Dto.PhoneNumber) ? Format.Dash : Dto.PhoneNumber;

    public string StatusLabel => Labels.ApplicationStatus(Dto.Status);

    public BadgeKind StatusKind => Labels.ApplicationStatusKind(Dto.Status);

    public bool IsPending => Dto.Status == ApplicationStatuses.Pending;

    public bool IsDeclined => Dto.Status == ApplicationStatuses.Declined;

    public string SubmittedText => Format.DateTime(Dto.CreatedAt);

    public string ReviewedText => Dto.ReviewedAt is null ? "Belum ditinjau" : Format.DateTime(Dto.ReviewedAt);

    public string? AdminMessage => string.IsNullOrWhiteSpace(Dto.AdminMessage) ? null : Dto.AdminMessage;
}

public sealed class ActivityItem
{
    public ActivityItem(ActivityItemDto dto, DateTimeOffset now)
    {
        Dto = dto;
        WhenText = Format.Relative(dto.At, now);
    }

    public ActivityItemDto Dto { get; }

    public string Title => Labels.Activity(Dto.Type);

    public string Actor => Dto.Actor?.Username ?? "Pengguna terhapus";

    public string Glyph => Labels.ActivityGlyph(Dto.Type);

    public BadgeKind Kind => Labels.ActivityKind(Dto.Type);

    public string WhenText { get; }

    public string AtText => Format.DateTime(Dto.At);
}

public sealed class ChartPoint
{
    public ChartPoint(string label, decimal value, string valueText, string toolTip, double ratio)
    {
        Label = label;
        Value = value;
        ValueText = valueText;
        ToolTip = toolTip;
        Ratio = ratio;
    }

    public string Label { get; }

    public decimal Value { get; }

    public string ValueText { get; }

    public string ToolTip { get; }

    public double Ratio { get; }

    public GridLength FillLength => new(Ratio, GridUnitType.Star);

    public GridLength RestLength => new(1 - Ratio, GridUnitType.Star);

    public static IReadOnlyList<ChartPoint> Scale<T>(
        IEnumerable<T> source,
        Func<T, string> label,
        Func<T, decimal> value,
        Func<T, string> valueText,
        Func<T, string> toolTip)
    {
        var items = source.ToList();
        var max = items.Count == 0 ? 0m : items.Max(value);
        return items.Select(item =>
        {
            var v = value(item);
            var ratio = max <= 0 ? 0 : Math.Max((double)(v / max), v > 0 ? 0.02 : 0);
            return new ChartPoint(label(item), v, valueText(item), toolTip(item), Math.Clamp(ratio, 0, 1));
        }).ToList();
    }
}

public sealed record StatCardModel(string Title, string Value, string Caption, BadgeKind Kind);
