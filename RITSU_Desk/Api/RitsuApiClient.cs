using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.Api;

public sealed class RitsuApiClient : IRitsuApiClient
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ISessionService _session;
    private readonly Func<Uri> _baseUri;

    public RitsuApiClient(HttpClient http, ISessionService session, Func<Uri> baseUri)
    {
        _http = http;
        _session = session;
        _baseUri = baseUri;
    }

    public static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseCookies = false,
        };
        var http = new HttpClient(handler) { Timeout = DefaultTimeout };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RITSU-Desk", "1.0"));
        return http;
    }

    public Task<MetaDto> GetMetaAsync(CancellationToken ct = default) =>
        SendAsync<MetaDto>(HttpMethod.Get, "meta", null, authenticated: false, ct);

    public Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "auth/login", new { email, password }, authenticated: false, ct);

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken ct = default) =>
        (await SendAsync<UserEnvelope>(HttpMethod.Get, "auth/me", null, authenticated: true, ct)).User;

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        using var _ = await SendRawAsync(HttpMethod.Post, Resolve("auth/logout"), null, authenticated: true, ct);
    }

    public Task<MeSummaryDto> GetMySummaryAsync(string? side, CancellationToken ct = default) =>
        SendAsync<MeSummaryDto>(HttpMethod.Get, "me/summary" + new QueryString().Add("as", side), null, true, ct);

    public Task<PagedResult<BookingDto>> GetMyBookingsAsync(BookingQuery query, CancellationToken ct = default) =>
        SendAsync<PagedResult<BookingDto>>(HttpMethod.Get, "me/bookings" + BookingQueryString(query, includeSide: true), null, true, ct);

    public async Task<BookingDto> GetMyBookingAsync(Guid id, CancellationToken ct = default) =>
        (await SendAsync<BookingEnvelope>(HttpMethod.Get, $"me/bookings/{id}", null, true, ct)).Booking;

    public async Task<DriverApplicationDto?> GetMyDriverApplicationAsync(CancellationToken ct = default) =>
        (await SendAsync<ApplicationEnvelope>(HttpMethod.Get, "me/driver-application", null, true, ct)).Application;

    public Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken ct = default) =>
        SendAsync<AdminDashboardDto>(HttpMethod.Get, "admin/dashboard", null, true, ct);

    public Task<FinanceDto> GetFinanceAsync(CancellationToken ct = default) =>
        SendAsync<FinanceDto>(HttpMethod.Get, "admin/finance", null, true, ct);

    public async Task<IReadOnlyList<ActivityItemDto>> GetActivityAsync(int limit, CancellationToken ct = default) =>
        (await SendAsync<ItemsEnvelope<ActivityItemDto>>(HttpMethod.Get, "admin/activity" + new QueryString().Add("limit", limit), null, true, ct)).Items;

    public Task<PagedResult<UserDto>> GetUsersAsync(UserQuery query, CancellationToken ct = default)
    {
        var qs = new QueryString()
            .Add("page", query.Page)
            .Add("pageSize", query.PageSize)
            .Add("q", query.Search)
            .Add("role", query.Role);
        return SendAsync<PagedResult<UserDto>>(HttpMethod.Get, "admin/users" + qs, null, true, ct);
    }

    public Task<UserDetailDto> GetUserAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<UserDetailDto>(HttpMethod.Get, $"admin/users/{id}", null, true, ct);

    public async Task<UserDto> ChangeUserRoleAsync(Guid id, string role, CancellationToken ct = default) =>
        (await SendAsync<UserEnvelope>(HttpMethod.Patch, $"admin/users/{id}", new { role }, true, ct)).User;

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        using var _ = await SendRawAsync(HttpMethod.Delete, Resolve($"admin/users/{id}"), null, authenticated: true, ct);
    }

    public Task<PagedResult<DriverApplicationDto>> GetApplicationsAsync(ApplicationQuery query, CancellationToken ct = default)
    {
        var qs = new QueryString()
            .Add("page", query.Page)
            .Add("pageSize", query.PageSize)
            .Add("status", query.Status);
        return SendAsync<PagedResult<DriverApplicationDto>>(HttpMethod.Get, "admin/applications" + qs, null, true, ct);
    }

    public async Task<DriverApplicationDto?> ApproveApplicationAsync(Guid id, CancellationToken ct = default) =>
        (await SendAsync<ApplicationEnvelope>(HttpMethod.Post, $"admin/applications/{id}/approve", new { }, true, ct)).Application;

    public async Task<DriverApplicationDto?> RejectApplicationAsync(Guid id, string reason, CancellationToken ct = default) =>
        (await SendAsync<ApplicationEnvelope>(HttpMethod.Post, $"admin/applications/{id}/reject", new { reason }, true, ct)).Application;

    public async Task<byte[]> GetLicenseImageAsync(string licenseUrl, CancellationToken ct = default)
    {
        using var response = await SendRawAsync(HttpMethod.Get, ResolveServerPath(licenseUrl), null, authenticated: true, ct);
        try
        {
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw ApiException.Network(ex);
        }
    }

    public Task<PagedResult<BookingDto>> GetAdminBookingsAsync(BookingQuery query, CancellationToken ct = default) =>
        SendAsync<PagedResult<BookingDto>>(HttpMethod.Get, "admin/bookings" + BookingQueryString(query, includeSide: false), null, true, ct);

    public async Task<BookingDto> GetAdminBookingAsync(Guid id, CancellationToken ct = default) =>
        (await SendAsync<BookingEnvelope>(HttpMethod.Get, $"admin/bookings/{id}", null, true, ct)).Booking;

    public async Task<IReadOnlyList<PaymentMethodDto>> GetPaymentMethodsAsync(CancellationToken ct = default) =>
        (await SendAsync<ItemsEnvelope<PaymentMethodDto>>(HttpMethod.Get, "admin/payment-methods", null, true, ct)).Items;

    private static QueryString BookingQueryString(BookingQuery query, bool includeSide) =>
        new QueryString()
            .Add("page", query.Page)
            .Add("pageSize", query.PageSize)
            .Add("as", includeSide ? query.Side : null)
            .Add("stage", query.Stage)
            .Add("paid", query.Paid)
            .Add("paymentMethodId", query.PaymentMethodId)
            .Add("from", query.From)
            .Add("to", query.To)
            .Add("q", query.Search);

    private Uri Resolve(string relative) => new(_baseUri(), relative);

    internal Uri ResolveServerPath(string path)
    {
        var baseUri = _baseUri();
        var prefix = ServerAddress.ApiPath + "/";
        var resolved = path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? new Uri(baseUri, path[prefix.Length..])
            : new Uri(baseUri, path);

        if (Uri.Compare(resolved, baseUri, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
        {
            throw ApiException.InvalidResponse(null);
        }
        return resolved;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string relative, object? body, bool authenticated, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, Resolve(relative), body, authenticated, ct);
        try
        {
            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return result ?? throw ApiException.InvalidResponse((int)response.StatusCode);
        }
        catch (JsonException ex)
        {
            AppLog.Error($"Invalid JSON from {method} {Describe(response.RequestMessage?.RequestUri)}", ex);
            throw ApiException.InvalidResponse((int)response.StatusCode, ex);
        }
        catch (NotSupportedException ex)
        {
            throw ApiException.InvalidResponse((int)response.StatusCode, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw ApiException.Timeout(ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw ApiException.Network(ex);
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, Uri uri, object? body, bool authenticated, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (authenticated)
        {
            var token = _session.Token;
            if (token is null || _session.IsExpired)
            {
                _session.NotifyExpired();
                throw ApiException.SessionExpired();
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            AppLog.Warn($"Timeout on {method} {Describe(uri)}");
            throw ApiException.Timeout(ex);
        }
        catch (HttpRequestException ex)
        {
            AppLog.Warn($"Network error on {method} {Describe(uri)}: {ex.Message}");
            throw ApiException.Network(ex);
        }

        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            var error = await ReadErrorAsync(response, ct);
            AppLog.Warn($"{method} {Describe(uri)} -> {(int)response.StatusCode} {error.Code}");

            if (authenticated && error.Code == ApiErrorCodes.Unauthorized) _session.NotifyExpired();
            throw error;
        }
    }

    private static async Task<ApiException> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiErrorBody>(JsonOptions, ct);
            if (body?.Error?.Code is { Length: > 0 } code)
            {
                return new ApiException(ApiErrorKind.Http, status, code, body.Error.Message ?? code, body.Error.Details);
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException or IOException)
        {
        }

        var fallback = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => ApiErrorCodes.ValidationError,
            HttpStatusCode.Unauthorized => ApiErrorCodes.Unauthorized,
            HttpStatusCode.Forbidden => ApiErrorCodes.Forbidden,
            HttpStatusCode.NotFound => ApiErrorCodes.NotFound,
            HttpStatusCode.TooManyRequests => ApiErrorCodes.RateLimited,
            _ when status >= 500 => ApiErrorCodes.Internal,
            _ => ApiErrorCodes.InvalidResponse,
        };
        return new ApiException(ApiErrorKind.Http, status, fallback, $"HTTP {status}");
    }

    private static string Describe(Uri? uri) => uri?.AbsolutePath ?? "?";
}

public sealed class QueryString
{
    private readonly StringBuilder _sb = new();

    public QueryString Add(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return this;
        _sb.Append(_sb.Length == 0 ? '?' : '&')
           .Append(Uri.EscapeDataString(name))
           .Append('=')
           .Append(Uri.EscapeDataString(value.Trim()));
        return this;
    }

    public QueryString Add(string name, int? value) =>
        value is null ? this : Add(name, value.Value.ToString(CultureInfo.InvariantCulture));

    public QueryString Add(string name, bool? value) =>
        value is null ? this : Add(name, value.Value ? "true" : "false");

    public QueryString Add(string name, Guid? value) =>
        value is null ? this : Add(name, value.Value.ToString());

    public QueryString Add(string name, DateTimeOffset? value) =>
        value is null ? this : Add(name, value.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));

    public override string ToString() => _sb.ToString();

    public static implicit operator string(QueryString qs) => qs.ToString();
}
