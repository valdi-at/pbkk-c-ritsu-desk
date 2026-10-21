using System.Collections.ObjectModel;
using System.Windows.Input;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels;

public enum NavKey
{
    MyDashboard,
    MyBookings,
    DriverStatus,
    AdminDashboard,
    Users,
    Applications,
    Bookings,
    PaymentMethods,
    Finance,
    Profile,
}

public sealed class NavItem : ObservableObject
{
    private readonly Func<PageViewModel> _factory;
    private PageViewModel? _page;

    public NavItem(NavKey key, string title, string glyph, string? section, Func<PageViewModel> factory)
    {
        Key = key;
        Title = title;
        Glyph = glyph;
        Section = section;
        _factory = factory;
    }

    public NavKey Key { get; }

    public string Title { get; }

    public string Glyph { get; }

    public string? Section { get; }

    public bool HasSection => Section is not null;

    public PageViewModel Page => _page ??= _factory();

    public PageViewModel? CreatedPage => _page;
}

public sealed class ShellViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly IThemeService _theme;
    private NavItem? _selected;
    private PageViewModel? _currentPage;

    public ShellViewModel(UserDto user, PageFactory pages, ISessionService session, IThemeService theme, Func<Task> logout)
    {
        _session = session;
        _theme = theme;
        User = user;
        Role = user.Role;

        pages.Navigate = Navigate;
        NavItems = new ObservableCollection<NavItem>(BuildNav(user.Role, pages));

        LogoutCommand = new AsyncRelayCommand(logout);
        ToggleThemeCommand = new RelayCommand(() => _theme.Apply(IsDarkTheme ? AppTheme.Light : AppTheme.Dark));
        _theme.Changed += OnThemeChanged;

        Selected = NavItems.FirstOrDefault();
    }

    public UserDto User { get; private set; }

    public string Role { get; }

    public string Username => User.Username;

    public string Email => User.Email;

    public string Initial => string.IsNullOrEmpty(User.Username) ? "?" : User.Username[..1].ToUpperInvariant();

    public string RoleLabel => Labels.Role(User.Role);

    public BadgeKind RoleKind => Labels.RoleKind(User.Role);

    public ObservableCollection<NavItem> NavItems { get; }

    public NavItem? Selected
    {
        get => _selected;
        set
        {
            if (value is null || !SetProperty(ref _selected, value)) return;
            CurrentPage = value.Page;
            _ = value.Page.ActivateAsync();
        }
    }

    public PageViewModel? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public bool IsDarkTheme => _theme.Current == AppTheme.Dark;

    public string ThemeGlyph => IsDarkTheme ? Glyphs.Sun : Glyphs.Moon;

    public string ThemeText => IsDarkTheme ? "Mode terang" : "Mode gelap";

    public ICommand LogoutCommand { get; }

    public ICommand ToggleThemeCommand { get; }

    public void Navigate(NavKey key)
    {
        var item = NavItems.FirstOrDefault(n => n.Key == key);
        if (item is not null) Selected = item;
    }

    public void UpdateUser(UserDto user)
    {
        User = user;
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(Initial));
    }

    public void Detach()
    {
        _theme.Changed -= OnThemeChanged;
        foreach (var item in NavItems) item.CreatedPage?.Cancel();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(ThemeGlyph));
        OnPropertyChanged(nameof(ThemeText));
    }

    private static IEnumerable<NavItem> BuildNav(string role, PageFactory pages)
    {
        if (role == Roles.Admin)
        {
            yield return new NavItem(NavKey.AdminDashboard, "Dashboard", Glyphs.Home, "MENU UTAMA", pages.AdminDashboard);
            yield return new NavItem(NavKey.Applications, "Pengajuan Driver", Glyphs.Document, null, pages.Applications);
            yield return new NavItem(NavKey.Bookings, "Pemesanan", Glyphs.Car, null, pages.AdminBookings);
            yield return new NavItem(NavKey.Users, "Pengguna", Glyphs.People, "MASTER DATA", pages.Users);
            yield return new NavItem(NavKey.PaymentMethods, "Metode Pembayaran", Glyphs.Money, null, pages.PaymentMethods);
            yield return new NavItem(NavKey.Finance, "Laporan Keuangan", Glyphs.Chart, "LAPORAN", pages.Finance);
        }
        else
        {
            yield return new NavItem(NavKey.MyDashboard, "Dashboard Saya", Glyphs.Home, "MENU UTAMA", pages.MyDashboard);
            yield return new NavItem(NavKey.MyBookings, "Riwayat Perjalanan", Glyphs.History, null, pages.MyBookings);
            yield return new NavItem(NavKey.DriverStatus, "Status Driver", Glyphs.Ticket, null, pages.DriverStatus);
        }
        yield return new NavItem(NavKey.Profile, "Profil & Pengaturan", Glyphs.Settings, "AKUN", pages.Profile);
    }
}
