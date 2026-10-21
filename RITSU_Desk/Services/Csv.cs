using System.IO;
using System.Text;

namespace RITSU_Desk.Services;

public static class Csv
{
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (value[0] is '=' or '+' or '-' or '@' &&
            !decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    public static string Build(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(',', header.Select(Escape)).Append("\r\n");
        foreach (var row in rows) sb.AppendJoin(',', row.Select(Escape)).Append("\r\n");
        return sb.ToString();
    }

    public static Task WriteAsync(string path, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string?>> rows) =>
        File.WriteAllTextAsync(path, Build(header, rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    public static string Number(decimal value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
