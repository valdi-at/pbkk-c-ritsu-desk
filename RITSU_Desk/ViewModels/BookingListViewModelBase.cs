using System.Collections.ObjectModel;
using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels;

public abstract class BookingListViewModelBase : PageViewModel
{
    private readonly Func<DateTimeOffset> _now;
    private string _searchText = string.Empty;
    private Option<int?> _selectedStage;
    private Option<bool?> _selectedPaid;
    private Option<PeriodPreset> _selectedPeriod;
    private DateTime? _customFrom;
    private DateTime? _customTo;
    private BookingItem? _selectedItem;
    private BookingItem? _detail;
    private bool _isDetailLoading;
    private string? _detailError;
    private CancellationTokenSource? _searchDelay;
    private CancellationTokenSource? _detailCts;
    private bool _replacingItems;

    protected BookingListViewModelBase(string title, string subtitle, Func<DateTimeOffset>? now)
        : base(title, subtitle)
    {
        _now = now ?? (() => DateTimeOffset.Now);

        StageOptions =
        [
            new("Semua stage", null),
            .. Enumerable.Range(1, 5).Select(s => new Option<int?>($"{s} · {Labels.Stage(s)}", s)),
        ];
        PaidOptions = [new("Semua status bayar", null), new("Lunas", true), new("Belum dibayar", false)];
        PeriodOptions = Enum.GetValues<PeriodPreset>().Select(p => new Option<PeriodPreset>(PeriodFilter.Label(p), p)).ToList();

        _selectedStage = StageOptions[0];
        _selectedPaid = PaidOptions[0];
        _selectedPeriod = PeriodOptions[0];

        Pager = new PagerViewModel();
        Pager.Changed += (_, _) => _ = RefreshAsync();

        SearchCommand = new RelayCommand(ApplyFilterChange);
        ClearFiltersCommand = new RelayCommand(ClearFilters, () => HasActiveFilters);
        CloseDetailCommand = new RelayCommand(() => SelectedItem = null);
    }

    public ObservableCollection<BookingItem> Items { get; } = [];

    public PagerViewModel Pager { get; }

    public IReadOnlyList<Option<int?>> StageOptions { get; }

    public IReadOnlyList<Option<bool?>> PaidOptions { get; }

    public IReadOnlyList<Option<PeriodPreset>> PeriodOptions { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) DebounceSearch();
        }
    }

    public Option<int?> SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (value is not null && SetProperty(ref _selectedStage, value)) ApplyFilterChange();
        }
    }

    public Option<bool?> SelectedPaid
    {
        get => _selectedPaid;
        set
        {
            if (value is not null && SetProperty(ref _selectedPaid, value)) ApplyFilterChange();
        }
    }

    public Option<PeriodPreset> SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (value is null || !SetProperty(ref _selectedPeriod, value)) return;
            OnPropertyChanged(nameof(IsCustomPeriod));
            ApplyFilterChange();
        }
    }

    public bool IsCustomPeriod => SelectedPeriod.Value == PeriodPreset.Custom;

    public DateTime? CustomFrom
    {
        get => _customFrom;
        set
        {
            if (SetProperty(ref _customFrom, value) && IsCustomPeriod) ApplyFilterChange();
        }
    }

    public DateTime? CustomTo
    {
        get => _customTo;
        set
        {
            if (SetProperty(ref _customTo, value) && IsCustomPeriod) ApplyFilterChange();
        }
    }

    public virtual bool HasActiveFilters =>
        SearchText.Trim().Length > 0 || SelectedStage.Value is not null || SelectedPaid.Value is not null ||
        SelectedPeriod.Value != PeriodPreset.All;

    public bool IsEmpty => HasLoaded && !IsBusy && !HasError && Items.Count == 0;

    public virtual bool ShowCustomerColumn => false;

    public virtual bool ShowDriverColumn => false;

    public virtual bool ShowPartiesColumn => true;

    public string EmptyText => HasActiveFilters
        ? "Tidak ada perjalanan yang cocok dengan filter."
        : EmptyTextNoFilters;

    protected virtual string EmptyTextNoFilters => "Belum ada perjalanan.";

    public BookingItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_replacingItems || !SetProperty(ref _selectedItem, value)) return;
            if (value is null)
            {
                _detailCts?.Cancel();
                Detail = null;
                DetailError = null;
                IsDetailLoading = false;
            }
            else
            {
                _ = LoadDetailAsync(value);
            }
        }
    }

    public BookingItem? Detail
    {
        get => _detail;
        private set
        {
            if (SetProperty(ref _detail, value)) OnPropertyChanged(nameof(IsDetailOpen));
        }
    }

    public bool IsDetailOpen => Detail is not null;

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

    public ICommand SearchCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public ICommand CloseDetailCommand { get; }

    protected abstract Task<PagedResult<BookingDto>> FetchAsync(BookingQuery query, CancellationToken ct);

    protected abstract Task<BookingDto> FetchDetailAsync(Guid id, CancellationToken ct);

    protected virtual BookingQuery BuildQuery(int page, int pageSize)
    {
        var (from, to) = PeriodFilter.Range(SelectedPeriod.Value, _now(), CustomFrom, CustomTo);
        var search = SearchText.Trim();
        return new BookingQuery
        {
            Page = page,
            PageSize = pageSize,
            Stage = SelectedStage.Value,
            Paid = SelectedPaid.Value,
            From = from,
            To = to,
            Search = search.Length == 0 ? null : search,
        };
    }

    protected override async Task LoadAsync(CancellationToken ct)
    {
        var result = await FetchAsync(BuildQuery(Pager.Page, Pager.PageSize), ct);
        ct.ThrowIfCancellationRequested();

        if (result.Items.Count == 0 && result.Total > 0 && Pager.Page > 1)
        {
            var last = Math.Max(1, (int)Math.Ceiling(result.Total / (double)Pager.PageSize));
            result = await FetchAsync(BuildQuery(last, Pager.PageSize), ct);
            ct.ThrowIfCancellationRequested();
        }

        var selectedId = SelectedItem?.Id;
        _replacingItems = true;
        try
        {
            Items.Clear();
            foreach (var dto in result.Items) Items.Add(new BookingItem(dto));
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
            _ = LoadDetailAsync(reselect);
        }

        OnStateChanged();
    }

    protected override void OnStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }

    protected void ApplyFilterChange()
    {
        _searchDelay?.Cancel();
        Pager.Reset();
        OnPropertyChanged(nameof(HasActiveFilters));
        _ = RefreshAsync();
    }

    protected virtual void ResetExtraFilters()
    {
    }

    private void ClearFilters()
    {
        _searchText = string.Empty;
        _selectedStage = StageOptions[0];
        _selectedPaid = PaidOptions[0];
        _selectedPeriod = PeriodOptions[0];
        _customFrom = null;
        _customTo = null;
        ResetExtraFilters();
        OnPropertyChanged(string.Empty);
        ApplyFilterChange();
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

    private async Task LoadDetailAsync(BookingItem item)
    {
        _detailCts?.Cancel();
        var cts = _detailCts = new CancellationTokenSource();

        Detail = item;
        DetailError = null;
        IsDetailLoading = true;
        try
        {
            var dto = await FetchDetailAsync(item.Id, cts.Token);
            if (!cts.IsCancellationRequested) Detail = new BookingItem(dto);
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
}
