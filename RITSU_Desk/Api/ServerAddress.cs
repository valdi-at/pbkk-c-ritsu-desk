using System.Net;
using System.Net.Sockets;

namespace RITSU_Desk.Api;

public static class ServerAddress
{
    public const string ApiPath = "/api/desktop/v1";

    public static bool TryNormalize(string? input, out Uri? baseUri, out string? error)
    {
        baseUri = null;
        error = null;

        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            error = "Alamat server wajib diisi.";
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal)) text = "http://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
        {
            error = "Alamat server tidak valid. Contoh: http://localhost:3000";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            error = "Alamat server tidak boleh berisi query, fragment, atau kredensial.";
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !AllowsPlainHttp(uri))
        {
            error = "Server di luar jaringan lokal wajib memakai HTTPS.";
            return false;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith(ApiPath, StringComparison.OrdinalIgnoreCase)) path += ApiPath;

        baseUri = new UriBuilder(uri.Scheme, uri.Host, uri.Port, path + "/").Uri;
        return true;
    }

    public static bool AllowsPlainHttp(Uri uri)
    {
        if (uri.IsLoopback) return true;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ip)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168);
        }
        return ip.AddressFamily == AddressFamily.InterNetworkV6 && (ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal);
    }

    public static Uri WebOrigin(Uri baseUri)
    {
        var path = baseUri.AbsolutePath.TrimEnd('/');
        var root = path.EndsWith(ApiPath, StringComparison.OrdinalIgnoreCase) ? path[..^ApiPath.Length] : string.Empty;
        return new UriBuilder(baseUri.Scheme, baseUri.Host, baseUri.Port, root + "/").Uri;
    }

    public static string Display(Uri baseUri) => baseUri.GetLeftPart(UriPartial.Authority);
}
