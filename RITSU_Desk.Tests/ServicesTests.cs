using System.Text.Json;
using RITSU_Desk.Api;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.Tests;

public class ServerAddressTests
{
    [Theory]
    [InlineData("10.10.0.1:3000", "http://10.10.0.1:3000/api/desktop/v1/")]
    [InlineData("http://localhost:3000", "http://localhost:3000/api/desktop/v1/")]
    [InlineData("http://localhost:3000/api/desktop/v1", "http://localhost:3000/api/desktop/v1/")]
    [InlineData("https://ritsu.shinyshoe.net/api/desktop/v1/", "https://ritsu.shinyshoe.net/api/desktop/v1/")]
    [InlineData("http://192.168.1.20:3000/", "http://192.168.1.20:3000/api/desktop/v1/")]
    [InlineData("http://ritsu.localhost", "http://ritsu.localhost/api/desktop/v1/")]
    public void Accepts_and_normalizes(string input, string expected)
    {
        Assert.True(ServerAddress.TryNormalize(input, out var uri, out var error), error);
        Assert.Equal(expected, uri!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://10.10.0.1")]
    [InlineData("http://ritsu.shinyshoe.net")]
    [InlineData("http://8.8.8.8:3000")]
    [InlineData("http://172.32.0.1:3000")]
    [InlineData("http://localhost:3000/?x=1")]
    [InlineData("http://user:pass@localhost:3000")]
    public void Rejects(string input)
    {
        Assert.False(ServerAddress.TryNormalize(input, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Web_origin_strips_the_api_path()
    {
        Assert.Equal(new Uri("http://10.10.0.1:3000/"), ServerAddress.WebOrigin(new Uri("http://10.10.0.1:3000/api/desktop/v1/")));
        Assert.Equal(new Uri("https://x.test/ritsu/"), ServerAddress.WebOrigin(new Uri("https://x.test/ritsu/api/desktop/v1/")));
    }
}

public class FormatTests
{
    [Theory]
    [InlineData(12000, "Rp12.000")]
    [InlineData(0, "Rp0")]
    [InlineData(1234567.5, "Rp1.234.568")]
    [InlineData(950.4, "Rp950")]
    public void Rupiah_uses_id_ID_grouping_without_decimals(decimal value, string expected) =>
        Assert.Equal(expected, Format.Rupiah(value));

    [Theory]
    [InlineData(3.5, "3,5 km")]
    [InlineData(3.47, "3,47 km")]
    [InlineData(2, "2,0 km")]
    [InlineData(1.234, "1,23 km")]
    public void Km_has_one_or_two_decimals(decimal value, string expected) =>
        Assert.Equal(expected, Format.Km(value));

    [Fact]
    public void Times_are_shown_in_WIB()
    {
        Assert.Equal("21 Okt 2026, 23.30 WIB", Format.DateTime(DateTimeOffset.Parse("2026-10-21T16:30:00Z")));
        Assert.Equal("22 Okt 2026, 06.30 WIB", Format.DateTime(DateTimeOffset.Parse("2026-10-21T23:30:00Z")));
        Assert.Equal("-", Format.DateTime(null));
    }

    [Fact]
    public void Location_prefers_name_then_coordinates_then_raw()
    {
        Assert.Equal("Asrama ITS", Format.Location(new LocationDto(-7.2, 112.7, " Asrama ITS ", "{}")));
        Assert.Equal("-7.2812, 112.7951", Format.Location(new LocationDto(-7.28123456, 112.79512345, null, "{}")));
        Assert.Equal("-7.2812, 112.7951", Format.Location(new LocationDto(-7.28123456, 112.79512345, "  ", "{}")));
        Assert.Equal("Depan kantin", Format.Location(new LocationDto(null, null, null, "Depan kantin")));
        Assert.Equal("-", Format.Location(new LocationDto(null, null, null, "")));
        Assert.Equal("-", Format.Location(null));
    }

    [Fact]
    public void Month_and_day_labels_are_Indonesian()
    {
        Assert.Equal("Okt 2026", Format.Month("2026-10"));
        Assert.Equal("Oktober 2026", Format.Month("2026-10", shortName: false));
        Assert.Equal("21 Okt", Format.Day("2026-10-21"));
        Assert.Equal("garbage", Format.Month("garbage"));
    }

    [Theory]
    [InlineData(950, "950")]
    [InlineData(12500, "12,5rb")]
    [InlineData(1200000, "1,2jt")]
    public void Compact_amounts(decimal value, string expected) => Assert.Equal(expected, Format.Compact(value));

    [Fact]
    public void Relative_time()
    {
        var now = DateTimeOffset.Parse("2026-10-21T12:00:00Z");
        Assert.Equal("baru saja", Format.Relative(now.AddSeconds(-20), now));
        Assert.Equal("5 menit lalu", Format.Relative(now.AddMinutes(-5), now));
        Assert.Equal("3 jam lalu", Format.Relative(now.AddHours(-3), now));
        Assert.Equal("2 hari lalu", Format.Relative(now.AddDays(-2), now));
    }
}

public class LabelsTests
{
    [Fact]
    public void Values_match_the_web_exactly()
    {
        Assert.Equal("Penumpang", Labels.Role("customer"));
        Assert.Equal("Driver", Labels.Role("driver"));
        Assert.Equal("Admin", Labels.Role("admin"));
        Assert.Equal("Menunggu", Labels.ApplicationStatus("pending"));
        Assert.Equal("Disetujui", Labels.ApplicationStatus("approved"));
        Assert.Equal("Ditolak", Labels.ApplicationStatus("declined"));
        Assert.Equal("Mencari driver", Labels.Stage(1));
        Assert.Equal("Driver merespons", Labels.Stage(2));
        Assert.Equal("Driver menuju titik jemput", Labels.Stage(3));
        Assert.Equal("Dalam perjalanan", Labels.Stage(4));
        Assert.Equal("Selesai", Labels.Stage(5));
        Assert.Equal("Lunas", Labels.Paid(true));
        Assert.Equal("Belum dibayar", Labels.Paid(false));
    }

    [Fact]
    public void Badge_colors_follow_the_web()
    {
        Assert.Equal(BadgeKind.Warning, Labels.StageKind(1));
        Assert.Equal(BadgeKind.Info, Labels.StageKind(2));
        Assert.Equal(BadgeKind.Primary, Labels.StageKind(3));
        Assert.Equal(BadgeKind.Primary, Labels.StageKind(4));
        Assert.Equal(BadgeKind.Success, Labels.StageKind(5));
        Assert.Equal(BadgeKind.Warning, Labels.RoleKind("admin"));
        Assert.Equal(BadgeKind.Primary, Labels.RoleKind("driver"));
        Assert.Equal(BadgeKind.Neutral, Labels.RoleKind("customer"));
        Assert.Equal(BadgeKind.Danger, Labels.ApplicationStatusKind("declined"));
        Assert.True(Labels.IsCash("Cash"));
        Assert.False(Labels.IsCash("cash"));
    }
}

public class PeriodFilterTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-21T17:30:00Z");

    [Fact]
    public void Today_uses_the_WIB_calendar_day()
    {
        var (from, to) = PeriodFilter.Range(PeriodPreset.Today, Now);

        Assert.Equal(DateTimeOffset.Parse("2026-10-21T17:00:00Z"), from);
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T17:00:00Z"), to);
    }

    [Fact]
    public void A_one_day_range_includes_late_evening_and_early_morning_bookings()
    {
        var (from, to) = PeriodFilter.Range(PeriodPreset.Custom, Now, new DateTime(2026, 10, 21), new DateTime(2026, 10, 21));
        var at2330Wib = DateTimeOffset.Parse("2026-10-21T16:30:00Z");
        var at0630Wib = DateTimeOffset.Parse("2026-10-20T23:30:00Z");
        var nextDay0030Wib = DateTimeOffset.Parse("2026-10-21T17:30:00Z");

        Assert.True(at2330Wib >= from && at2330Wib < to);
        Assert.True(at0630Wib >= from && at0630Wib < to);
        Assert.False(nextDay0030Wib >= from && nextDay0030Wib < to);
    }

    [Fact]
    public void Custom_range_swaps_reversed_dates_and_allows_open_ends()
    {
        var (from, to) = PeriodFilter.Range(PeriodPreset.Custom, Now, new DateTime(2026, 10, 25), new DateTime(2026, 10, 20));
        Assert.Equal(DateTimeOffset.Parse("2026-10-19T17:00:00Z"), from);
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T17:00:00Z"), to);

        var (openFrom, openTo) = PeriodFilter.Range(PeriodPreset.Custom, Now, new DateTime(2026, 10, 1), null);
        Assert.NotNull(openFrom);
        Assert.Null(openTo);

        Assert.Equal((null, null), PeriodFilter.Range(PeriodPreset.All, Now));
    }

    [Fact]
    public void This_month_starts_on_the_first_in_WIB()
    {
        var (from, _) = PeriodFilter.Range(PeriodPreset.ThisMonth, DateTimeOffset.Parse("2026-10-31T18:00:00Z"));
        Assert.Equal(DateTimeOffset.Parse("2026-10-31T17:00:00Z"), from);
    }
}

public class ErrorMessagesTests
{
    [Theory]
    [InlineData(ApiErrorCodes.InvalidCredentials, "Email atau password salah.")]
    [InlineData(ApiErrorCodes.Unauthorized, "Sesi berakhir, silakan login lagi.")]
    [InlineData(ApiErrorCodes.Forbidden, "Anda tidak punya akses ke fitur ini.")]
    [InlineData(ApiErrorCodes.SelfActionForbidden, "Anda tidak bisa melakukan aksi ini pada akun sendiri.")]
    [InlineData(ApiErrorCodes.NotFound, "Data tidak ditemukan. Mungkin sudah dihapus.")]
    [InlineData(ApiErrorCodes.AlreadyReviewed, "Pengajuan ini sudah ditinjau. Daftar dimuat ulang.")]
    [InlineData(ApiErrorCodes.RateLimited, "Terlalu banyak percobaan login. Coba lagi beberapa menit lagi.")]
    [InlineData(ApiErrorCodes.Internal, "Terjadi kesalahan di server. Coba lagi nanti.")]
    [InlineData(ApiErrorCodes.ValidationError, "Data yang diisi belum valid.")]
    public void Codes_map_to_the_PRD_messages(string code, string expected) =>
        Assert.Equal(expected, ErrorMessages.For(new ApiException(ApiErrorKind.Http, 400, code, "english text")));

    [Fact]
    public void Validation_lists_fields_in_Indonesian()
    {
        var details = JsonDocument.Parse("""{"formErrors":[],"fieldErrors":{"email":["Please enter a valid email address."],"reason":["x"]}}""").RootElement;
        var ex = new ApiException(ApiErrorKind.Http, 400, ApiErrorCodes.ValidationError, "x", details);

        Assert.Equal("Data yang diisi belum valid. Periksa: Email, Alasan.", ErrorMessages.For(ex));
    }

    [Fact]
    public void Connection_problems_say_so_instead_of_blaming_credentials()
    {
        var message = ErrorMessages.For(ApiException.Network(new Exception()));
        Assert.Contains("Server tidak dapat dihubungi", message);
        Assert.DoesNotContain("password", message, StringComparison.OrdinalIgnoreCase);
    }
}

public class CsvTests
{
    [Fact]
    public void Escapes_separators_quotes_and_newlines()
    {
        Assert.Equal("plain", Csv.Escape("plain"));
        Assert.Equal("\"a,b\"", Csv.Escape("a,b"));
        Assert.Equal("\"say \"\"hi\"\"\"", Csv.Escape("say \"hi\""));
        Assert.Equal("\"line1\nline2\"", Csv.Escape("line1\nline2"));
        Assert.Equal(string.Empty, Csv.Escape(null));
    }

    [Fact]
    public void Neutralizes_spreadsheet_formulas_but_keeps_negative_numbers()
    {
        Assert.Equal("'=HYPERLINK(1)", Csv.Escape("=HYPERLINK(1)"));
        Assert.Equal("'@cmd", Csv.Escape("@cmd"));
        Assert.Equal("-12.5", Csv.Escape("-12.5"));
    }

    [Fact]
    public void Build_uses_crlf_rows()
    {
        var csv = Csv.Build(["A", "B"], [["1", null], ["x,y", "2"]]);
        Assert.Equal("A,B\r\n1,\r\n\"x,y\",2\r\n", csv);
    }

    [Fact]
    public void Booking_rows_use_WIB_and_plain_numbers()
    {
        var row = RITSU_Desk.ViewModels.Admin.BookingCsv.Row(Data.Booking() with { DistanceKm = 3.47m, Price = 13700m });
        Assert.Equal("2026-10-21 15:00", row[1]);
        Assert.Equal("3.47", row[4]);
        Assert.Equal("13700", row[5]);
        Assert.Equal("Selesai", row[8]);
    }
}

public class SessionServiceTests
{
    [Fact]
    public void Expiry_clears_the_token_and_fires_once()
    {
        var session = new SessionService();
        var count = 0;
        session.Expired += (_, _) => count++;
        session.Start(new LoginResponse("t", DateTimeOffset.UtcNow.AddHours(1), Users.Customer));

        session.NotifyExpired();
        session.NotifyExpired();

        Assert.Equal(1, count);
        Assert.Null(session.Token);
        Assert.Null(session.User);
    }

    [Fact]
    public void Is_expired_follows_the_clock()
    {
        var now = DateTimeOffset.Parse("2026-10-21T00:00:00Z");
        var session = new SessionService(() => now);
        session.Start(new LoginResponse("t", now.AddHours(12), Users.Customer));
        Assert.False(session.IsExpired);
        now = now.AddHours(12);
        Assert.True(session.IsExpired);
    }
}
