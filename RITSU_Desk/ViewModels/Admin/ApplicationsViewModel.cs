using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Admin;

public sealed class ApplicationsViewModel : PageViewModel
{
    private readonly IRitsuApiClient _api;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private Option<string?> _selectedStatus;
    private ApplicationItem? _selectedItem;
    private ImageSource? _licenseImage;
    private bool _isLicenseLoading;
    private string? _licenseError;
    private bool _isActing;
    private bool _replacingItems;
    private CancellationTokenSource? _licenseCts;

    public ApplicationsViewModel(IRitsuApiClient api, IDialogService dialogs, INotificationService notifications)
        : base("Pengajuan Driver", "Tinjau data dan foto SIM, lalu setujui atau tolak")
    {
        _api = api;
        _dialogs = dialogs;
        _notifications = notifications;

        StatusOptions =
        [
            new("Semua status", null),
            new(Labels.ApplicationStatus(ApplicationStatuses.Pending), ApplicationStatuses.Pending),
            new(Labels.ApplicationStatus(ApplicationStatuses.Approved), ApplicationStatuses.Approved),
            new(Labels.ApplicationStatus(ApplicationStatuses.Declined), ApplicationStatuses.Declined),
        ];
        _selectedStatus = StatusOptions[0];

        Pager = new PagerViewModel();
        Pager.Changed += (_, _) => _ = RefreshAsync();

        CloseDetailCommand = new RelayCommand(() => SelectedItem = null);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => SelectedItem is { IsPending: true } && !IsActing);
        RejectCommand = new AsyncRelayCommand(RejectAsync, () => SelectedItem is { IsPending: true } && !IsActing);
        ReloadLicenseCommand = new AsyncRelayCommand(() => SelectedItem is { } s ? LoadLicenseAsync(s) : Task.CompletedTask);
    }

    public ObservableCollection<ApplicationItem> Items { get; } = [];

    public PagerViewModel Pager { get; }

    public IReadOnlyList<Option<string?>> StatusOptions { get; }

    public Option<string?> SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (value is null || !SetProperty(ref _selectedStatus, value)) return;
            Pager.Reset();
            _ = RefreshAsync();
        }
    }

    public ApplicationItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            var previous = _selectedItem;
            if (_replacingItems || !SetProperty(ref _selectedItem, value)) return;
            OnPropertyChanged(nameof(IsDetailOpen));
            CommandManager.InvalidateRequerySuggested();

            if (value is null)
            {
                _licenseCts?.Cancel();
                LicenseImage = null;
                LicenseError = null;
                IsLicenseLoading = false;
            }
            else if (previous?.Id != value.Id || LicenseImage is null)
            {
                _ = LoadLicenseAsync(value);
            }
        }
    }

    public bool IsDetailOpen => SelectedItem is not null;

    public ImageSource? LicenseImage
    {
        get => _licenseImage;
        private set => SetProperty(ref _licenseImage, value);
    }

    public bool IsLicenseLoading
    {
        get => _isLicenseLoading;
        private set => SetProperty(ref _isLicenseLoading, value);
    }

    public string? LicenseError
    {
        get => _licenseError;
        private set => SetProperty(ref _licenseError, value);
    }

    public bool IsActing
    {
        get => _isActing;
        private set
        {
            if (SetProperty(ref _isActing, value)) CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsEmpty => HasLoaded && !IsBusy && !HasError && Items.Count == 0;

    public string EmptyText => SelectedStatus.Value is null
        ? "Belum ada pengajuan driver."
        : $"Tidak ada pengajuan berstatus {SelectedStatus.Label.ToLowerInvariant()}.";

    public ICommand CloseDetailCommand { get; }

    public ICommand ApproveCommand { get; }

    public ICommand RejectCommand { get; }

    public ICommand ReloadLicenseCommand { get; }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var result = await _api.GetApplicationsAsync(new ApplicationQuery
        {
            Page = Pager.Page,
            PageSize = Pager.PageSize,
            Status = SelectedStatus.Value,
        }, ct);
        ct.ThrowIfCancellationRequested();

        var selectedId = SelectedItem?.Id;
        _replacingItems = true;
        try
        {
            Items.Clear();
            foreach (var a in result.Items) Items.Add(new ApplicationItem(a));
        }
        finally
        {
            _replacingItems = false;
        }
        Pager.Update(result.Page, result.Total);

        var reselect = selectedId is { } id ? Items.FirstOrDefault(i => i.Id == id) : null;
        if (reselect is null)
        {
            SelectedItem = null;
        }
        else
        {
            _selectedItem = reselect;
            OnPropertyChanged(nameof(SelectedItem));
            CommandManager.InvalidateRequerySuggested();
        }
        OnStateChanged();
    }

    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private async Task LoadLicenseAsync(ApplicationItem item)
    {
        _licenseCts?.Cancel();
        var cts = _licenseCts = new CancellationTokenSource();

        LicenseImage = null;
        LicenseError = null;
        if (string.IsNullOrEmpty(item.Dto.LicenseUrl))
        {
            LicenseError = "Foto SIM tidak tersedia.";
            return;
        }

        IsLicenseLoading = true;
        try
        {
            var bytes = await _api.GetLicenseImageAsync(item.Dto.LicenseUrl, cts.Token);
            if (cts.IsCancellationRequested) return;
            LicenseImage = Decode(bytes) ?? throw new InvalidDataException("Unsupported image");
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.Unauthorized)
        {
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested) return;
            LicenseError = ex is ApiException { Code: ApiErrorCodes.NotFound }
                ? "File foto SIM tidak ditemukan di server."
                : "Foto SIM tidak dapat dimuat.";
            if (ex is not ApiException) AppLog.Warn($"License image for application {item.Id} could not be decoded: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_licenseCts, cts)) IsLicenseLoading = false;
        }
    }

    internal static ImageSource? Decode(byte[] bytes)
    {
        if (bytes.Length == 0) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or InvalidOperationException or ArgumentException or IOException)
        {
            return null;
        }
    }

    private async Task ApproveAsync()
    {
        if (SelectedItem is not { IsPending: true } item) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "Setujui pengajuan driver",
            $"Setujui pengajuan {item.Username}? Role akun ini akan berubah menjadi Driver.",
            "Setujui");
        if (!confirmed) return;

        await ActAsync(
            () => _api.ApproveApplicationAsync(item.Id),
            $"Pengajuan {item.Username} disetujui. Akunnya sekarang driver.");
    }

    private async Task RejectAsync()
    {
        if (SelectedItem is not { IsPending: true } item) return;

        var reason = await _dialogs.AskRejectReasonAsync(item.Username);
        if (reason is null) return;

        await ActAsync(
            () => _api.RejectApplicationAsync(item.Id, reason),
            $"Pengajuan {item.Username} ditolak.");
    }

    private async Task ActAsync(Func<Task> action, string successMessage)
    {
        IsActing = true;
        try
        {
            await action();
            _notifications.Success(successMessage);
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.Unauthorized)
        {
            return;
        }
        catch (ApiException ex) when (ex.Code is ApiErrorCodes.AlreadyReviewed or ApiErrorCodes.NotFound)
        {
            _notifications.Warning(ErrorMessages.For(ex));
        }
        catch (Exception ex)
        {
            _notifications.Error(ErrorMessages.For(ex));
            return;
        }
        finally
        {
            IsActing = false;
        }
        await RefreshAsync();
    }
}
