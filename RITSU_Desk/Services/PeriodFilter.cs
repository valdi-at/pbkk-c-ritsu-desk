namespace RITSU_Desk.Services;

public enum PeriodPreset
{
    All,
    Today,
    Last7Days,
    Last30Days,
    ThisMonth,
    ThisYear,
    Custom,
}

public static class PeriodFilter
{
    public static (DateTimeOffset? From, DateTimeOffset? To) Range(
        PeriodPreset preset, DateTimeOffset now, DateTime? customFrom = null, DateTime? customTo = null)
    {
        var today = Format.ToWib(now).Date;
        return preset switch
        {
            PeriodPreset.Today => Days(today, today),
            PeriodPreset.Last7Days => Days(today.AddDays(-6), today),
            PeriodPreset.Last30Days => Days(today.AddDays(-29), today),
            PeriodPreset.ThisMonth => Days(new DateTime(today.Year, today.Month, 1), today),
            PeriodPreset.ThisYear => Days(new DateTime(today.Year, 1, 1), today),
            PeriodPreset.Custom => Custom(customFrom, customTo),
            _ => (null, null),
        };
    }

    public static (DateTimeOffset From, DateTimeOffset To) Days(DateTime firstDay, DateTime lastDay)
    {
        var from = new DateTimeOffset(firstDay.Date, Format.WibOffset);
        var to = new DateTimeOffset(lastDay.Date.AddDays(1), Format.WibOffset);
        return (from.ToUniversalTime(), to.ToUniversalTime());
    }

    private static (DateTimeOffset?, DateTimeOffset?) Custom(DateTime? from, DateTime? to)
    {
        if (from is null && to is null) return (null, null);
        if (from is { } f && to is { } t && f.Date > t.Date) (from, to) = (t, f);

        DateTimeOffset? start = from is { } a ? new DateTimeOffset(a.Date, Format.WibOffset).ToUniversalTime() : null;
        DateTimeOffset? end = to is { } b ? new DateTimeOffset(b.Date.AddDays(1), Format.WibOffset).ToUniversalTime() : null;
        return (start, end);
    }

    public static string Label(PeriodPreset preset) => preset switch
    {
        PeriodPreset.All => "Semua waktu",
        PeriodPreset.Today => "Hari ini",
        PeriodPreset.Last7Days => "7 hari terakhir",
        PeriodPreset.Last30Days => "30 hari terakhir",
        PeriodPreset.ThisMonth => "Bulan ini",
        PeriodPreset.ThisYear => "Tahun ini",
        PeriodPreset.Custom => "Rentang tanggal",
        _ => preset.ToString(),
    };
}
