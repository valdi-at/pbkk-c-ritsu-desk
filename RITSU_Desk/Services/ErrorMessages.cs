using System.Text.Json;
using RITSU_Desk.Api;

namespace RITSU_Desk.Services;

public static class ErrorMessages
{
    public const string SessionExpired = "Sesi berakhir, silakan login lagi.";

    private static readonly Dictionary<string, string> FieldLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "Email",
        ["password"] = "Password",
        ["role"] = "Role",
        ["reason"] = "Alasan",
        ["q"] = "Pencarian",
        ["stage"] = "Stage",
        ["paid"] = "Status bayar",
        ["from"] = "Tanggal awal",
        ["to"] = "Tanggal akhir",
        ["page"] = "Halaman",
        ["pageSize"] = "Jumlah per halaman",
        ["status"] = "Status",
        ["paymentMethodId"] = "Metode pembayaran",
    };

    public static string For(Exception ex) => ex switch
    {
        ApiException api => For(api),
        _ => "Terjadi kesalahan tak terduga. Coba lagi.",
    };

    public static string For(ApiException ex) => ex.Code switch
    {
        ApiErrorCodes.ValidationError => Validation(ex.Details),
        ApiErrorCodes.InvalidCredentials => "Email atau password salah.",
        ApiErrorCodes.Unauthorized => SessionExpired,
        ApiErrorCodes.Forbidden => "Anda tidak punya akses ke fitur ini.",
        ApiErrorCodes.SelfActionForbidden => "Anda tidak bisa melakukan aksi ini pada akun sendiri.",
        ApiErrorCodes.NotFound => "Data tidak ditemukan. Mungkin sudah dihapus.",
        ApiErrorCodes.AlreadyReviewed => "Pengajuan ini sudah ditinjau. Daftar dimuat ulang.",
        ApiErrorCodes.HasDependencies => Dependencies(ex.Details),
        ApiErrorCodes.RateLimited => "Terlalu banyak percobaan login. Coba lagi beberapa menit lagi.",
        ApiErrorCodes.Internal => "Terjadi kesalahan di server. Coba lagi nanti.",
        ApiErrorCodes.Network => "Server tidak dapat dihubungi. Periksa koneksi jaringan dan alamat server, lalu coba lagi.",
        ApiErrorCodes.Timeout => "Server tidak merespons dalam 15 detik. Coba lagi.",
        ApiErrorCodes.InvalidResponse => "Respons server tidak dikenali. Pastikan alamat server mengarah ke RITSU.",
        ApiErrorCodes.UnsupportedVersion => "Versi API server tidak didukung oleh aplikasi ini.",
        _ when ex.StatusCode >= 500 => "Terjadi kesalahan di server. Coba lagi nanti.",
        _ => $"Permintaan gagal ({ex.Code}). Coba lagi.",
    };

    private static string Validation(JsonElement? details)
    {
        const string message = "Data yang diisi belum valid.";
        if (details is not { ValueKind: JsonValueKind.Object } d ||
            !d.TryGetProperty("fieldErrors", out var fields) ||
            fields.ValueKind != JsonValueKind.Object)
        {
            return message;
        }

        var names = fields.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Array && p.Value.GetArrayLength() > 0)
            .Select(p => FieldLabels.TryGetValue(p.Name, out var label) ? label : p.Name)
            .Distinct()
            .ToList();
        return names.Count == 0 ? message : $"{message} Periksa: {string.Join(", ", names)}.";
    }

    private static string Dependencies(JsonElement? details)
    {
        int bookings = 0, chats = 0;
        if (details is { ValueKind: JsonValueKind.Object } d)
        {
            bookings = Int(d, "bookingsAsCustomer") + Int(d, "bookingsAsDriver");
            chats = Int(d, "chatMessages");
        }
        return $"Pengguna masih punya {bookings} pemesanan dan {chats} chat, jadi tidak bisa dihapus.";
    }

    private static int Int(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}
