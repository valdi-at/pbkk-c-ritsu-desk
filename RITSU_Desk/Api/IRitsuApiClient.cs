using RITSU_Desk.Models;

namespace RITSU_Desk.Api;

public interface IRitsuApiClient
{
    Task<MetaDto> GetMetaAsync(CancellationToken ct = default);

    Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default);

    Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default);

    Task LogoutAsync(CancellationToken ct = default);

    Task<MeSummaryDto> GetMySummaryAsync(string? side, CancellationToken ct = default);

    Task<PagedResult<BookingDto>> GetMyBookingsAsync(BookingQuery query, CancellationToken ct = default);

    Task<BookingDto> GetMyBookingAsync(Guid id, CancellationToken ct = default);

    Task<DriverApplicationDto?> GetMyDriverApplicationAsync(CancellationToken ct = default);

    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default);

    Task<FinanceDto> GetFinanceAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ActivityItemDto>> GetActivityAsync(int limit, CancellationToken ct = default);

    Task<PagedResult<UserDto>> GetUsersAsync(UserQuery query, CancellationToken ct = default);

    Task<UserDetailDto> GetUserAsync(Guid id, CancellationToken ct = default);

    Task<UserDto> ChangeUserRoleAsync(Guid id, string role, CancellationToken ct = default);

    Task DeleteUserAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<DriverApplicationDto>> GetApplicationsAsync(ApplicationQuery query, CancellationToken ct = default);

    Task<DriverApplicationDto?> ApproveApplicationAsync(Guid id, CancellationToken ct = default);

    Task<DriverApplicationDto?> RejectApplicationAsync(Guid id, string reason, CancellationToken ct = default);

    Task<byte[]> GetLicenseImageAsync(string licenseUrl, CancellationToken ct = default);

    Task<PagedResult<BookingDto>> GetAdminBookingsAsync(BookingQuery query, CancellationToken ct = default);

    Task<BookingDto> GetAdminBookingAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken ct = default);
}
