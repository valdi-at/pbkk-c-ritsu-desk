using RITSU_Desk.Models;

namespace RITSU_Desk.Services;

public enum BadgeKind
{
    Neutral,
    Primary,
    Info,
    Success,
    Warning,
    Danger,
}

public static class Labels
{
    public static string Role(string? role) => role switch
    {
        Roles.Customer => "Penumpang",
        Roles.Driver => "Driver",
        Roles.Admin => "Admin",
        null or "" => Format.Dash,
        _ => role,
    };

    public static BadgeKind RoleKind(string? role) => role switch
    {
        Roles.Admin => BadgeKind.Warning,
        Roles.Driver => BadgeKind.Primary,
        _ => BadgeKind.Neutral,
    };

    public static string ApplicationStatus(string? status) => status switch
    {
        ApplicationStatuses.Pending => "Menunggu",
        ApplicationStatuses.Approved => "Disetujui",
        ApplicationStatuses.Declined => "Ditolak",
        null or "" => Format.Dash,
        _ => status,
    };

    public static BadgeKind ApplicationStatusKind(string? status) => status switch
    {
        ApplicationStatuses.Pending => BadgeKind.Warning,
        ApplicationStatuses.Approved => BadgeKind.Success,
        ApplicationStatuses.Declined => BadgeKind.Danger,
        _ => BadgeKind.Neutral,
    };

    public static string Stage(int stage) => stage switch
    {
        1 => "Mencari driver",
        2 => "Driver merespons",
        3 => "Driver menuju titik jemput",
        4 => "Dalam perjalanan",
        5 => "Selesai",
        _ => $"Stage {stage}",
    };

    public static BadgeKind StageKind(int stage) => stage switch
    {
        1 => BadgeKind.Warning,
        2 => BadgeKind.Info,
        3 or 4 => BadgeKind.Primary,
        5 => BadgeKind.Success,
        _ => BadgeKind.Neutral,
    };

    public static string Paid(bool paid) => paid ? "Lunas" : "Belum dibayar";

    public static BadgeKind PaidKind(bool paid) => paid ? BadgeKind.Success : BadgeKind.Warning;

    public static bool IsCash(string? paymentMethodName) => paymentMethodName == "Cash";

    public static string Activity(string type) => type switch
    {
        "user_created" => "Pengguna baru mendaftar",
        "booking_created" => "Pemesanan baru dibuat",
        "booking_completed" => "Perjalanan selesai",
        "application_submitted" => "Pengajuan driver masuk",
        "application_reviewed" => "Pengajuan driver ditinjau",
        _ => type,
    };

    public static string ActivityGlyph(string type) => type switch
    {
        "user_created" => Glyphs.AddFriend,
        "booking_created" => Glyphs.Car,
        "booking_completed" => Glyphs.Completed,
        "application_submitted" => Glyphs.Document,
        "application_reviewed" => Glyphs.Accept,
        _ => Glyphs.Info,
    };

    public static BadgeKind ActivityKind(string type) => type switch
    {
        "user_created" => BadgeKind.Primary,
        "booking_created" => BadgeKind.Info,
        "booking_completed" => BadgeKind.Success,
        "application_submitted" => BadgeKind.Warning,
        _ => BadgeKind.Neutral,
    };
}

public static class Glyphs
{
    public const string Home = "";
    public const string History = "";
    public const string Car = "";
    public const string Contact = "";
    public const string People = "";
    public const string AddFriend = "";
    public const string Document = "";
    public const string Calendar = "";
    public const string Money = "";
    public const string Chart = "";
    public const string Settings = "";
    public const string Refresh = "";
    public const string Search = "";
    public const string Accept = "";
    public const string Cancel = "";
    public const string Delete = "";
    public const string Edit = "";
    public const string Download = "";
    public const string Info = "";
    public const string Warning = "";
    public const string Error = "";
    public const string Completed = "";
    public const string Clock = "";
    public const string Wallet = "";
    public const string Route = "";
    public const string Globe = "";
    public const string SignOut = "";
    public const string Sun = "";
    public const string Moon = "";
    public const string Lock = "";
    public const string Mail = "";
    public const string Phone = "";
    public const string Image = "";
    public const string ChevronLeft = "";
    public const string ChevronRight = "";
    public const string Shield = "";
    public const string Trending = "";
    public const string Ticket = "";
}
