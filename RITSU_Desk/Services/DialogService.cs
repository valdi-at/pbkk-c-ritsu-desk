using RITSU_Desk.Infrastructure;
using RITSU_Desk.ViewModels.Dialogs;

namespace RITSU_Desk.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool danger = false);

    Task<string?> AskRejectReasonAsync(string applicantName);

    Task<string?> PickRoleAsync(string username, string currentRole);
}

public sealed class DialogService : ObservableObject, IDialogService
{
    private DialogViewModel? _current;

    public DialogViewModel? Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool danger = false) =>
        ShowAsync(new ConfirmDialogViewModel(title, message, confirmText, danger));

    public Task<string?> AskRejectReasonAsync(string applicantName) =>
        ShowAsync(new RejectReasonDialogViewModel(applicantName));

    public Task<string?> PickRoleAsync(string username, string currentRole) =>
        ShowAsync(new ChangeRoleDialogViewModel(username, currentRole));

    public void CloseAll() => Current?.Cancel();

    private async Task<T> ShowAsync<T>(DialogViewModel<T> dialog)
    {
        Current?.Cancel();
        Current = dialog;
        try
        {
            return await dialog.Result;
        }
        finally
        {
            if (ReferenceEquals(Current, dialog)) Current = null;
        }
    }
}
