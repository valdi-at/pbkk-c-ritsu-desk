using System.IO;
using System.Text.Json;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;

namespace RITSU_Desk.Services;

public enum AppTheme
{
    Light,
    Dark,
}

public interface ISettingsService
{
    Uri ApiBaseUri { get; }

    AppTheme Theme { get; }

    IReadOnlyList<string> KnownServers { get; }

    void SetApiBaseUri(Uri baseUri);

    void SetTheme(AppTheme theme);
}

public sealed class SettingsService : ISettingsService
{
    private const string FallbackServer = "http://localhost:3000/api/desktop/v1";

    private readonly string _userFile;
    private Uri _apiBaseUri;
    private AppTheme _theme;

    public SettingsService(string defaultsFile, string userFile)
    {
        _userFile = userFile;

        var defaults = Read<DefaultsFile>(defaultsFile) ?? new DefaultsFile();
        var user = Read<UserFile>(userFile) ?? new UserFile();

        KnownServers = (defaults.KnownServers ?? [])
            .Where(s => ServerAddress.TryNormalize(s, out _, out _))
            .ToList();

        _apiBaseUri = Normalize(user.ApiBaseUrl)
            ?? Normalize(defaults.ApiBaseUrl)
            ?? Normalize(FallbackServer)!;
        _theme = Enum.TryParse<AppTheme>(user.Theme, ignoreCase: true, out var theme) ? theme : AppTheme.Light;
    }

    public Uri ApiBaseUri => _apiBaseUri;

    public AppTheme Theme => _theme;

    public IReadOnlyList<string> KnownServers { get; }

    public void SetApiBaseUri(Uri baseUri)
    {
        _apiBaseUri = baseUri;
        Save();
    }

    public void SetTheme(AppTheme theme)
    {
        _theme = theme;
        Save();
    }

    private static Uri? Normalize(string? value) =>
        ServerAddress.TryNormalize(value, out var uri, out _) ? uri : null;

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_userFile)!);
            var json = JsonSerializer.Serialize(
                new UserFile { ApiBaseUrl = _apiBaseUri.ToString(), Theme = _theme.ToString() },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_userFile, json);
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not save settings", ex);
        }
    }

    private static T? Read<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Could not read settings file {Path.GetFileName(path)}", ex);
            return null;
        }
    }

    private sealed class DefaultsFile
    {
        public string? ApiBaseUrl { get; set; }

        public List<string>? KnownServers { get; set; }
    }

    private sealed class UserFile
    {
        public string? ApiBaseUrl { get; set; }

        public string? Theme { get; set; }
    }
}
