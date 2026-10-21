using System.Windows.Input;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Dialogs;

public abstract class DialogViewModel : ObservableObject
{
    protected DialogViewModel(string title)
    {
        Title = title;
        CancelCommand = new RelayCommand(Cancel);
    }

    public string Title { get; }

    public ICommand CancelCommand { get; }

    public abstract void Cancel();
}

public abstract class DialogViewModel<T> : DialogViewModel
{
    private readonly TaskCompletionSource<T> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected DialogViewModel(string title) : base(title)
    {
    }

    public Task<T> Result => _result.Task;

    protected void Close(T value) => _result.TrySetResult(value);
}

public sealed class ConfirmDialogViewModel : DialogViewModel<bool>
{
    public ConfirmDialogViewModel(string title, string message, string confirmText, bool isDanger) : base(title)
    {
        Message = message;
        ConfirmText = confirmText;
        IsDanger = isDanger;
        ConfirmCommand = new RelayCommand(() => Close(true));
    }

    public string Message { get; }

    public string ConfirmText { get; }

    public bool IsDanger { get; }

    public ICommand ConfirmCommand { get; }

    public override void Cancel() => Close(false);
}

public sealed class RejectReasonDialogViewModel : DialogViewModel<string?>
{
    public const int MaxLength = 500;

    private string _reason = string.Empty;
    private string? _validationMessage;

    public RejectReasonDialogViewModel(string applicantName) : base("Tolak pengajuan driver")
    {
        ApplicantName = applicantName;
        SubmitCommand = new RelayCommand(Submit);
    }

    public string ApplicantName { get; }

    public string Reason
    {
        get => _reason;
        set
        {
            if (SetProperty(ref _reason, value) && !string.IsNullOrWhiteSpace(value)) ValidationMessage = null;
        }
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public ICommand SubmitCommand { get; }

    public override void Cancel() => Close(null);

    private void Submit()
    {
        var trimmed = Reason.Trim();
        if (trimmed.Length == 0)
        {
            ValidationMessage = "Alasan penolakan wajib diisi.";
            return;
        }
        Close(trimmed.Length > MaxLength ? trimmed[..MaxLength] : trimmed);
    }
}

public sealed record RoleOption(string Value, string Label, string Description);

public sealed class ChangeRoleDialogViewModel : DialogViewModel<string?>
{
    private RoleOption _selected;

    public ChangeRoleDialogViewModel(string username, string currentRole) : base("Ubah role pengguna")
    {
        Username = username;
        CurrentRole = currentRole;
        Options =
        [
            new(Roles.Customer, Labels.Role(Roles.Customer), "Memesan perjalanan di RITSU web."),
            new(Roles.Driver, Labels.Role(Roles.Driver), "Menerima dan mengantar pesanan."),
            new(Roles.Admin, Labels.Role(Roles.Admin), "Akses penuh ke modul admin."),
        ];
        _selected = Options.FirstOrDefault(o => o.Value == currentRole) ?? Options[0];
        SaveCommand = new RelayCommand(() => Close(Selected.Value), () => Selected.Value != CurrentRole);
    }

    public string Username { get; }

    public string CurrentRole { get; }

    public string CurrentRoleLabel => Labels.Role(CurrentRole);

    public IReadOnlyList<RoleOption> Options { get; }

    public RoleOption Selected
    {
        get => _selected;
        set
        {
            if (value is not null) SetProperty(ref _selected, value);
        }
    }

    public ICommand SaveCommand { get; }

    public override void Cancel() => Close(null);
}
