using System.ComponentModel;
using Orchid.Ui.State;
using Orchid.Ui.Theme;
using Orchid.Ui.Timeline.Drawing;

namespace Orchid.Ui.Timeline;

/// <summary>
/// The review surface: pitch axis | timeline over an overview strip, bound to a <see cref="ReviewState"/>. The three drawing views always have
/// the size of the viewport, never the size of the recording, so no drawing surface can outgrow the platform limit (Win2D: 16384 px).
/// Input is shared (tap, pan, pinch) unless the platform partial took it over with wheel, precise pointer and keyboard handling; both
/// end in the same ReviewState methods.
/// </summary>
public partial class TimelineView : ContentView
{
    private readonly GraphicsView _axis = new(), _timeline = new(), _overview = new();
    private ReviewState? _review;
    private TimelineDrawable? _timelineDrawable;
    private bool _inputAttached;
    private double _lastPanX, _lastPanY, _lastScale = 1;

    public TimelineView()
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)],
            RowSpacing = 8,
        };
        _axis.SetDynamicResource(WidthRequestProperty, "PitchAxisWidth");
        _overview.SetDynamicResource(HeightRequestProperty, "OverviewHeight");
        SemanticProperties.SetDescription(_axis, Strings.PitchAxisDescription);
        SemanticProperties.SetDescription(_timeline, Strings.TimelineDescription);
        SemanticProperties.SetDescription(_overview, Strings.OverviewDescription);
        grid.Add(_axis);
        grid.Add(_timeline, 1);
        grid.Add(_overview, 1, 1);
        Content = grid;

        _timeline.SizeChanged += (_, _) => UpdateViewport();
        Loaded += (_, _) =>
        {
            if (_inputAttached) return;
            _inputAttached = true;
            var platformInput = false;
            AttachPlatformInput(ref platformInput);
            if (!platformInput) AttachSharedInput();
        };
    }

    /// <summary>Wheel, precise pointer drag and keyboard handling of the platform; sets <paramref name="taken"/> when it took the input over.</summary>
    partial void AttachPlatformInput(ref bool taken);

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_review is not null)
        {
            _review.ViewChanged -= OnViewChanged;
            _review.PropertyChanged -= OnReviewChanged;
        }
        _review = BindingContext as ReviewState;
        if (_review is null) return;
        _timelineDrawable = new TimelineDrawable(_review);
        _timeline.Drawable = _timelineDrawable;
        _axis.Drawable = new PitchAxisDrawable(_review);
        _overview.Drawable = new OverviewDrawable(_review);
        _review.ViewChanged += OnViewChanged;
        _review.PropertyChanged += OnReviewChanged;
        UpdateViewport();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Microsoft.Maui.Controls.Application.Current is not { } app) return;
        app.RequestedThemeChanged -= OnThemeChanged;
        if (Handler is not null) app.RequestedThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs args) => Invalidate();

    private void OnViewChanged(object? sender, EventArgs args) => Invalidate();

    private void OnReviewChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ReviewState.HasPedal)) UpdateViewport();
    }

    private void Invalidate()
    {
        _axis.Invalidate();
        _timeline.Invalidate();
        _overview.Invalidate();
    }

    // The lane area is what is left under the ruler and above the pedal strip.
    private void UpdateViewport()
    {
        if (_review is null || _timeline.Width <= 0 || _timeline.Height <= 0) return;
        var ruler = ThemeResources.Get<double>("RulerHeight");
        var pedal = _review.HasPedal ? ThemeResources.Get<double>("PedalStripHeight") : 0;
        if (_timelineDrawable is not null) _timelineDrawable.PedalStripHeight = (float)pedal;
        _review.SetViewport(_timeline.Width, Math.Max(0, _timeline.Height - ruler - pedal), ruler);
    }

    private void AttachSharedInput()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, args) =>
        {
            if (_review is not null && args.GetPosition(_timeline) is { } point) _review.SelectAt(point.X, point.Y);
        };
        var pan = new PanGestureRecognizer { TouchPoints = 1 };
        pan.PanUpdated += OnPan;
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += OnPinch;
        _timeline.GestureRecognizers.Add(tap);
        _timeline.GestureRecognizers.Add(pan);
        _timeline.GestureRecognizers.Add(pinch);

        var seek = new TapGestureRecognizer();
        seek.Tapped += (_, args) =>
        {
            if (_review is not null && args.GetPosition(_overview) is { } point) SeekOverview(point.X);
        };
        var dragFrame = new PanGestureRecognizer { TouchPoints = 1 };
        dragFrame.PanUpdated += OnOverviewPan;
        _overview.GestureRecognizers.Add(seek);
        _overview.GestureRecognizers.Add(dragFrame);
    }

    private void SeekOverview(double x)
    {
        if (_review is null || _overview.Width <= 0) return;
        _review.SeekTo(TimeSpan.FromSeconds(Math.Clamp(x / _overview.Width, 0, 1) * _review.Duration.TotalSeconds));
    }

    // A drag moves the content with the pointer: the view moves the other way.
    private void OnPan(object? sender, PanUpdatedEventArgs args)
    {
        if (_review is null) return;
        if (args.StatusType == GestureStatus.Started) (_lastPanX, _lastPanY) = (0, 0);
        if (args.StatusType != GestureStatus.Running) return;
        _review.PanBy(-(args.TotalX - _lastPanX), -(args.TotalY - _lastPanY));
        (_lastPanX, _lastPanY) = (args.TotalX, args.TotalY);
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs args)
    {
        if (_review is null) return;
        if (args.Status == GestureStatus.Started) _lastScale = 1;
        if (args.Status != GestureStatus.Running || _lastScale <= 0) return;
        _review.ZoomTimeAt(args.Scale / _lastScale, args.ScaleOrigin.X * _timeline.Width);
        _lastScale = args.Scale;
    }

    private void OnOverviewPan(object? sender, PanUpdatedEventArgs args)
    {
        if (_review is null || _overview.Width <= 0) return;
        if (args.StatusType == GestureStatus.Started) _lastPanX = 0;
        if (args.StatusType != GestureStatus.Running) return;
        var seconds = (args.TotalX - _lastPanX) / _overview.Width * _review.Duration.TotalSeconds;
        _review.ScrollTo(_review.Camera.TimeOffset + seconds);
        _lastPanX = args.TotalX;
    }
}
