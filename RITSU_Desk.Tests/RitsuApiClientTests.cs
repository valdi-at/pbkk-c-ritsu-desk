using System.Net;
using System.Net.Http;
using RITSU_Desk.Api;
using RITSU_Desk.Models;

namespace RITSU_Desk.Tests;

public class RitsuApiClientTests
{
    [Fact]
    public async Task Meta_is_parsed_from_the_live_server_shape()
    {
        var (client, handler, _) = Api.Create(_ => StubHandler.Json(
            """{"apiVersion":1,"schemaVersion":"005_chat_add_media.sql","serverTime":"2026-10-21T11:51:36.599Z","dbTimezone":"UTC"}"""),
            loggedIn: false);

        var meta = await client.GetMetaAsync();

        Assert.Equal(1, meta.ApiVersion);
        Assert.Equal("005_chat_add_media.sql", meta.SchemaVersion);
        Assert.Equal("UTC", meta.DbTimezone);
        Assert.Equal(new Uri("http://10.10.0.1:3000/api/desktop/v1/meta"), handler.Requests[0].Uri);
        Assert.Null(handler.Requests[0].Authorization);
    }

    [Fact]
    public async Task Login_posts_credentials_without_a_token()
    {
        var (client, handler, _) = Api.Create(_ => StubHandler.Json(
            """{"token":"abc","expiresAt":"2026-10-21T23:00:00.000Z","user":{"id":"22222222-2222-2222-2222-222222222222","username":"nadia","email":"nadia@its.ac.id","role":"customer","createdAt":"2026-02-01T00:00:00.000Z","updatedAt":"2026-02-02T00:00:00.000Z","extra":"ignored"}}"""),
            loggedIn: false);

        var login = await client.LoginAsync("nadia@its.ac.id", "rahasia");

        Assert.Equal("abc", login.Token);
        Assert.Equal(Roles.Customer, login.User.Role);
        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/api/desktop/v1/auth/login", request.Uri.AbsolutePath);
        Assert.Null(request.Authorization);
        Assert.Equal("""{"email":"nadia@its.ac.id","password":"rahasia"}""", request.Body);
    }

    [Fact]
    public async Task Invalid_credentials_do_not_end_a_session()
    {
        var (client, _, session) = Api.Create(_ => StubHandler.Error(HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS"));
        var expired = false;
        session.Expired += (_, _) => expired = true;

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.LoginAsync("a@b.c", "x"));

        Assert.Equal(ApiErrorCodes.InvalidCredentials, ex.Code);
        Assert.False(expired);
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public async Task Authenticated_calls_send_the_bearer_token()
    {
        var (client, handler, _) = Api.Create(_ => StubHandler.Json(
            """{"user":{"id":"11111111-1111-1111-1111-111111111111","username":"valdi","email":"v@its.ac.id","role":"admin","createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}}"""));

        var me = await client.GetCurrentUserAsync();

        Assert.Equal("valdi", me.Username);
        Assert.Equal("Bearer tok.en", handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task Unauthorized_response_ends_the_session_once()
    {
        var (client, _, session) = Api.Create(_ => StubHandler.Error(HttpStatusCode.Unauthorized, "UNAUTHORIZED"));
        var expiredCount = 0;
        session.Expired += (_, _) => expiredCount++;

        await Assert.ThrowsAsync<ApiException>(() => client.GetAdminDashboardAsync());
        await Assert.ThrowsAsync<ApiException>(() => client.GetAdminDashboardAsync());

        Assert.Equal(1, expiredCount);
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.Token);
    }

    [Fact]
    public async Task Locally_expired_token_is_never_sent()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("should not be called"));
        var now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");
        var session = new RITSU_Desk.Services.SessionService(() => now);
        session.Start(new LoginResponse("old", now.AddMinutes(-1), Users.Admin));
        var client = new RitsuApiClient(new HttpClient(handler), session, () => Api.Base);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAdminDashboardAsync());

        Assert.Equal(ApiErrorCodes.Unauthorized, ex.Code);
        Assert.Empty(handler.Requests);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task My_bookings_query_string_carries_every_filter_in_utc()
    {
        var (client, handler, _) = Api.Create(_ => StubHandler.Json("""{"items":[],"page":2,"pageSize":50,"total":0}"""));

        await client.GetMyBookingsAsync(new BookingQuery
        {
            Page = 2,
            PageSize = 50,
            Side = BookingSides.Driver,
            Stage = 5,
            Paid = false,
            From = new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.FromHours(7)),
            To = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.FromHours(7)),
            Search = " asrama & co ",
        });

        var uri = handler.Requests.Single().Uri;
        Assert.Equal("/api/desktop/v1/me/bookings", uri.AbsolutePath);
        Assert.Equal(
            "?page=2&pageSize=50&as=driver&stage=5&paid=false&from=2026-10-20T17%3A00%3A00Z&to=2026-10-21T17%3A00%3A00Z&q=asrama%20%26%20co",
            uri.Query);
    }

    [Fact]
    public async Task Admin_bookings_never_send_as_and_skip_empty_filters()
    {
        var methodId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var (client, handler, _) = Api.Create(_ => StubHandler.Json("""{"items":[],"page":1,"pageSize":25,"total":0}"""));

        await client.GetAdminBookingsAsync(new BookingQuery { Side = BookingSides.Driver, Search = "  ", PaymentMethodId = methodId });

        Assert.Equal($"?page=1&pageSize=25&paymentMethodId={methodId}", handler.Requests.Single().Uri.Query);
    }

    [Fact]
    public async Task Booking_locations_tolerate_missing_names_and_non_json_rows()
    {
        var (client, _, _) = Api.Create(_ => StubHandler.Json("""
            {"booking":{
              "id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","stage":3,
              "pickup":{"lat":-7.28123456,"lng":112.79512345,"name":null,"raw":"{\"lat\":-7.28123456,\"lng\":112.79512345}"},
              "destination":{"lat":null,"lng":null,"name":null,"raw":"Depan kantin"},
              "stops":["{\"lat\":1,\"lng\":2,\"name\":\"Halte\"}", {"lat":null,"lng":null,"name":null,"raw":"x"}],
              "distanceKm":3.47,"price":13700,"paymentStatus":false,
              "paymentMethod":{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","name":"GoPay"},
              "customer":{"id":"22222222-2222-2222-2222-222222222222","username":"nadia"},
              "driver":null,
              "driverReadyAt":null,"deliveryStartTime":null,"deliveryEndTime":null,
              "createdAt":"2026-10-21T08:00:00.000Z","updatedAt":"2026-10-21T08:00:00.000Z"}}
            """));

        var booking = await client.GetMyBookingAsync(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        Assert.Equal("-7.2812, 112.7951", RITSU_Desk.Services.Format.Location(booking.Pickup));
        Assert.Equal("Depan kantin", RITSU_Desk.Services.Format.Location(booking.Destination));
        Assert.Equal("Halte", booking.Stops![0].Name);
        Assert.Equal("x", booking.Stops[1].Raw);
        Assert.Null(booking.Driver);
        Assert.Equal(3.47m, booking.DistanceKm);
        Assert.Equal(13700m, booking.Price);
    }

    [Fact]
    public async Task Has_dependencies_error_keeps_the_counts()
    {
        var (client, _, _) = Api.Create(_ => StubHandler.Error(HttpStatusCode.Conflict, "HAS_DEPENDENCIES",
            """{"bookingsAsCustomer":4,"bookingsAsDriver":1,"chatMessages":12}"""));

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.DeleteUserAsync(Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("Pengguna masih punya 5 pemesanan dan 12 chat, jadi tidak bisa dihapus.", RITSU_Desk.Services.ErrorMessages.For(ex));
    }

    [Fact]
    public async Task Delete_accepts_204_no_content()
    {
        var (client, handler, _) = Api.Create(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.DeleteUserAsync(Users.Customer.Id);

        Assert.Equal(HttpMethod.Delete, handler.Requests.Single().Method);
        Assert.EndsWith($"/admin/users/{Users.Customer.Id}", handler.Requests.Single().Uri.AbsolutePath);
    }

    [Fact]
    public async Task Html_error_page_falls_back_to_a_code_from_the_status()
    {
        var (client, _, _) = Api.Create(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>", System.Text.Encoding.UTF8, "text/html"),
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetFinanceAsync());

        Assert.Equal(ApiErrorCodes.Internal, ex.Code);
        Assert.Equal(502, ex.StatusCode);
    }

    [Fact]
    public async Task Success_with_html_body_is_reported_as_invalid_response()
    {
        var (client, _, _) = Api.Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>login page</html>", System.Text.Encoding.UTF8, "text/html"),
        }, loggedIn: false);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetMetaAsync());

        Assert.Equal(ApiErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public async Task Connection_failure_is_a_network_error()
    {
        var (client, _, _) = Api.Create(_ => throw new HttpRequestException("No connection could be made"), loggedIn: false);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetMetaAsync());

        Assert.Equal(ApiErrorKind.Network, ex.Kind);
        Assert.True(ex.IsConnectionProblem);
    }

    [Fact]
    public async Task Http_client_timeout_is_a_timeout_error()
    {
        var (client, _, _) = Api.Create(_ => throw new TaskCanceledException("timed out", new TimeoutException()), loggedIn: false);

        var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetMetaAsync());

        Assert.Equal(ApiErrorKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_turned_into_a_timeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var (client, _, _) = Api.Create(_ => throw new TaskCanceledException(), loggedIn: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetMetaAsync(cts.Token));
    }

    [Fact]
    public async Task License_url_is_fetched_from_the_configured_server_with_the_token()
    {
        var (client, handler, _) = Api.Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });

        var bytes = await client.GetLicenseImageAsync("/api/desktop/v1/admin/applications/abc/license");

        Assert.Equal([1, 2, 3], bytes);
        Assert.Equal(new Uri("http://10.10.0.1:3000/api/desktop/v1/admin/applications/abc/license"), handler.Requests.Single().Uri);
        Assert.Equal("Bearer tok.en", handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task License_url_on_another_host_is_refused_so_the_token_never_leaves()
    {
        var (client, handler, _) = Api.Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ApiException>(() => client.GetLicenseImageAsync("https://evil.example/steal"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Admin_dashboard_maps_every_metric()
    {
        var (client, _, _) = Api.Create(_ => StubHandler.Json("""
            {"totalUsers":12,"usersByRole":{"customer":8,"driver":3,"admin":1},
             "applicationsByStatus":{"pending":3,"approved":2,"declined":1},
             "revenue":{"total":469200,"paidCount":37,"average":12681.08},
             "outstanding":{"amount":166800,"count":14},
             "bookingVolume":{"today":{"count":3,"paidCount":1},"week":{"count":14,"paidCount":9},"month":{"count":37,"paidCount":28}},
             "revenueByPaymentMethod":[{"id":"cccccccc-cccc-cccc-cccc-cccccccccccc","name":"Cash","iconLink":"/icons/cash.svg","revenue":144300,"count":11}],
             "dailyRevenue":[{"day":"2026-10-21","revenue":20000,"count":2}],
             "topDrivers":[{"id":"33333333-3333-3333-3333-333333333333","username":"budi","totalEarned":219400,"bookingCount":19}]}
            """));

        var d = await client.GetAdminDashboardAsync();

        Assert.Equal(3, d.UsersByRole[Roles.Driver]);
        Assert.Equal(3, d.ApplicationsByStatus[ApplicationStatuses.Pending]);
        Assert.Equal(12681.08m, d.Revenue.Average);
        Assert.Equal(28, d.BookingVolume.Month.PaidCount);
        Assert.Equal("budi", d.TopDrivers.Single().Username);
    }
}
