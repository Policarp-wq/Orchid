using Orchid.Platform.Windows;

namespace Orchid.UiPreview;

public partial class WorkspacePage : ContentPage
{
    // Matches TimingStorage's current buffer interval.
    private const int BufferSizeMs = 20;
    private const double WheelStepPixels = 80, DragThresholdPixels = 4;
    private readonly PreviewSession _session = new(TimeProvider.System);
    private readonly TimelineDrawing _timeline;
    private readonly IDispatcherTimer _timer;
    private bool _updatingPosition;
    private bool _ready;
    private WindowsRecordingSession? _recording;
    private CancellationTokenSource? _stopRecording;
    private (int Index, string Name)[] _devices = [];
    private bool _followingPlayhead;
    // Set when the user pans away from the playhead; panning back to it, seeking, Play and Rewind resume following.
    private bool _followPaused;
    private double? _dragStartX;
    private double _dragStartScroll;
    private bool _dragged;
    private readonly Microsoft.UI.Xaml.Input.PointerEventHandler _wheelHandler, _pressHandler, _moveHandler, _releaseHandler;
    private Microsoft.UI.Xaml.UIElement? _inputView;
    private bool[] _strongBeats = PreviewSession.DefaultStrongBeats(4);

    public WorkspacePage()
    {
        InitializeComponent();
        (_wheelHandler, _pressHandler, _moveHandler, _releaseHandler) = (OnTimelineWheel, OnTimelinePressed, OnTimelineMoved, OnTimelineReleased);
        TimelineViewport.HandlerChanged += (_, _) => AttachTimelineInput();
        _session.LoadRecording([], 0);
        _timeline = new TimelineDrawing(_session);
        Timeline.Drawable = _timeline;
        PitchLabels.Drawable = new PitchLabelsDrawing(_timeline);
        Keyboard.Drawable = new KeyboardDrawing(_session);
        TimelineScroll.SizeChanged += (_, _) => UpdateTimelineLayout();
        Pulse.ItemsSource = new[] { "1/1", "1/2", "1/4", "1/8", "1/16" };
        Pulse.SelectedIndex = 2;
        ResetAccents();
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
            // Backend gap: Open does not take the strong-beat pattern yet, so clicks still accent only beat 1 (IMPLEMENTATION-DOD D2).
            recording = WindowsRecordingSession.Open(_devices[Device.SelectedIndex].Index, bpm, pulseDenominator);
            _recording = recording;
            recording.NoteReceived += (midi, interaction, milliseconds) => Dispatcher.Dispatch(() =>
            {
                if (_recording != recording) return;
                _session.ReceiveLiveNote(midi, interaction, milliseconds / 1000.0);
            });
            _session.BeginRecording();
            _followPaused = false;
            _session.Bpm = bpm;
            _session.PulseDenominator = pulseDenominator;
            _session.StrongBeats = _strongBeats.ToArray();
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
        EditAccentsButton.IsEnabled = !recording;
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
        if (force) _followPaused = false;
        else if (_followPaused) return;
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
            _followPaused = false;
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
        _followPaused = false;
        _timer.Stop();
        _session.Seek(0);
        UpdatePosition();
        await TimelineScroll.ScrollToAsync(0, 0, false);
    }
    private void SelectNote(object? sender, TappedEventArgs args)
    {
        if (_dragged)
        {
            _dragged = false; // the release of a pan is not a click
            return;
        }
        _followPaused = false;
        if (_recording is not null || args.GetPosition(Timeline) is not { } point ||
            _timeline.HitTest(new PointF((float)point.X, (float)point.Y)) is not { } note) return;
        _timer.Stop();
        _session.Seek(note.Attack);
        UpdatePosition();
    }

    // Moves the view by delta pixels from the given offset. The platform ScrollViewers swallow a vertical wheel
    // without moving, so the wheel and the mouse drag are translated here.
    private void PanTimeline(double from, double delta)
    {
        var target = TimelineDrawing.PanOffset((float)from, delta, (float)(TimelineExtent.WidthRequest - TimelineScroll.Width));
        var playhead = _session.Position * TimelineDrawing.PixelsPerSecond;
        _followPaused = playhead < target || playhead >= target + TimelineScroll.Width;
        _ = TimelineScroll.ScrollToAsync(target, 0, false);
    }

    // MAUI's pointer gestures do not deliver PointerMoved here, so the timeline listens to the WinUI events directly.
    // handledEventsToo: the inner ScrollViewers mark wheel and pointer events as handled even when they cannot use them.
    private void AttachTimelineInput()
    {
        foreach (var (route, handler) in InputRoutes())
            _inputView?.RemoveHandler(route, handler);
        _inputView = TimelineViewport.Handler?.PlatformView as Microsoft.UI.Xaml.UIElement;
        foreach (var (route, handler) in InputRoutes())
            _inputView?.AddHandler(route, handler, true);
    }

    private IEnumerable<(Microsoft.UI.Xaml.RoutedEvent, Microsoft.UI.Xaml.Input.PointerEventHandler)> InputRoutes() =>
    [
        (Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent, _wheelHandler),
        (Microsoft.UI.Xaml.UIElement.PointerPressedEvent, _pressHandler),
        (Microsoft.UI.Xaml.UIElement.PointerMovedEvent, _moveHandler),
        (Microsoft.UI.Xaml.UIElement.PointerReleasedEvent, _releaseHandler),
        (Microsoft.UI.Xaml.UIElement.PointerCanceledEvent, _releaseHandler),
    ];

    private void OnTimelineWheel(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        var properties = args.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender).Properties;
        if (properties.IsHorizontalMouseWheel) return; // the horizontal ScrollView already pans on its own
        args.Handled = true;
        PanTimeline(TimelineScroll.ScrollX, -properties.MouseWheelDelta / 120.0 * WheelStepPixels);
    }

    private void OnTimelinePressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
        // The scroll bar drags itself; only presses on the timeline content start a pan.
        if (!point.Properties.IsLeftButtonPressed || IsInsideScrollBar(args.OriginalSource)) return;
        _dragStartX = point.Position.X;
        _dragStartScroll = TimelineScroll.ScrollX;
        _dragged = false;
    }

    private void OnTimelineMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (_dragStartX is not { } start) return;
        var point = args.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _dragStartX = null; // the button was released where we could not see it
            return;
        }
        var delta = start - point.Position.X;
        if (!_dragged && Math.Abs(delta) < DragThresholdPixels) return;
        _dragged = true;
        PanTimeline(_dragStartScroll, delta);
    }

    private void OnTimelineReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) => _dragStartX = null;

    private static bool IsInsideScrollBar(object? source)
    {
        for (var node = source as Microsoft.UI.Xaml.DependencyObject; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
            if (node is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar) return true;
        return false;
    }

    private void ChangeSettings(object? sender, EventArgs args)
    {
        if (!_ready) return;
        Tempo.Text = (int.TryParse(Tempo.Text, out var bpm) ? Math.Clamp(bpm, 30, 240) : 120).ToString();
        UpdatePosition();
    }

    private void ChangePulse(object? sender, EventArgs args)
    {
        if (!_ready) return;
        ResetAccents();
        ChangeSettings(sender, args);
    }

    // A new pulse changes the number of beats in the measure, so the pattern restarts with only the first beat strong.
    private void ResetAccents()
    {
        _strongBeats = PreviewSession.DefaultStrongBeats(1 << Pulse.SelectedIndex);
        ShowAccents();
    }

    private void ShowAccents()
    {
        AccentBars.Beats = _strongBeats;
        AccentSummary.Text = PreviewSession.DescribeStrongBeats(_strongBeats);
    }

    private void OpenAccents(object? sender, EventArgs args) => AccentsDialog.Open();

    private void AccentsChanged(object? sender, BeatsChangedEventArgs args)
    {
        _strongBeats = [.. args.Beats];
        AccentSummary.Text = PreviewSession.DescribeStrongBeats(_strongBeats);
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

    private static ImageSource Icon(string key) => ThemeResources.Get<ImageSource>(key);

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
