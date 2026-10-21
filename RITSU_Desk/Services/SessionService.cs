using RITSU_Desk.Models;

namespace RITSU_Desk.Services;

public interface ISessionService
{
    string? Token { get; }

    UserDto? User { get; }

    DateTimeOffset? ExpiresAt { get; }

    bool IsAuthenticated { get; }

    bool IsExpired { get; }

    event EventHandler? Expired;

    event EventHandler? UserChanged;

    void Start(LoginResponse login);

    void UpdateUser(UserDto user);

    void Clear();

    void NotifyExpired();
}

public sealed class SessionService : ISessionService
{
    private readonly Func<DateTimeOffset> _now;

    public SessionService(Func<DateTimeOffset>? now = null) => _now = now ?? (() => DateTimeOffset.UtcNow);

    public string? Token { get; private set; }

    public UserDto? User { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsAuthenticated => Token is not null;

    public bool IsExpired => ExpiresAt is { } at && _now() >= at;

    public event EventHandler? Expired;

    public event EventHandler? UserChanged;

    public void Start(LoginResponse login)
    {
        Token = login.Token;
        ExpiresAt = login.ExpiresAt;
        User = login.User;
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateUser(UserDto user)
    {
        if (User == user) return;
        User = user;
        UserChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Token = null;
        ExpiresAt = null;
        User = null;
    }

    public void NotifyExpired()
    {
        if (!IsAuthenticated) return;
        Clear();
        Expired?.Invoke(this, EventArgs.Empty);
    }
}
