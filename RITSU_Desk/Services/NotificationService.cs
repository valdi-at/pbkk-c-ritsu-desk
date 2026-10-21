using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using RITSU_Desk.Infrastructure;

namespace RITSU_Desk.Services;

public enum ToastKind
{
    Success,
    Info,
    Warning,
    Error,
}

public interface INotificationService
{
    void Show(ToastKind kind, string message);

    void Success(string message) => Show(ToastKind.Success, message);

    void Info(string message) => Show(ToastKind.Info, message);

    void Warning(string message) => Show(ToastKind.Warning, message);

    void Error(string message) => Show(ToastKind.Error, message);
}

public sealed class Toast
{
    public Toast(ToastKind kind, string message, Action<Toast> close)
    {
        Kind = kind;
        Message = message;
        CloseCommand = new RelayCommand(() => close(this));
    }

    public ToastKind Kind { get; }

    public string Message { get; }

    public string Title => Kind switch
    {
        ToastKind.Success => "Berhasil",
        ToastKind.Warning => "Perhatian",
        ToastKind.Error => "Gagal",
        _ => "Info",
    };

    public string Glyph => Kind switch
    {
        ToastKind.Success => Glyphs.Completed,
        ToastKind.Warning => Glyphs.Warning,
        ToastKind.Error => Glyphs.Error,
        _ => Glyphs.Info,
    };

    public BadgeKind BadgeKind => Kind switch
    {
        ToastKind.Success => BadgeKind.Success,
        ToastKind.Warning => BadgeKind.Warning,
        ToastKind.Error => BadgeKind.Danger,
        _ => BadgeKind.Info,
    };

    public ICommand CloseCommand { get; }
}

public sealed class NotificationService : INotificationService
{
    private const int MaxVisible = 4;

    public ObservableCollection<Toast> Toasts { get; } = [];

    public void Show(ToastKind kind, string message)
    {
        var toast = new Toast(kind, message, Remove);
        Toasts.Add(toast);
        while (Toasts.Count > MaxVisible) Toasts.RemoveAt(0);

        var timer = new DispatcherTimer
        {
            Interval = kind is ToastKind.Error or ToastKind.Warning ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(4.5),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Remove(toast);
        };
        timer.Start();
    }

    private void Remove(Toast toast) => Toasts.Remove(toast);
}
