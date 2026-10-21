using System.Windows;

namespace RITSU_Desk.Services;

public interface IThemeService
{
    AppTheme Current { get; }

    event EventHandler? Changed;

    void Apply(AppTheme theme);
}

public sealed class ThemeService : IThemeService
{
    private const string ColorsPrefix = "Themes/Colors.";

    private readonly ISettingsService _settings;

    public ThemeService(ISettingsService settings) => _settings = settings;

    public AppTheme Current { get; private set; } = AppTheme.Light;

    public event EventHandler? Changed;

    public void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        app.ThemeMode = theme == AppTheme.Dark ? ThemeMode.Dark : ThemeMode.Light;

        var dictionaries = app.Resources.MergedDictionaries;
        var colors = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/RITSU_Desk;component/{ColorsPrefix}{theme}.xaml", UriKind.Absolute),
        };
        var existing = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains(ColorsPrefix, StringComparison.OrdinalIgnoreCase) == true);
        if (existing is not null) dictionaries.Remove(existing);
        dictionaries.Add(colors);

        var changed = Current != theme;
        Current = theme;
        _settings.SetTheme(theme);
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }
}
