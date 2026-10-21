namespace RITSU_Desk.Models;

public static class Roles
{
    public const string Customer = "customer";
    public const string Driver = "driver";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<string> All = [Customer, Driver, Admin];
}

public static class ApplicationStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Declined = "declined";
}

public static class BookingSides
{
    public const string Customer = "customer";
    public const string Driver = "driver";
}

public sealed record BookingQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Side { get; init; }
    public int? Stage { get; init; }
    public bool? Paid { get; init; }
    public Guid? PaymentMethodId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Search { get; init; }
}

public sealed record UserQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }
    public string? Role { get; init; }
}

public sealed record ApplicationQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Status { get; init; }
}
