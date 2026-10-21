using System.Collections.ObjectModel;
using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels.Admin;

public sealed class UserDetailViewModel
{
    public UserDetailViewModel(UserDetailDto dto, Guid? currentUserId)
    {
        User = new UserItem(dto.User, currentUserId);
        Dependencies = dto.Dependencies;
        LatestApplication = dto.LatestApplication is null ? null : new ApplicationItem(dto.LatestApplication);
    }

    public UserItem User { get; }

    public UserDependenciesDto Dependencies { get; }

    public ApplicationItem? LatestApplication { get; }

    public bool HasApplication => LatestApplication is not null;

    public string BookingsAsCustomerText => Format.Number(Dependencies.BookingsAsCustomer);

    public string BookingsAsDriverText => Format.Number(Dependencies.BookingsAsDriver);

    public string ChatMessagesText => Format.Number(Dependencies.ChatMessages);

    public bool HasDependencies => Dependencies.TotalBookings + Dependencies.ChatMessages > 0;

    public string DependencyHint =>
        $"Pengguna ini terhubung ke {Format.Number(Dependencies.TotalBookings)} pemesanan dan {Format.Number(Dependencies.ChatMessages)} chat, jadi server akan menolak penghapusan.";
}

public sealed class UsersViewModel : PageViewModel
{
    private readonly IRitsuApiClient _api;
    private readonly ISessionService _session;
    private readonly IDialogService _dialogs;
    private readonly INotificationService _notifications;
    private string _searchText = string.Empty;
    private Option<string?> _selectedRole;
    private UserItem? _selectedItem;
    private UserDetailViewModel? _detail;
    private bool _isDetailLoading;
    private string? _detailError;
    private CancellationTokenSource? _searchDelay;
    private CancellationTokenSource? _detailCts;
    private bool _replacingItems;

    public UsersViewModel(IRitsuApiClient api, ISessionService session, IDialogService dialogs, INotificationService notifications)
        : base("Pengguna", "Akun RITSU, role, dan relasi data")
    {
        _api = api;
        _session = session;
        _dialogs = dialogs;
        _notifications = notifications;

        RoleOptions =
        [
            new("Semua role", null),
            .. Roles.All.Select(r => new Option<string?>(Labels.Role(r), r)),
        ];
        _selectedRole = RoleOptions[0];

        Pager = new PagerViewModel();
        Pager.Changed += (_, _) => _ = RefreshAsync();

        SearchCommand = new RelayCommand(ApplyFilterChange);
        CloseDetailCommand = new RelayCommand(() => SelectedItem = null);
        ChangeRoleCommand = new AsyncRelayCommand(ChangeRoleAsync, () => SelectedItem is { IsSelf: false });
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedItem is { IsSelf: false });
    }

    public ObservableCollection<UserItem> Items { get; } = [];

    public PagerViewModel Pager { get; }

    public IReadOnlyList<Option<string?>> RoleOptions { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) DebounceSearch();
        }
    }

    public Option<string?> SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (value is not null && SetProperty(ref _selectedRole, value)) ApplyFilterChange();
        }
    }

    public UserItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_replacingItems || !SetProperty(ref _selectedItem, value)) return;
            OnPropertyChanged(nameof(IsDetailOpen));
            OnPropertyChanged(nameof(IsSelfSelected));
            CommandManager.InvalidateRequerySuggested();
            if (value is null)
            {
                _detailCts?.Cancel();
                Detail = null;
                DetailError = null;
                IsDetailLoading = false;
            }
            else
            {
                _ = LoadDetailAsync(value.Id);
            }
        }
    }

    public bool IsDetailOpen => SelectedItem is not null;

    public bool IsSelfSelected => SelectedItem?.IsSelf == true;

    public UserDetailViewModel? Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public bool IsDetailLoading
    {
        get => _isDetailLoading;
        private set => SetProperty(ref _isDetailLoading, value);
    }

    public string? DetailError
    {
        get => _detailError;
        private set => SetProperty(ref _detailError, value);
    }

    public bool IsEmpty => HasLoaded && !IsBusy && !HasError && Items.Count == 0;

    public string EmptyText => SearchText.Trim().Length > 0 || SelectedRole.Value is not null
        ? "Tidak ada pengguna yang cocok dengan pencarian."
        : "Belum ada pengguna.";

    public ICommand SearchCommand { get; }

    public ICommand CloseDetailCommand { get; }

    public ICommand ChangeRoleCommand { get; }

    public ICommand DeleteCommand { get; }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var search = SearchText.Trim();
        var result = await _api.GetUsersAsync(new UserQuery
        {
            Page = Pager.Page,
            PageSize = Pager.PageSize,
            Search = search.Length == 0 ? null : search,
            Role = SelectedRole.Value,
        }, ct);
        ct.ThrowIfCancellationRequested();

        var selectedId = SelectedItem?.Id;
        var me = _session.User?.Id;
        _replacingItems = true;
        try
        {
            Items.Clear();
            foreach (var u in result.Items) Items.Add(new UserItem(u, me));
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
            _ = LoadDetailAsync(reselect.Id);
        }
        OnStateChanged();
    }

    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    private void ApplyFilterChange()
    {
        _searchDelay?.Cancel();
        Pager.Reset();
        _ = RefreshAsync();
    }

    private async void DebounceSearch()
    {
        _searchDelay?.Cancel();
        var cts = _searchDelay = new CancellationTokenSource();
        try
        {
            await Task.Delay(400, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        ApplyFilterChange();
    }

    private async Task LoadDetailAsync(Guid id)
    {
        _detailCts?.Cancel();
        var cts = _detailCts = new CancellationTokenSource();

        IsDetailLoading = true;
        DetailError = null;
        if (Detail?.User.Id != id) Detail = null;
        try
        {
            var dto = await _api.GetUserAsync(id, cts.Token);
            if (!cts.IsCancellationRequested) Detail = new UserDetailViewModel(dto, _session.User?.Id);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.Unauthorized)
        {
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested) DetailError = ErrorMessages.For(ex);
        }
        finally
        {
            if (ReferenceEquals(_detailCts, cts)) IsDetailLoading = false;
        }
    }

    private async Task ChangeRoleAsync()
    {
        if (SelectedItem is not { } user) return;

        var role = await _dialogs.PickRoleAsync(user.Username, user.Dto.Role);
        if (role is null) return;

        try
        {
            var updated = await _api.ChangeUserRoleAsync(user.Id, role);
            _notifications.Success($"Role {updated.Username} diubah menjadi {Labels.Role(updated.Role)}.");
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.Unauthorized)
        {
            return;
        }
        catch (Exception ex)
        {
            _notifications.Error(ErrorMessages.For(ex));
        }
        await RefreshAsync();
    }

    private async Task DeleteAsync()
    {
        if (SelectedItem is not { } user) return;

        var confirmed = await _dialogs.ConfirmAsync(
            "Hapus pengguna",
            $"Hapus akun {user.Username} ({user.Email})? Pengajuan driver miliknya ikut terhapus dan tindakan ini tidak bisa dibatalkan.",
            "Hapus",
            danger: true);
        if (!confirmed) return;

        try
        {
            await _api.DeleteUserAsync(user.Id);
            _notifications.Success($"Akun {user.Username} sudah dihapus.");
            SelectedItem = null;
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.Unauthorized)
        {
            return;
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.NotFound)
        {
            _notifications.Warning(ErrorMessages.For(ex));
            SelectedItem = null;
        }
        catch (Exception ex)
        {
            _notifications.Error(ErrorMessages.For(ex));
        }
        await RefreshAsync();
    }
}
