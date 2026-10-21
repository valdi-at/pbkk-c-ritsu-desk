using System.Text.Json;
using System.Text.Json.Serialization;

namespace RITSU_Desk.Models;

public sealed record MetaDto(int ApiVersion, string? SchemaVersion, DateTimeOffset ServerTime, string? DbTimezone);

public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

public sealed record UserEnvelope(UserDto User);

[JsonConverter(typeof(LocationDtoConverter))]
public sealed record LocationDto(double? Lat, double? Lng, string? Name, string Raw);

public sealed record PartyDto(Guid Id, string? Username);

public sealed record PaymentMethodRefDto(Guid Id, string Name);

public sealed record BookingDto(
    Guid Id,
    int Stage,
    LocationDto? Pickup,
    LocationDto? Destination,
    IReadOnlyList<LocationDto>? Stops,
    decimal DistanceKm,
    decimal Price,
    bool PaymentStatus,
    PaymentMethodRefDto? PaymentMethod,
    PartyDto? Customer,
    PartyDto? Driver,
    DateTimeOffset? DriverReadyAt,
    DateTimeOffset? DeliveryStartTime,
    DateTimeOffset? DeliveryEndTime,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record BookingEnvelope(BookingDto Booking);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record ItemsEnvelope<T>(IReadOnlyList<T> Items);

public sealed record MonthlyTripsDto(string Month, int Count, decimal Amount);

public sealed record MeSummaryDto(
    string As,
    int TotalTrips,
    int CompletedTrips,
    decimal PaidAmount,
    int UnpaidCount,
    decimal UnpaidAmount,
    IReadOnlyList<MonthlyTripsDto> Monthly);

public sealed record DriverApplicationDto(
    Guid Id,
    Guid UserId,
    string Username,
    string Email,
    string PhoneNumber,
    string Status,
    string? AdminMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt,
    string? LicenseUrl);

public sealed record ApplicationEnvelope(DriverApplicationDto? Application);

public sealed record UserDependenciesDto(int BookingsAsCustomer, int BookingsAsDriver, int ChatMessages)
{
    public int TotalBookings => BookingsAsCustomer + BookingsAsDriver;
}

public sealed record UserDetailDto(UserDto User, UserDependenciesDto Dependencies, DriverApplicationDto? LatestApplication);

public sealed record PeriodVolumeDto(int Count, int PaidCount);

public sealed record BookingVolumeDto(PeriodVolumeDto Today, PeriodVolumeDto Week, PeriodVolumeDto Month);

public sealed record RevenueStatsDto(decimal Total, int PaidCount, decimal Average);

public sealed record OutstandingDto(decimal Amount, int Count);

public sealed record PaymentMethodRevenueDto(Guid Id, string Name, string? IconLink, decimal Revenue, int Count);

public sealed record DailyRevenueDto(string Day, decimal Revenue, int Count);

public sealed record TopDriverDto(Guid Id, string Username, decimal TotalEarned, int BookingCount);

public sealed record AdminDashboardDto(
    int TotalUsers,
    IReadOnlyDictionary<string, int> UsersByRole,
    IReadOnlyDictionary<string, int> ApplicationsByStatus,
    RevenueStatsDto Revenue,
    OutstandingDto Outstanding,
    BookingVolumeDto BookingVolume,
    IReadOnlyList<PaymentMethodRevenueDto> RevenueByPaymentMethod,
    IReadOnlyList<DailyRevenueDto> DailyRevenue,
    IReadOnlyList<TopDriverDto> TopDrivers);

public sealed record RevenueCountDto(decimal Revenue, int Count);

public sealed record MonthlyRevenueDto(string Month, decimal Revenue, int Count);

public sealed record PaymentBreakdownDto(Guid Id, string Name, decimal Revenue, int Count);

public sealed record RecentPaidBookingDto(Guid Id, decimal Price, DateTimeOffset CreatedAt, string? PaymentMethod);

public sealed record FinanceDto(
    RevenueCountDto AllTime,
    RevenueCountDto ThisMonth,
    RevenueCountDto ThisYear,
    OutstandingDto Outstanding,
    decimal AveragePerBooking,
    IReadOnlyList<MonthlyRevenueDto> MonthlyTrend,
    IReadOnlyList<PaymentBreakdownDto> PaymentBreakdown,
    IReadOnlyList<RecentPaidBookingDto> RecentPaidBookings);

public sealed record ActivityItemDto(string Type, DateTimeOffset At, PartyDto? Actor, Guid EntityId);

public sealed record PaymentMethodDto(
    Guid Id,
    string Name,
    string? IconLink,
    JsonElement? PaymentInfo,
    DateTimeOffset CreatedAt,
    int BookingCount);

public sealed record ApiErrorBody(ApiErrorDetail? Error);

public sealed record ApiErrorDetail(string? Code, string? Message, JsonElement? Details);
