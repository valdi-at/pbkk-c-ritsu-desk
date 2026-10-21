using System.Windows.Input;
using RITSU_Desk.Api;
using RITSU_Desk.Infrastructure;
using RITSU_Desk.Services;

namespace RITSU_Desk.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    private CancellationTokenSource? _loadCts;
    private bool _isBusy;
    private string? _errorMessage;
    private string _errorGlyph = Glyphs.Error;
    private DateTimeOffset? _lastLoaded;
    private bool _hasLoaded;

    protected PageViewModel(string title, string subtitle)
    {
        Title = title;
        Subtitle = subtitle;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public string Title { get; }

    public string Subtitle { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value)) OnStateChanged();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                RaiseErrorLayout();
                OnStateChanged();
            }
        }
    }

    public bool HasError => ErrorMessage is not null;

    public bool ShowFullError => HasError && !HasLoaded;

    public bool ShowErrorBanner => HasError && HasLoaded;

    public bool ShowContent => !ShowFullError;

    public string ErrorGlyph
    {
        get => _errorGlyph;
        private set => SetProperty(ref _errorGlyph, value);
    }

    public DateTimeOffset? LastLoaded
    {
        get => _lastLoaded;
        private set
        {
            if (SetProperty(ref _lastLoaded, value)) OnPropertyChanged(nameof(LastLoadedText));
        }
    }

    public string LastLoadedText => LastLoaded is { } at ? $"Terakhir dimuat {Format.Time(at)}" : "Belum dimuat";

    public bool HasLoaded => _hasLoaded;

    public ICommand RefreshCommand { get; }

    public Task ActivateAsync() => _hasLoaded || IsBusy ? Task.CompletedTask : RefreshAsync();

    public async Task RefreshAsync()
    {
        _loadCts?.Cancel();
        var cts = _loadCts = new CancellationTokenSource();

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await LoadAsync(cts.Token);
            if (cts.IsCancellationRequested) return;
            if (!_hasLoaded)
            {
                _hasLoaded = true;
                OnPropertyChanged(nameof(HasLoaded));
                RaiseErrorLayout();
            }
            LastLoaded = DateTimeOffset.Now;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (ApiException ex) when (ex.Code == ApiErrorCodes.Unauthorized)
        {
        }
        catch (Exception ex)
        {
            if (ex is not ApiException) AppLog.Error($"Loading {GetType().Name} failed", ex);
            ErrorGlyph = ex is ApiException { IsConnectionProblem: true } ? Glyphs.Globe
                : ex is ApiException { Code: ApiErrorCodes.Forbidden } ? Glyphs.Lock
                : Glyphs.Error;
            ErrorMessage = ErrorMessages.For(ex);
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts)) IsBusy = false;
        }
    }

    public void Cancel() => _loadCts?.Cancel();

    protected abstract Task LoadAsync(CancellationToken ct);

    private void RaiseErrorLayout()
    {
        OnPropertyChanged(nameof(ShowFullError));
        OnPropertyChanged(nameof(ShowErrorBanner));
        OnPropertyChanged(nameof(ShowContent));
    }

    protected virtual void OnStateChanged()
    {
    }
}
