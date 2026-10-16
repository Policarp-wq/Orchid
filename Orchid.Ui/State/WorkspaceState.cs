using Orchid.Ui.Contracts;
using Orchid.Ui.Theme;

namespace Orchid.Ui.State;

public enum WorkspaceMode { Ready, CountIn, Recording, Review }

/// <summary>
/// Everything the views show. Views and layouts hold no state of their own, so switching shells or resizing loses nothing.
/// </summary>
public sealed class WorkspaceState : ObservableObject
{
    private readonly OrchidServices _services;
    private WorkspaceMode _mode;
    private ILiveSession? _session;
    private SessionSettings? _liveSettings;
    private bool _stopping;

    public WorkspaceState(OrchidServices services)
    {
        _services = services;
        Review = new ReviewState(services.Clock);
        Review.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ReviewState.IsPlaying)) Raise(nameof(IsTicking));
            if (args.PropertyName != nameof(ReviewState.HasRecording)) return;
            Raise(nameof(HasLastReview));
            Raise(nameof(HasNoLastReview));
        };
        StartCommand = new Command(async () => await StartAsync(), () => Setup.CanStart);
        Setup.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SetupState.CanStart)) StartCommand.ChangeCanExecute();
        };
        StopCommand = new Command(async () => await StopAsync());
        RefreshCommand = new Command(async () => await Setup.RefreshAsync(services.Inputs, CancellationToken.None));
        NewSessionCommand = new Command(NewSession);
        ShowReviewCommand = new Command(ShowReview);
        RecoverCommand = new Command(async () => await RecoverAsync());
        DiscardRecoveryCommand = new Command(() => Status.Recovery = null);
        BannerActionCommand = new Command(async () => await BannerActionAsync());
    }

    public SetupState Setup { get; } = new();
    public LiveState Live { get; } = new();
    public ReviewState Review { get; }
    public StatusState Status { get; } = new();

    public Command StartCommand { get; }
    public Command StopCommand { get; }
    public Command RefreshCommand { get; }
    public Command NewSessionCommand { get; }
    public Command ShowReviewCommand { get; }
    public Command RecoverCommand { get; }
    public Command DiscardRecoveryCommand { get; }
    public Command BannerActionCommand { get; }

    public WorkspaceMode Mode
    {
        get => _mode;
        private set
        {
            if (!Set(ref _mode, value)) return;
            foreach (var name in new[] { nameof(IsTicking), nameof(IsReady), nameof(IsLive), nameof(IsReview), nameof(ModeText) }) Raise(name);
        }
    }

    public bool IsReady => Mode == WorkspaceMode.Ready;
    public bool IsLive => Mode is WorkspaceMode.CountIn or WorkspaceMode.Recording;
    public bool IsReview => Mode == WorkspaceMode.Review;
    public bool HasLastReview => Review.HasRecording;
    public bool HasNoLastReview => !Review.HasRecording;

    public string ModeText => Mode switch
    {
        WorkspaceMode.CountIn => Strings.ModeCountIn,
        WorkspaceMode.Recording => Strings.ModeRecording,
        WorkspaceMode.Review => Strings.ModeReview,
        _ => Strings.ModeReady,
    };

    /// <summary>The host runs its frame timer only while this is true, so Ready and a paused review cost nothing.</summary>
    public bool IsTicking => IsLive || Review.IsPlaying;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await Setup.RefreshAsync(_services.Inputs, cancellationToken);
        Status.Recovery = await _services.Store.FindUnfinishedAsync(cancellationToken);
    }

    public async Task StartAsync()
    {
        if (!Setup.CanStart || Setup.SelectedInput is not { } input) return;
        var settings = Setup.ToSettings();
        Review.Pause();
        Status.ClearBanner();
        Setup.IsLocked = true;
        try
        {
            _session = await _services.Recorder.StartAsync(input, settings, CancellationToken.None);
        }
        catch (SessionStartException failure)
        {
            Setup.IsLocked = false;
            Status.ShowProblem(failure.Code);
            return;
        }
        _liveSettings = settings;
        Live.Begin(settings);
        Mode = settings.CountInBars > 0 ? WorkspaceMode.CountIn : WorkspaceMode.Recording;
        Tick();
    }

    /// <summary>One UI frame: reads the live snapshot or advances playback.</summary>
    public void Tick()
    {
        if (_session is null || _liveSettings is null)
        {
            Review.Tick();
            return;
        }
        var snapshot = _session.Read();
        Live.Apply(snapshot, _liveSettings);
        if (snapshot.Problem is { } problem && Status.BannerProblem != problem.Code) Status.ShowProblem(problem.Code);
        if (snapshot.Phase == LivePhase.Recording) Mode = WorkspaceMode.Recording;
        // The backend ended the session (a lost device): keep what was captured.
        if (snapshot.Phase == LivePhase.Ended) _ = StopAsync();
    }

    /// <summary>Stop during the count-in returns to Ready without a session; otherwise the closed recording opens in review.</summary>
    public async Task StopAsync()
    {
        if (_session is not { } session || _stopping) return;
        _stopping = true;
        var countingIn = Mode == WorkspaceMode.CountIn;
        try
        {
            var recording = await session.StopAsync();
            await session.DisposeAsync();
            _session = null;
            _liveSettings = null;
            Setup.IsLocked = false;
            if (countingIn)
            {
                Mode = WorkspaceMode.Ready;
                return;
            }
            Review.Load(recording);
            Status.FileName = Strings.UnsavedName;
            Mode = WorkspaceMode.Review;
            await SaveAsync();
        }
        finally
        {
            _stopping = false;
        }
    }

    /// <summary>A failed save keeps the recording in review and offers Retry.</summary>
    public async Task SaveAsync()
    {
        if (!Review.HasRecording) return;
        Status.SaveState = SaveState.Saving;
        switch (await _services.Store.SaveAsync(Review.Recording, CancellationToken.None))
        {
            case SaveResult.Saved saved:
                Status.FileName = Path.GetFileName(saved.Path);
                Status.SaveState = SaveState.Saved;
                if (Status.BannerProblem == ProblemCode.SaveFailed) Status.ClearBanner();
                break;
            case SaveResult.Failed failed:
                Status.SaveState = SaveState.Failed;
                Status.ShowProblem(failed.Code);
                break;
        }
    }

    public async Task OpenAsync(string path)
    {
        if (IsLive) return;
        var result = await _services.Store.LoadAsync(path, CancellationToken.None);
        if (result is not LoadResult.Loaded loaded)
        {
            Status.ShowLoadProblem(result);
            return;
        }
        Status.ClearBanner();
        Review.Load(loaded.Data);
        Status.FileName = loaded.Name;
        Status.SaveState = SaveState.Saved;
        Mode = WorkspaceMode.Review;
    }

    public async Task RecoverAsync()
    {
        if (Status.Recovery is not { } recovery || IsLive) return;
        Status.Recovery = null;
        Review.Load(recovery);
        Status.FileName = Strings.UnsavedName;
        Mode = WorkspaceMode.Review;
        await SaveAsync();
    }

    public void NewSession()
    {
        if (IsLive) return;
        Review.Pause();
        Mode = WorkspaceMode.Ready;
    }

    public void ShowReview()
    {
        if (Review.HasRecording && !IsLive) Mode = WorkspaceMode.Review;
    }

    private async Task BannerActionAsync()
    {
        if (Status.SaveState == SaveState.Failed) await SaveAsync();
        else Status.ClearBanner();
    }
}
