using System.Windows.Input;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels;

public sealed class PagerViewModel : ObservableObject
{
    public const int MaxPageSize = 100;

    private int _page = 1;
    private int _pageSize;
    private int _total;

    public PagerViewModel(int pageSize = 25)
    {
        _pageSize = pageSize;
        PreviousCommand = new RelayCommand(() => GoTo(Page - 1), () => Page > 1);
        NextCommand = new RelayCommand(() => GoTo(Page + 1), () => Page < TotalPages);
        FirstCommand = new RelayCommand(() => GoTo(1), () => Page > 1);
        LastCommand = new RelayCommand(() => GoTo(TotalPages), () => Page < TotalPages);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<int> PageSizeOptions { get; } = [10, 25, 50, 100];

    public int Page
    {
        get => _page;
        private set
        {
            if (SetProperty(ref _page, value)) RaiseTexts();
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            var size = Math.Clamp(value, 1, MaxPageSize);
            if (!SetProperty(ref _pageSize, size)) return;
            _page = 1;
            OnPropertyChanged(nameof(Page));
            RaiseTexts();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public int Total
    {
        get => _total;
        private set
        {
            if (SetProperty(ref _total, value)) RaiseTexts();
        }
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public string RangeText
    {
        get
        {
            if (Total == 0) return "Tidak ada data";
            var start = (Page - 1) * PageSize + 1;
            var end = Math.Min(Total, Page * PageSize);
            return $"Menampilkan {Format.Number(start)}–{Format.Number(end)} dari {Format.Number(Total)}";
        }
    }

    public string PageText => $"Halaman {Format.Number(Page)} dari {Format.Number(TotalPages)}";

    public ICommand PreviousCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand FirstCommand { get; }

    public ICommand LastCommand { get; }

    public void Reset() => Page = 1;

    public void Update(int page, int total)
    {
        Total = total;
        Page = Math.Max(1, page);
    }

    private void GoTo(int page)
    {
        page = Math.Clamp(page, 1, TotalPages);
        if (page == Page) return;
        Page = page;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseTexts()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(RangeText));
        OnPropertyChanged(nameof(PageText));
        CommandManager.InvalidateRequerySuggested();
    }
}
