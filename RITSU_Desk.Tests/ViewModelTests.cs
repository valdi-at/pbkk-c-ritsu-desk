using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Models;
using RITSU_Desk.Services;
using RITSU_Desk.ViewModels;
using RITSU_Desk.ViewModels.Admin;
using RITSU_Desk.ViewModels.Dialogs;
using RITSU_Desk.ViewModels.Student;

namespace RITSU_Desk.Tests;

public class LoginViewModelTests
{
    private static (LoginViewModel Vm, FakeApi Api, SessionService Session) Create()
    {
        var api = new FakeApi();
        var session = new SessionService();
        return (new LoginViewModel(api, session, new FakeSettings(), new FakePlatform()), api, session);
    }

    private static Task Login(LoginViewModel vm) => ((AsyncRelayCommand)vm.LoginCommand).ExecuteAsync();

    [Fact]
    public async Task Empty_or_malformed_email_is_caught_before_calling_the_api()
    {
        var (vm, api, _) = Create();
        vm.Email = "nadia";
        vm.Password = "";

        await Login(vm);

        Assert.Equal("Format email belum benar.", vm.EmailError);
        Assert.Equal("Password wajib diisi.", vm.PasswordError);
        Assert.Equal(0, api.LoginCalls);
    }

    [Fact]
    public async Task Wrong_password_shows_the_shared_message()
    {
        var (vm, api, session) = Create();
        api.Login = (_, _) => throw FakeApi.Error(ApiErrorCodes.InvalidCredentials, 401);
        await vm.CheckServerAsync();
        vm.Email = "nadia@its.ac.id";
        vm.Password = "salah";

        await Login(vm);

        Assert.Equal("Email atau password salah.", vm.ErrorMessage);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task Successful_login_starts_the_session_and_forgets_the_password()
    {
        var (vm, api, session) = Create();
        api.Login = (email, _) => new LoginResponse("tok", DateTimeOffset.UtcNow.AddHours(12), Users.Customer with { Email = email });
        var loggedIn = false;
        vm.LoggedIn += (_, _) => loggedIn = true;
        await vm.CheckServerAsync();
        vm.Email = "  nadia@its.ac.id ";
        vm.Password = "benar";

        await Login(vm);

        Assert.True(loggedIn);
        Assert.Equal("tok", session.Token);
        Assert.Equal("nadia@its.ac.id", session.User!.Email);
        Assert.Equal(string.Empty, vm.Password);
    }

    [Fact]
    public async Task Unsupported_api_version_blocks_login()
    {
        var (vm, api, _) = Create();
        api.Meta = () => new MetaDto(2, null, DateTimeOffset.UtcNow, null);

        await vm.CheckServerAsync();

        Assert.Equal(ServerStatus.Incompatible, vm.ServerStatus);
        Assert.False(vm.LoginCommand.CanExecute(null));
        Assert.Contains("v2", vm.ErrorMessage);
    }

    [Fact]
    public async Task Unreachable_server_is_reported_as_a_connection_problem()
    {
        var (vm, api, _) = Create();
        api.Meta = () => throw ApiException.Network(new Exception());
        api.Login = (_, _) => throw ApiException.Network(new Exception());
        vm.Email = "nadia@its.ac.id";
        vm.Password = "x";

        await Login(vm);

        Assert.Equal(ServerStatus.Offline, vm.ServerStatus);
        Assert.Contains("Server tidak dapat dihubungi", vm.ErrorMessage);
    }
}

public class ApplicationsViewModelTests
{
    [Fact]
    public async Task Approving_an_application_reviewed_on_the_web_warns_and_reloads()
    {
        var pending = Data.Application();
        var api = new FakeApi
        {
            Applications = _ => Data.Page(pending),
            Approve = _ => throw FakeApi.Error(ApiErrorCodes.AlreadyReviewed, 409),
        };
        var notes = new FakeNotifications();
        var vm = new ApplicationsViewModel(api, new FakeDialogs(), notes);
        await vm.RefreshAsync();
        vm.SelectedItem = vm.Items.Single();

        await ((AsyncRelayCommand)vm.ApproveCommand).ExecuteAsync();

        Assert.Equal((ToastKind.Warning, "Pengajuan ini sudah ditinjau. Daftar dimuat ulang."), notes.Shown.Single());
        Assert.Equal(2, api.ApplicationsCalls);
    }

    [Fact]
    public async Task Rejecting_sends_the_reason_and_keeps_the_panel_on_the_updated_row()
    {
        var id = Guid.NewGuid();
        var status = ApplicationStatuses.Pending;
        string? sentReason = null;
        var api = new FakeApi
        {
            Applications = _ => Data.Page(Data.Application(status, id)),
            Reject = (_, reason) =>
            {
                sentReason = reason;
                status = ApplicationStatuses.Declined;
                return null;
            },
        };
        var vm = new ApplicationsViewModel(api, new FakeDialogs { RejectReason = "SIM kedaluwarsa" }, new FakeNotifications());
        await vm.RefreshAsync();
        vm.SelectedItem = vm.Items.Single();

        await ((AsyncRelayCommand)vm.RejectCommand).ExecuteAsync();

        Assert.Equal("SIM kedaluwarsa", sentReason);
        Assert.NotNull(vm.SelectedItem);
        Assert.Equal("Ditolak", vm.SelectedItem!.StatusLabel);
        Assert.False(vm.RejectCommand.CanExecute(null));
    }

    [Fact]
    public async Task Undecodable_license_image_shows_a_placeholder()
    {
        var api = new FakeApi { Applications = _ => Data.Page(Data.Application()) };
        var vm = new ApplicationsViewModel(api, new FakeDialogs(), new FakeNotifications());
        await vm.RefreshAsync();

        vm.SelectedItem = vm.Items.Single();
        await Task.Delay(50);

        Assert.Null(vm.LicenseImage);
        Assert.Equal("Foto SIM tidak dapat dimuat.", vm.LicenseError);
    }

    [Fact]
    public async Task Reject_dialog_requires_a_reason()
    {
        var dialog = new RejectReasonDialogViewModel("nadia") { Reason = "   " };

        dialog.SubmitCommand.Execute(null);

        Assert.Equal("Alasan penolakan wajib diisi.", dialog.ValidationMessage);
        Assert.False(dialog.Result.IsCompleted);

        dialog.Reason = "  Foto buram ";
        dialog.SubmitCommand.Execute(null);
        Assert.Equal("Foto buram", await dialog.Result);
    }
}

public class UsersViewModelTests
{
    private static SessionService LoggedInAsAdmin()
    {
        var session = new SessionService();
        session.Start(new LoginResponse("t", DateTimeOffset.UtcNow.AddHours(1), Users.Admin));
        return session;
    }

    private static UserDetailDto Detail(UserDto u) => new(u, new UserDependenciesDto(4, 0, 12), null);

    [Fact]
    public async Task Deleting_a_user_with_bookings_reports_the_counts()
    {
        var api = new FakeApi
        {
            Users = _ => Data.Page(Users.Admin, Users.Customer),
            User = id => Detail(id == Users.Admin.Id ? Users.Admin : Users.Customer),
            DeleteUser = _ => throw FakeApi.Error(ApiErrorCodes.HasDependencies, 409, """{"bookingsAsCustomer":4,"bookingsAsDriver":0,"chatMessages":12}"""),
        };
        var notes = new FakeNotifications();
        var vm = new UsersViewModel(api, LoggedInAsAdmin(), new FakeDialogs(), notes);
        await vm.RefreshAsync();
        vm.SelectedItem = vm.Items.Single(u => u.Id == Users.Customer.Id);

        await ((AsyncRelayCommand)vm.DeleteCommand).ExecuteAsync();

        Assert.Equal((ToastKind.Error, "Pengguna masih punya 4 pemesanan dan 12 chat, jadi tidak bisa dihapus."), notes.Shown.Single());
        Assert.Equal(Users.Customer.Id, vm.SelectedItem?.Id);
    }

    [Fact]
    public async Task Own_account_cannot_be_changed_from_the_ui()
    {
        var api = new FakeApi { Users = _ => Data.Page(Users.Admin), User = _ => Detail(Users.Admin) };
        var vm = new UsersViewModel(api, LoggedInAsAdmin(), new FakeDialogs(), new FakeNotifications());
        await vm.RefreshAsync();

        vm.SelectedItem = vm.Items.Single();

        Assert.True(vm.IsSelfSelected);
        Assert.False(vm.ChangeRoleCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.Equal("valdi (Anda)", vm.SelectedItem!.DisplayName);
    }

    [Fact]
    public async Task Changing_a_role_calls_the_api_and_confirms()
    {
        string? sentRole = null;
        var api = new FakeApi
        {
            Users = _ => Data.Page(Users.Customer),
            User = _ => Detail(Users.Customer),
            ChangeRole = (_, role) =>
            {
                sentRole = role;
                return Users.Customer with { Role = role };
            },
        };
        var notes = new FakeNotifications();
        var vm = new UsersViewModel(api, LoggedInAsAdmin(), new FakeDialogs { PickedRole = Roles.Driver }, notes);
        await vm.RefreshAsync();
        vm.SelectedItem = vm.Items.Single();

        await ((AsyncRelayCommand)vm.ChangeRoleCommand).ExecuteAsync();

        Assert.Equal(Roles.Driver, sentRole);
        Assert.Equal((ToastKind.Success, "Role nadia diubah menjadi Driver."), notes.Shown.Single());
    }

    [Fact]
    public void Role_dialog_only_saves_a_different_role()
    {
        var dialog = new ChangeRoleDialogViewModel("nadia", Roles.Customer);
        Assert.False(dialog.SaveCommand.CanExecute(null));

        dialog.Selected = dialog.Options.Single(o => o.Value == Roles.Admin);
        Assert.True(dialog.SaveCommand.CanExecute(null));
    }
}

public class BookingListTests
{
    [Fact]
    public async Task Filters_reset_to_page_one_and_reach_the_query()
    {
        var api = new FakeApi { MyBookings = q => new PagedResult<BookingDto>([Data.Booking()], q.Page, q.PageSize, 60) };
        var session = new SessionService();
        session.Start(new LoginResponse("t", DateTimeOffset.UtcNow.AddHours(1), Users.Driver));
        var now = DateTimeOffset.Parse("2026-10-21T05:00:00Z");
        var vm = new MyBookingsViewModel(api, session, () => now);
        await vm.RefreshAsync();
        vm.Pager.NextCommand.Execute(null);
        await Task.Delay(20);
        Assert.Equal(2, api.BookingQueries.Last().Page);

        vm.SelectedStage = vm.StageOptions.Single(o => o.Value == 4);
        vm.SelectedPeriod = vm.PeriodOptions.Single(o => o.Value == PeriodPreset.Today);
        await Task.Delay(20);

        var q = api.BookingQueries.Last();
        Assert.Equal(1, q.Page);
        Assert.Equal(4, q.Stage);
        Assert.Equal(BookingSides.Driver, q.Side);
        Assert.Equal(DateTimeOffset.Parse("2026-10-20T17:00:00Z"), q.From);
        Assert.True(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Drivers_can_switch_to_their_passenger_history()
    {
        var api = new FakeApi { MyBookings = _ => Data.Page(Data.Booking()) };
        var session = new SessionService();
        session.Start(new LoginResponse("t", DateTimeOffset.UtcNow.AddHours(1), Users.Driver));
        var vm = new MyBookingsViewModel(api, session);
        await vm.RefreshAsync();
        Assert.True(vm.Side.IsAvailable);
        Assert.True(vm.ShowCustomerColumn);

        vm.Side.Selected = vm.Side.Options.Single(o => o.Value == BookingSides.Customer);
        await Task.Delay(20);

        Assert.Equal(BookingSides.Customer, api.BookingQueries.Last().Side);
        Assert.True(vm.ShowDriverColumn);
        Assert.False(vm.ShowCustomerColumn);
    }

    [Fact]
    public async Task Refresh_keeps_the_open_booking_selected()
    {
        var id = Guid.NewGuid();
        var api = new FakeApi
        {
            AdminBookings = _ => Data.Page(Data.Booking(id: id), Data.Booking()),
            Booking = bookingId => Data.Booking(id: bookingId),
        };
        var vm = new AdminBookingsViewModel(api, new FakePlatform(), new FakeNotifications());
        await vm.RefreshAsync();
        vm.SelectedItem = vm.Items.First(i => i.Id == id);

        await vm.RefreshAsync();

        Assert.Equal(id, vm.SelectedItem?.Id);
        Assert.True(vm.IsDetailOpen);
        Assert.True(vm.ShowPartiesColumn);
    }

    [Fact]
    public async Task Load_failure_before_any_data_shows_the_full_page_error()
    {
        var api = new FakeApi { AdminBookings = _ => throw ApiException.Timeout(new TimeoutException()) };
        var vm = new AdminBookingsViewModel(api, new FakePlatform(), new FakeNotifications());

        await vm.RefreshAsync();

        Assert.True(vm.ShowFullError);
        Assert.False(vm.ShowContent);
        Assert.Equal("Server tidak merespons dalam 15 detik. Coba lagi.", vm.ErrorMessage);
    }
}

public class PagerViewModelTests
{
    [Fact]
    public void Range_text_and_navigation()
    {
        var pager = new PagerViewModel(25);
        var changes = 0;
        pager.Changed += (_, _) => changes++;
        pager.Update(1, 60);

        Assert.Equal("Menampilkan 1–25 dari 60", pager.RangeText);
        Assert.Equal(3, pager.TotalPages);
        Assert.False(pager.PreviousCommand.CanExecute(null));

        pager.LastCommand.Execute(null);
        Assert.Equal(3, pager.Page);
        Assert.Equal("Menampilkan 51–60 dari 60", pager.RangeText);
        Assert.False(pager.NextCommand.CanExecute(null));

        pager.PageSize = 100;
        Assert.Equal(1, pager.Page);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Page_size_is_capped_at_the_api_maximum()
    {
        var pager = new PagerViewModel { PageSize = 500 };
        Assert.Equal(100, pager.PageSize);
    }
}
