using Orchid.Platform.Windows;

namespace Orchid.UiPreview;

public partial class WorkspacePage : ContentPage
{
    // Matches TimingStorage's current buffer interval.
    private const int BufferSizeMs = 20;
    private readonly PreviewSession _session = new(TimeProvider.System);
    private readonly TimelineDrawing _timeline;
    private readonly IDispatcherTimer _timer;
    private bool _updatingPosition;
    private bool _ready;
    private WindowsRecordingSession? _recording;
    private CancellationTokenSource? _stopRecording;
    private (int Index, string Name)[] _devices = [];
    private bool _followingPlayhead;

    public WorkspacePage()
    {
        InitializeComponent();
        _session.LoadRecording([], 0);
        _timeline = new TimelineDrawing(_session);
        Timeline.Drawable = _timeline;
        PitchLabels.Drawable = new PitchLabelsDrawing(_timeline);
        Keyboard.Drawable = new KeyboardDrawing(_session);
        TimelineScroll.SizeChanged += (_, _) => UpdateTimelineLayout();
        Pulse.ItemsSource = new[] { "1/1", "1/2", "1/4", "1/8", "1/16" };
        Pulse.SelectedIndex = 2;
        ReviewGrid.ItemsSource = new[] { "1/4", "1/8", "1/16" };
        ReviewGrid.SelectedIndex = 1;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(BufferSizeMs);
        _timer.Tick += async (_, _) =>
        {
            if (_recording is { } recording) _session.SetRecordingPosition(recording.ElapsedMilliseconds / 1000.0);
            else _session.Tick();
            UpdatePosition();
            await FollowPlayhead();
            if (!_session.IsPlaying && _recording is null) _timer.Stop();
        };
        SizeChanged += (_, _) => Reflow();
        // Drawings read theme tokens, so they must repaint when the OS or the Light / Dark button changes the theme.
        Microsoft.Maui.Controls.Application.Current!.RequestedThemeChanged += RepaintDrawings;
        Unloaded += (_, _) =>
        {
            Microsoft.Maui.Controls.Application.Current!.RequestedThemeChanged -= RepaintDrawings;
            _stopRecording?.Cancel(); _timer.Stop(); _session.Pause();
        };
        _ready = true;
        RefreshDevices(this, EventArgs.Empty);
        SetRecordingControls(false);
        UpdatePosition();
    }

    private void RefreshDevices(object? sender, EventArgs args)
    {
        try
        {
            _devices = WindowsRecordingSession.GetDevices();
            Device.ItemsSource = _devices.Select(device => device.Name).ToArray();
            Device.SelectedIndex = _devices.Length == 0 ? -1 : 0;
            SessionStatus.Text = _devices.Length == 0 ? "No MIDI inputs found. Connect a device and refresh." : "Ready · 4/4";
        }
        catch (Exception exception)
        {
            _devices = [];
            Device.ItemsSource = Array.Empty<string>();
            SessionStatus.Text = $"MIDI input error: {exception.Message}";
        }
        SelectDevice(sender, args);
    }

    private void SelectDevice(object? sender, EventArgs args)
    {
        if (_ready && _stopRecording is null)
            SessionButton.IsEnabled = Device.SelectedIndex >= 0 && Device.SelectedIndex < _devices.Length;
    }

    private async void ToggleRecording(object? sender, EventArgs args)
    {
        if (_stopRecording is { } activeStop)
        {
            SessionButton.IsEnabled = false;
            SessionStatus.Text = "Stopping…";
            activeStop.Cancel();
            return;
        }
        if (Device.SelectedIndex < 0 || Device.SelectedIndex >= _devices.Length) return;
        PausePlayback(sender, args);
        ChangeSettings(sender, args);
        using var stop = new CancellationTokenSource();
        _stopRecording = stop;
        SessionButton.IsEnabled = false;
        SetRecordingControls(true);
        WindowsRecordingSession? recording = null;
        Exception? failure = null;
        try
        {
            var bpm = int.Parse(Tempo.Text);
            var pulseDenominator = 1 << Pulse.SelectedIndex;
            recording = WindowsRecordingSession.Open(_devices[Device.SelectedIndex].Index, bpm, pulseDenominator);
            _recording = recording;
            recording.NoteReceived += (midi, interaction, milliseconds) => Dispatcher.Dispatch(() =>
            {
                if (_recording != recording) return;
                _session.ReceiveLiveNote(midi, interaction, milliseconds / 1000.0);
            });
            _session.BeginRecording();
            _session.Bpm = bpm;
            _session.PulseDenominator = pulseDenominator;
            SessionButton.Text = "Stop";
            SessionButton.ImageSource = Icon("IconStop");
            SessionButton.IsEnabled = true;
            SessionStatus.Text = "Recording · 4/4";
            _timer.Start();
            UpdatePosition();
            // MAUI may leave a no-op scroll pending; capture must not wait for it.
            _ = TimelineScroll.ScrollToAsync(0, 0, false);
            await Task.Run(() => recording.RunAsync(stop.Token));
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception exception) { failure = exception; }
        finally
        {
            _timer.Stop();
            try
            {
                if (recording is not null)
                    _session.LoadRecording(recording.GetTimings(), recording.ElapsedMilliseconds / 1000.0);
            }
            catch (Exception exception) { failure ??= exception; }
            try { recording?.Dispose(); }
            catch (Exception exception) { failure ??= exception; }
            _recording = null;
            _stopRecording = null;
            SetRecordingControls(false);
            SessionButton.Text = "Start session";
            SessionButton.ImageSource = Icon("IconRecord");
            SelectDevice(sender, args);
            UpdatePosition();
            SessionStatus.Text = failure is null ? "Stopped · recording kept in memory" : $"Recording error: {failure.Message}";
        }
        if (failure is not null && IsLoaded)
            await DisplayAlertAsync("Recording failed", failure.Message, "OK");
    }

    private void SetRecordingControls(bool recording)
    {
        Device.IsEnabled = !recording;
        RefreshButton.IsEnabled = !recording;
        Tempo.IsEnabled = !recording;
        Pulse.IsEnabled = !recording;
        Timeline.IsEnabled = !recording;
        PlayButton.IsEnabled = !recording && _session.Duration > 0;
        RewindButton.IsEnabled = !recording && _session.Duration > 0;
        PositionSlider.IsEnabled = !recording && _session.Duration > 0;
    }

    private void UpdatePosition()
    {
        _updatingPosition = true;
        PositionSlider.Maximum = Math.Max(.001, _session.Duration);
        PositionSlider.Value = _session.Position;
        _updatingPosition = false;
        PositionLabel.Text = $"{_session.Position:F3} s";
        TimeLabel.Text = $"{_session.Position:F2} / {_session.Duration:F2} s";
        var playLabel = _session.IsPlaying ? "Pause" : "Play";
        PlayButton.ImageSource = Icon(_session.IsPlaying ? "IconPause" : "IconPlay");
        SemanticProperties.SetDescription(PlayButton, playLabel);
        ToolTipProperties.SetText(PlayButton, playLabel);
        SessionSummary.Text = _recording is not null
            ? $"Recording · {_session.Position:F1} s"
            : $"{_session.Notes.Count} notes · {_session.Duration:F1} s · {_session.Bpm} BPM · 1/{_session.PulseDenominator}";
        var held = _session.HeldNotes.ToArray();
        var current = held.OrderBy(note => note.Attack).LastOrDefault();
        NoteLabel.Text = current?.Name ?? "No key held";
        LiveNoteLabel.Text = _recording is not null ? current?.Name ?? "Play a note" : "Ready to play";
        MidiLabel.Text = current is null ? "" : $"MIDI {current.Midi}";
        AttackLabel.Text = current is null ? "–" : $"{current.Attack:F3} s";
        HeldLabel.Text = current is null ? "–"
            : $"{(_session.IsRecording ? Math.Max(0, _session.Position - current.Attack) : current.Duration) * 1000:F0} ms";
        var beat = (int)Math.Floor(_session.Position / _session.QuarterSeconds);
        MeasureLabel.Text = $"{beat / 4 + 1}";
        BeatLabel.Text = $"{beat % 4 + 1} of 4";
        FooterContext.Text = $"{_session.Bpm} BPM · 1/{_session.PulseDenominator} pulse";
        UpdateTimelineLayout();
        Timeline.Invalidate();
        PitchLabels.Invalidate();
        Keyboard.Invalidate();
    }

    private void UpdateTimelineLayout()
    {
        TimelineExtent.WidthRequest = Math.Max(TimelineScroll.Width, _timeline.ContentWidth);
        TimelineExtent.HeightRequest = _timeline.ContentHeight;
        TimelineViewport.HeightRequest = _timeline.ContentHeight;
        Timeline.Invalidate();
        PitchLabels.Invalidate();
    }

    private void ScrollTimeline(object? sender, ScrolledEventArgs args)
    {
        _timeline.ViewportOffset = (float)args.ScrollX;
        Timeline.Invalidate();
    }

    private async Task FollowPlayhead(bool force = false)
    {
        if (_followingPlayhead || !IsLoaded || TimelineScroll.Width <= 0) return;
        var x = _session.Position * TimelineDrawing.PixelsPerSecond;
        if (!force && x >= TimelineScroll.ScrollX && x < TimelineScroll.ScrollX + TimelineScroll.Width * .85) return;
        _followingPlayhead = true;
        try { await TimelineScroll.ScrollToAsync(Math.Max(0, x - TimelineScroll.Width * .8), 0, false); }
        finally { _followingPlayhead = false; }
    }

    private void TogglePlayback(object? sender, EventArgs args)
    {
        if (_session.IsPlaying) PausePlayback(sender, args);
        else
        {
            _session.Play();
            _timer.Start();
            UpdatePosition();
        }
    }

    private void PausePlayback(object? sender, EventArgs args)
    {
        _session.Pause();
        _timer.Stop();
        UpdatePosition();
    }

    private async void Seek(object? sender, ValueChangedEventArgs args)
    {
        if (!_ready || _updatingPosition) return;
        _timer.Stop();
        _session.Seek(args.NewValue);
        UpdatePosition();
        await FollowPlayhead(force: true);
    }

    private async void Rewind(object? sender, EventArgs args)
    {
        _timer.Stop();
        _session.Seek(0);
        UpdatePosition();
        await TimelineScroll.ScrollToAsync(0, 0, false);
    }
    private void SelectNote(object? sender, TappedEventArgs args)
    {
        if (_recording is not null || args.GetPosition(Timeline) is not { } point ||
            _timeline.HitTest(new PointF((float)point.X, (float)point.Y)) is not { } note) return;
        _timer.Stop();
        _session.Seek(note.Attack);
        UpdatePosition();
    }

    private void ChangeSettings(object? sender, EventArgs args)
    {
        if (!_ready) return;
        Tempo.Text = (int.TryParse(Tempo.Text, out var bpm) ? Math.Clamp(bpm, 30, 240) : 120).ToString();
        UpdatePosition();
    }

    private void ChangeGrid(object? sender, EventArgs args)
    {
        if (!_ready) return;
        _timeline.GridDenominator = 4 << ReviewGrid.SelectedIndex;
        Timeline.Invalidate();
    }

    private void ChangeTheme(object? sender, EventArgs args)
    {
        if (Microsoft.Maui.Controls.Application.Current is not { } app) return;
        app.UserAppTheme = app.RequestedTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
    }

    private static ImageSource Icon(string key) =>
        Microsoft.Maui.Controls.Application.Current!.Resources.TryGetValue(key, out var icon) && icon is ImageSource source
            ? source
            : throw new InvalidOperationException($"Icon '{key}' is not defined in Theme.xaml.");

    private void RepaintDrawings(object? sender, AppThemeChangedEventArgs args)
    {
        Timeline.Invalidate();
        PitchLabels.Invalidate();
        Keyboard.Invalidate();
    }

    private void Reflow()
    {
        var narrow = Width < 900;
        Review.ColumnDefinitions[1].Width = narrow ? 0 : 230;
        Review.ColumnSpacing = narrow ? 0 : 18;
        Grid.SetColumn(MomentPanel, narrow ? 0 : 1);
        Grid.SetRow(MomentPanel, narrow ? 1 : 0);
        var compact = Width < 650;
        Subtitle.IsVisible = !compact;
        Setup.ColumnDefinitions[1].Width = compact ? new GridLength(1, GridUnitType.Star) : 110;
        Setup.ColumnDefinitions[2].Width = compact ? 0 : 110;
        Grid.SetColumnSpan(Device, compact ? 2 : 1);
        Grid.SetColumn(TempoField, compact ? 0 : 1);
        Grid.SetColumn(Pulse, compact ? 1 : 2);
        Grid.SetRow(TempoField, compact ? 1 : 0);
        Grid.SetRow(Pulse, compact ? 1 : 0);
    }
}
