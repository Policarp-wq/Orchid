using Orchid.Ui.Contracts;
using Orchid.Ui.Controls;
using Orchid.Ui.Theme;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.State;

/// <summary>
/// One review position in [0, duration] drives the playhead, overview, time readout, moment panel and held marks. Playback derives the
/// position from an elapsed clock and its starting offset, so late UI ticks never accumulate drift.
/// </summary>
public sealed class ReviewState : ObservableObject
{
    public const double ZoomStep = 1.5;
    private static readonly RecordingData Nothing = new(new SessionSettings(120, 4, 4, 4, [true, false, false, false], 0), TimeSpan.Zero, [], []);

    private readonly TimeProvider _clock;
    private long _playStartedAt;
    private TimeSpan _playStartPosition;
    private RecordingData _recording = Nothing;
    private TimeSpan _position;
    private bool _isPlaying;
    private NoteSpan? _selectedNote;
    private bool _isFolded;
    private GridSettings _gridSettings = new();
    private IReadOnlyList<NoteSpan> _held = [];
    private IReadOnlyList<NoteSpan> _sustained = [];
    private IReadOnlyList<HeldChip> _heldChips = [];
    private IReadOnlyList<HeldChip> _sustainedChips = [];
    private string _positionText = Display.Clock(TimeSpan.Zero);
    private string _barText = Strings.NoValue;
    private string _beatText = Strings.NoValue;

    public ReviewState(TimeProvider clock)
    {
        _clock = clock;
        Notes = new NoteIndex(Nothing);
        Grid = new GridModel(Nothing.Settings);
        Pitches = PitchMap.For(Notes, folded: false);
        PlayPauseCommand = new Command(TogglePlay);
        RewindCommand = new Command(Rewind);
        ZoomTimeInCommand = new Command(() => ZoomTime(ZoomStep));
        ZoomTimeOutCommand = new Command(() => ZoomTime(1 / ZoomStep));
        ZoomPitchInCommand = new Command(() => ZoomPitch(ZoomStep));
        ZoomPitchOutCommand = new Command(() => ZoomPitch(1 / ZoomStep));
        FitCommand = new Command(Fit);
        FollowCommand = new Command(Follow);
        FoldCommand = new Command(() => IsFolded = !IsFolded);
    }

    /// <summary>Raised when anything drawn changed: recording, position, camera, pitch layout or grid.</summary>
    public event EventHandler? ViewChanged;

    public Command PlayPauseCommand { get; }
    public Command RewindCommand { get; }
    public Command ZoomTimeInCommand { get; }
    public Command ZoomTimeOutCommand { get; }
    public Command ZoomPitchInCommand { get; }
    public Command ZoomPitchOutCommand { get; }
    public Command FitCommand { get; }
    public Command FollowCommand { get; }
    public Command FoldCommand { get; }

    public RecordingData Recording => _recording;
    public bool HasRecording => !ReferenceEquals(_recording, Nothing);
    public NoteIndex Notes { get; private set; }
    public GridModel Grid { get; private set; }
    public PitchMap Pitches { get; private set; }
    public TimelineCamera Camera { get; } = new();

    /// <summary>The top of the lane area inside the timeline view (below the ruler); set by the view with the viewport.</summary>
    public double LaneTop { get; private set; }

    public TimelineTransform Transform => new(Camera, Pitches, LaneTop);
    public TimeSpan Duration => _recording.Duration;
    public bool IsEmpty => Notes.Notes.Count == 0;
    public bool HasPedal => Notes.Pedal.Count > 0;
    public string DurationText => Display.Format(Strings.TimeTotal, Display.Clock(Duration));
    public string NoteCountText => Display.NoteCount(Notes.Notes.Count);
    public string SummaryText => Display.Format(Strings.LastReviewSummary, NoteCountText, Display.Clock(Duration));

    public TimeSpan Position
    {
        get => _position;
        private set
        {
            if (!Set(ref _position, value)) return;
            PositionText = Display.Clock(value);
            UpdateMoment();
        }
    }

    public string PositionText { get => _positionText; private set => Set(ref _positionText, value); }
    public string BarText { get => _barText; private set => Set(ref _barText, value); }
    public string BeatText { get => _beatText; private set => Set(ref _beatText, value); }

    /// <summary>Keys held at the position, lowest first, with MIDI number, attack and duration.</summary>
    public IReadOnlyList<HeldChip> HeldChips
    {
        get => _heldChips;
        private set
        {
            if (Set(ref _heldChips, value)) Raise(nameof(HeldSummaryText));
        }
    }

    public IReadOnlyList<HeldChip> SustainedChips { get => _sustainedChips; private set => Set(ref _sustainedChips, value); }
    public IReadOnlyList<NoteSpan> Held => _held;

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (Set(ref _isPlaying, value)) Raise(nameof(PlayPauseText));
        }
    }

    public string PlayPauseText => IsPlaying ? Strings.Pause : Strings.Play;

    /// <summary>The held notes as one line for the compact moment row.</summary>
    public string HeldSummaryText => HeldChips.Count == 0 ? Strings.HeldNone : string.Join(" ", HeldChips.Select(chip => chip.Name));

    public bool CanZoomTimeIn => Camera.CanZoomTimeIn;
    public bool CanZoomTimeOut => Camera.CanZoomTimeOut;
    public bool CanZoomPitchIn => Camera.CanZoomPitchIn;
    public bool CanZoomPitchOut => Camera.CanZoomPitchOut;

    public NoteSpan? SelectedNote { get => _selectedNote; private set => Set(ref _selectedNote, value); }

    public bool IsFollowing => Camera.Mode == CameraMode.Locked;

    public bool IsFolded
    {
        get => _isFolded;
        set
        {
            if (!Set(ref _isFolded, value)) return;
            Raise(nameof(FoldHint));
            Pitches = PitchMap.For(Notes, value);
            Camera.SetLaneCount(Pitches.LaneCount);
            CameraMoved();
        }
    }

    public GridSettings GridSettings
    {
        get => _gridSettings;
        set
        {
            if (Set(ref _gridSettings, value)) Changed();
        }
    }

    /// <summary>Tempo and pulse of the recording, for the header: the grid in review comes from the session settings, not from setup.</summary>
    public string ContextText => string.Join(" · ", Display.Format(Strings.BpmValue, _recording.Settings.Bpm),
        Display.Format(Strings.PulseContext, _recording.Settings.PulseDenominator));

    public string FollowHint => IsFollowing ? Strings.FollowOn : Strings.FollowOff;
    public string FoldHint => IsFolded ? Strings.FoldOnHint : Strings.FoldDescription;

    public string ScaleText => Display.Format(Strings.TimeScale, Math.Round(Grid.Settings.Pulse.TotalSeconds * Camera.PxPerSecond));

    /// <summary>Shows a closed recording fitted, with the cursor at the start and nothing selected.</summary>
    public void Load(RecordingData recording)
    {
        _recording = recording;
        IsPlaying = false;
        SelectedNote = null;
        Notes = new NoteIndex(recording);
        Grid = new GridModel(recording.Settings);
        Pitches = PitchMap.For(Notes, IsFolded);
        Camera.Reset(recording.Duration.TotalSeconds, Pitches.LaneCount, IsEmpty);
        _position = TimeSpan.MaxValue;
        Position = TimeSpan.Zero;
        foreach (var name in new[] { nameof(Recording), nameof(HasRecording), nameof(IsEmpty), nameof(HasPedal), nameof(DurationText), nameof(NoteCountText), nameof(SummaryText), nameof(ContextText) })
            Raise(name);
        CameraMoved();
    }

    public void SetViewport(double width, double laneHeight, double laneTop)
    {
        LaneTop = laneTop;
        Camera.Resize(width, laneHeight);
        Camera.Follow(Position.TotalSeconds);
        CameraMoved();
    }

    /// <summary>Moves the position and stops playback there; the camera locks onto the cursor.</summary>
    public void SeekTo(TimeSpan position)
    {
        IsPlaying = false;
        SelectedNote = null;
        Position = Clamp(position);
        Lock();
    }

    public void Play()
    {
        if (!HasRecording || Duration <= TimeSpan.Zero) return;
        if (Position >= Duration) Position = TimeSpan.Zero;
        _playStartPosition = Position;
        _playStartedAt = _clock.GetTimestamp();
        IsPlaying = true;
        Lock();
    }

    public void Pause()
    {
        Tick();
        IsPlaying = false;
    }

    public void TogglePlay()
    {
        if (IsPlaying) Pause();
        else Play();
    }

    public void Rewind() => SeekTo(TimeSpan.Zero);

    /// <summary>Advances playback from the elapsed clock and stops at the end.</summary>
    public void Tick()
    {
        if (!IsPlaying) return;
        Position = Clamp(_playStartPosition + _clock.GetElapsedTime(_playStartedAt));
        if (Position >= Duration) IsPlaying = false;
        Camera.Follow(Position.TotalSeconds);
        Changed();
    }

    /// <summary>A click: a note seeks to its exact attack and is selected; empty space seeks to the time under the pointer.</summary>
    public void SelectAt(double x, double y)
    {
        var note = Transform.HitTest(Notes, x, y);
        SeekTo(note?.Attack ?? TimeSpan.FromSeconds(Transform.XToTime(x)));
        SelectedNote = note;
    }

    public void PanBy(double dx, double dy)
    {
        Camera.PanBy(dx, Camera.PitchFits ? 0 : dy);
        CameraMoved();
    }

    /// <summary>Overview frame drag: the camera starts at <paramref name="start"/> seconds and is Free.</summary>
    public void ScrollTo(double start)
    {
        Camera.ScrollTo(start);
        CameraMoved();
    }

    public void ZoomTimeAt(double factor, double anchorX)
    {
        if (Camera.ZoomTimeAt(factor, anchorX)) CameraMoved();
    }

    public void ZoomPitchAt(double factor, double anchorY)
    {
        if (Camera.ZoomPitchAt(factor, anchorY)) CameraMoved();
    }

    /// <summary>Button and keyboard zoom keep the cursor fixed while Locked and the middle of the viewport while Free.</summary>
    public void ZoomTime(double factor)
    {
        var cursor = Transform.TimeToX(Position.TotalSeconds);
        var anchor = IsFollowing && cursor >= 0 && cursor <= Camera.ViewportWidth ? cursor : Camera.ViewportWidth / 2;
        ZoomTimeAt(factor, anchor);
    }

    public void ZoomPitch(double factor) => ZoomPitchAt(factor, Camera.ViewportHeight / 2);

    public void Fit()
    {
        Camera.Fit();
        Camera.Mode = CameraMode.Locked;
        Camera.Follow(Position.TotalSeconds);
        CameraMoved();
    }

    public void Follow() => Lock();

    private void Lock()
    {
        Camera.Mode = CameraMode.Locked;
        Camera.Follow(Position.TotalSeconds);
        CameraMoved();
    }

    private void CameraMoved()
    {
        foreach (var name in new[] { nameof(IsFollowing), nameof(FollowHint), nameof(ScaleText), nameof(CanZoomTimeIn), nameof(CanZoomTimeOut), nameof(CanZoomPitchIn), nameof(CanZoomPitchOut) })
            Raise(name);
        Changed();
    }

    private void Changed() => ViewChanged?.Invoke(this, EventArgs.Empty);

    private TimeSpan Clamp(TimeSpan position) =>
        position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position;

    private void UpdateMoment()
    {
        var (bar, beat) = Grid.PositionAt(Position);
        BarText = bar.ToString(System.Globalization.CultureInfo.CurrentCulture);
        BeatText = Display.Format(Strings.BeatOf, beat, Grid.Settings.ClicksPerMeasure);
        var held = Notes.HeldAt(Position);
        if (!held.SequenceEqual(_held))
        {
            _held = held;
            HeldChips = [.. held.Select(note => new HeldChip(Display.NoteName(note.Midi),
                Display.Format(Strings.HeldDetails, note.Midi, Display.Seconds(note.Attack), Display.Milliseconds(note.Release - note.Attack))))];
        }
        var sustained = HasPedal ? Notes.SustainedAt(Position) : [];
        if (!sustained.SequenceEqual(_sustained))
        {
            _sustained = sustained;
            SustainedChips = [.. sustained.Select(note => new HeldChip(Display.NoteName(note.Midi), null))];
        }
    }
}
