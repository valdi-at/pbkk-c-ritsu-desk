using System.Net;
using System.Net.Http;
using System.Text;
using RITSU_Desk.Api;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.Tests;

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public List<(HttpMethod Method, Uri Uri, string? Authorization, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body));
        var response = _respond(request);
        response.RequestMessage = request;
        return response;
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Error(HttpStatusCode status, string code, string? details = null) =>
        Json(details is null
            ? $$$"""{"error":{"code":"{{{code}}}","message":"server text"}}"""
            : $$$"""{"error":{"code":"{{{code}}}","message":"server text","details":{{{details}}}}}""", status);
}

internal static class Api
{
    public static readonly Uri Base = new("http://10.10.0.1:3000/api/desktop/v1/");

    public static (RitsuApiClient Client, StubHandler Handler, SessionService Session) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond, bool loggedIn = true)
    {
        var handler = new StubHandler(respond);
        var session = new SessionService();
        if (loggedIn) session.Start(new LoginResponse("tok.en", DateTimeOffset.UtcNow.AddHours(12), Users.Admin));
        var client = new RitsuApiClient(new HttpClient(handler), session, () => Base);
        return (client, handler, session);
    }
}

internal static class Users
{
    public static readonly UserDto Admin = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "valdi", "valdi@its.ac.id", Roles.Admin,
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"), DateTimeOffset.Parse("2026-01-02T00:00:00Z"));

    public static readonly UserDto Customer = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "nadia", "nadia@its.ac.id", Roles.Customer,
        DateTimeOffset.Parse("2026-02-01T00:00:00Z"), DateTimeOffset.Parse("2026-02-02T00:00:00Z"));

    public static readonly UserDto Driver = new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "budi", "budi@its.ac.id", Roles.Driver,
        DateTimeOffset.Parse("2026-03-01T00:00:00Z"), DateTimeOffset.Parse("2026-03-02T00:00:00Z"));
}

internal static class Data
{
    public static BookingDto Booking(int stage = 5, bool paid = true, Guid? id = null) => new(
        id ?? Guid.NewGuid(), stage,
        new LocationDto(-7.28, 112.79, "Asrama ITS", "{}"),
        new LocationDto(-7.27, 112.78, "Perpustakaan", "{}"),
        [], 2.5m, 12000m, paid,
        new PaymentMethodRefDto(Guid.NewGuid(), "Cash"),
        new PartyDto(Users.Customer.Id, Users.Customer.Username),
        new PartyDto(Users.Driver.Id, Users.Driver.Username),
        null, null, null,
        DateTimeOffset.Parse("2026-10-21T08:00:00Z"), DateTimeOffset.Parse("2026-10-21T08:30:00Z"));

    public static DriverApplicationDto Application(string status = ApplicationStatuses.Pending, Guid? id = null) => new(
        id ?? Guid.NewGuid(), Users.Customer.Id, Users.Customer.Username, Users.Customer.Email, "08123", status,
        status == ApplicationStatuses.Declined ? "Foto buram" : null,
        DateTimeOffset.Parse("2026-10-20T08:00:00Z"), null, "/api/desktop/v1/admin/applications/x/license");

    public static PagedResult<T> Page<T>(params T[] items) => new(items, 1, 25, items.Length);
}

internal sealed class FakeApi : IRitsuApiClient
{
    public Func<MetaDto> Meta { get; set; } = () => new MetaDto(1, "005", DateTimeOffset.UtcNow, "UTC");
    public Func<string, string, LoginResponse> Login { get; set; } = (_, _) => throw new NotImplementedException();
    public Func<UserQuery, PagedResult<UserDto>> Users { get; set; } = _ => throw new NotImplementedException();
    public Func<Guid, UserDetailDto> User { get; set; } = _ => throw new NotImplementedException();
    public Func<Guid, string, UserDto> ChangeRole { get; set; } = (_, _) => throw new NotImplementedException();
    public Action<Guid> DeleteUser { get; set; } = _ => throw new NotImplementedException();
    public Func<ApplicationQuery, PagedResult<DriverApplicationDto>> Applications { get; set; } = _ => throw new NotImplementedException();
    public Func<Guid, DriverApplicationDto?> Approve { get; set; } = _ => throw new NotImplementedException();
    public Func<Guid, string, DriverApplicationDto?> Reject { get; set; } = (_, _) => throw new NotImplementedException();
    public Func<BookingQuery, PagedResult<BookingDto>> MyBookings { get; set; } = _ => throw new NotImplementedException();
    public Func<BookingQuery, PagedResult<BookingDto>> AdminBookings { get; set; } = _ => throw new NotImplementedException();
    public Func<Guid, BookingDto> Booking { get; set; } = _ => throw new NotImplementedException();

    public int LoginCalls { get; private set; }
    public int ApplicationsCalls { get; private set; }
    public List<BookingQuery> BookingQueries { get; } = [];

    public Task<MetaDto> GetMetaAsync(CancellationToken ct = default) => Run(Meta);

    public Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        LoginCalls++;
        return Run(() => Login(email, password));
    }

    public Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task LogoutAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<MeSummaryDto> GetMySummaryAsync(string? side, CancellationToken ct = default) => throw new NotImplementedException();

    public Task<PagedResult<BookingDto>> GetMyBookingsAsync(BookingQuery query, CancellationToken ct = default)
    {
        BookingQueries.Add(query);
        return Run(() => MyBookings(query));
    }

    public Task<BookingDto> GetMyBookingAsync(Guid id, CancellationToken ct = default) => Run(() => Booking(id));
    public Task<DriverApplicationDto?> GetMyDriverApplicationAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<FinanceDto> GetFinanceAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<ActivityItemDto>> GetActivityAsync(int limit, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PagedResult<UserDto>> GetUsersAsync(UserQuery query, CancellationToken ct = default) => Run(() => Users(query));
    public Task<UserDetailDto> GetUserAsync(Guid id, CancellationToken ct = default) => Run(() => User(id));
    public Task<UserDto> ChangeUserRoleAsync(Guid id, string role, CancellationToken ct = default) => Run(() => ChangeRole(id, role));
    public Task DeleteUserAsync(Guid id, CancellationToken ct = default) => Run(() => { DeleteUser(id); return 0; });

    public Task<PagedResult<DriverApplicationDto>> GetApplicationsAsync(ApplicationQuery query, CancellationToken ct = default)
    {
        ApplicationsCalls++;
        return Run(() => Applications(query));
    }

    public Task<DriverApplicationDto?> ApproveApplicationAsync(Guid id, CancellationToken ct = default) => Run(() => Approve(id));
    public Task<DriverApplicationDto?> RejectApplicationAsync(Guid id, string reason, CancellationToken ct = default) => Run(() => Reject(id, reason));
    public Task<byte[]> GetLicenseImageAsync(string licenseUrl, CancellationToken ct = default) => Task.FromResult(new byte[] { 1, 2, 3 });

    public Task<PagedResult<BookingDto>> GetAdminBookingsAsync(BookingQuery query, CancellationToken ct = default)
    {
        BookingQueries.Add(query);
        return Run(() => AdminBookings(query));
    }

    public Task<BookingDto> GetAdminBookingAsync(Guid id, CancellationToken ct = default) => Run(() => Booking(id));
    public Task<IReadOnlyList<PaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PaymentMethodDto>>([]);

    private static Task<T> Run<T>(Func<T> f)
    {
        try
        {
            return Task.FromResult(f());
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    public static ApiException Error(string code, int status, string? detailsJson = null) =>
        new(ApiErrorKind.Http, status, code, "server text",
            detailsJson is null ? null : System.Text.Json.JsonDocument.Parse(detailsJson).RootElement.Clone());
}

internal sealed class FakeNotifications : INotificationService
{
    public List<(ToastKind Kind, string Message)> Shown { get; } = [];

    public void Show(ToastKind kind, string message) => Shown.Add((kind, message));
}

internal sealed class FakeDialogs : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public string? RejectReason { get; set; } = "Foto buram";
    public string? PickedRole { get; set; }
    public List<string> Confirmations { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool danger = false)
    {
        Confirmations.Add(message);
        return Task.FromResult(ConfirmResult);
    }

    public Task<string?> AskRejectReasonAsync(string applicantName) => Task.FromResult(RejectReason);

    public Task<string?> PickRoleAsync(string username, string currentRole) => Task.FromResult(PickedRole);
}

internal sealed class FakeSettings : ISettingsService
{
    public Uri ApiBaseUri { get; private set; } = Api.Base;
    public AppTheme Theme { get; private set; }
    public IReadOnlyList<string> KnownServers { get; } = [];
    public void SetApiBaseUri(Uri baseUri) => ApiBaseUri = baseUri;
    public void SetTheme(AppTheme theme) => Theme = theme;
}

internal sealed class FakePlatform : IPlatformService
{
    public List<Uri> Opened { get; } = [];
    public string? AskSaveCsvPath(string suggestedFileName) => null;
    public void OpenInBrowser(Uri uri) => Opened.Add(uri);
}
