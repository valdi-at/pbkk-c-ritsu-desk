using System.IO;
using System.Text;

namespace RITSU_Desk.Infrastructure;

public static class AppLog
{
    private static readonly object Gate = new();
    private const long MaxBytes = 2 * 1024 * 1024;
    private static string? _path;

    public static string? FilePath => _path;

    public static void Initialize(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "ritsu-desk.log");
            if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
            {
                File.Move(_path, Path.Combine(directory, "ritsu-desk.old.log"), overwrite: true);
            }
        }
        catch
        {
            _path = null;
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        if (_path is null) return;

        var line = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
            .Append(' ').Append(level).Append(' ').Append(message);
        if (ex is not null) line.AppendLine().Append(ex);

        try
        {
            lock (Gate) File.AppendAllText(_path, line.AppendLine().ToString());
        }
        catch
        {
        }
    }
}
