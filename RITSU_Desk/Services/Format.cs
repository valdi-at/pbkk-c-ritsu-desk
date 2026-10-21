using System.Globalization;
using RITSU_Desk.Models;

namespace RITSU_Desk.Services;

public static class Format
{
    public static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public static readonly TimeSpan WibOffset = TimeSpan.FromHours(7);

    public const string Dash = "-";

    public static string Rupiah(decimal value) =>
        (value < 0 ? "-Rp" : "Rp") + Math.Abs(Math.Round(value, 0, MidpointRounding.AwayFromZero)).ToString("N0", Id);

    public static string Compact(decimal value)
    {
        var abs = Math.Abs(value);
        var sign = value < 0 ? "-" : string.Empty;
        if (abs >= 1_000_000_000m) return $"{sign}{(abs / 1_000_000_000m).ToString("0.#", Id)}M";
        if (abs >= 1_000_000m) return $"{sign}{(abs / 1_000_000m).ToString("0.#", Id)}jt";
        if (abs >= 1_000m) return $"{sign}{(abs / 1_000m).ToString("0.#", Id)}rb";
        return value.ToString("0", Id);
    }

    public static string Km(decimal value) => value.ToString("0.0#", Id) + " km";

    public static string Number(int value) => value.ToString("N0", Id);

    public static string Percent(decimal ratio) => ratio.ToString("0.#%", Id);

    public static DateTimeOffset ToWib(DateTimeOffset value) => value.ToOffset(WibOffset);

    public static string DateTime(DateTimeOffset? value) =>
        value is { } v ? ToWib(v).ToString("d MMM yyyy, HH.mm", Id) + " WIB" : Dash;

    public static string Date(DateTimeOffset? value) =>
        value is { } v ? ToWib(v).ToString("d MMM yyyy", Id) : Dash;

    public static string Time(DateTimeOffset value) => ToWib(value).ToString("HH.mm", Id) + " WIB";

    public static string Relative(DateTimeOffset value, DateTimeOffset now)
    {
        var diff = now - value;
        if (diff < TimeSpan.FromMinutes(1)) return "baru saja";
        if (diff < TimeSpan.FromHours(1)) return $"{(int)diff.TotalMinutes} menit lalu";
        if (diff < TimeSpan.FromDays(1)) return $"{(int)diff.TotalHours} jam lalu";
        if (diff < TimeSpan.FromDays(7)) return $"{(int)diff.TotalDays} hari lalu";
        return Date(value);
    }

    public static string Month(string isoMonth, bool shortName = true) =>
        TryParseMonth(isoMonth, out var month)
            ? month.ToString(shortName ? "MMM yyyy" : "MMMM yyyy", Id)
            : isoMonth;

    public static string MonthShort(string isoMonth) =>
        TryParseMonth(isoMonth, out var month) ? month.ToString("MMM", Id) : isoMonth;

    public static string Day(string isoDay, bool withYear = false) =>
        System.DateTime.TryParseExact(isoDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day.ToString(withYear ? "d MMM yyyy" : "d MMM", Id)
            : isoDay;

    public static string Location(LocationDto? location)
    {
        if (location is null) return Dash;
        if (!string.IsNullOrWhiteSpace(location.Name)) return location.Name.Trim();
        if (location.Lat is { } lat && location.Lng is { } lng)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{lat:0.0000}, {lng:0.0000}");
        }
        return string.IsNullOrWhiteSpace(location.Raw) ? Dash : location.Raw.Trim();
    }

    public static string ShortId(Guid id) => id.ToString()[..8].ToUpperInvariant();

    private static bool TryParseMonth(string isoMonth, out System.DateTime month) =>
        System.DateTime.TryParseExact(isoMonth, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out month);
}
