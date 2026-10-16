using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Orchid.Ui.State;
using Orchid.Ui.Timeline.Drawing;
using Point = Windows.Foundation.Point;
using Windows.System;

namespace Orchid.Ui.Timeline;

// MAUI's pointer gestures do not deliver precise moves, wheel or keys on Windows, so the timeline and the overview listen to the WinUI
// events directly. handledEventsToo: platform parents mark wheel and pointer events as handled even when they cannot use them.
public partial class TimelineView
{
    private const double WheelNotchSeconds = .5, WheelPitchLanes = 3, ClickThreshold = 4;

    private enum OverviewDrag { None, Seek, Frame }

    private Point _pressed, _last;
    private bool _pressing, _dragged;
    private OverviewDrag _overviewDrag;
    private double _frameGrab;

    partial void AttachPlatformInput(ref bool taken)
    {
        if (_timeline.Handler?.PlatformView is not UIElement timeline || _overview.Handler?.PlatformView is not UIElement overview) return;
        Listen(timeline, PointerRoutedHandler(OnTimelineWheel), UIElement.PointerWheelChangedEvent);
        Listen(timeline, PointerRoutedHandler(OnTimelinePressed), UIElement.PointerPressedEvent);
        Listen(timeline, PointerRoutedHandler(OnTimelineMoved), UIElement.PointerMovedEvent);
        Listen(timeline, PointerRoutedHandler(OnTimelineReleased), UIElement.PointerReleasedEvent);
        Listen(timeline, PointerRoutedHandler(OnTimelineCanceled), UIElement.PointerCanceledEvent);
        Listen(timeline, PointerRoutedHandler(OnTimelineCanceled), UIElement.PointerCaptureLostEvent);
        timeline.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnTimelineKey), true);
        Listen(overview, PointerRoutedHandler(OnOverviewPressed), UIElement.PointerPressedEvent);
        Listen(overview, PointerRoutedHandler(OnOverviewMoved), UIElement.PointerMovedEvent);
        Listen(overview, PointerRoutedHandler(OnOverviewReleased), UIElement.PointerReleasedEvent);
        Listen(overview, PointerRoutedHandler(OnOverviewReleased), UIElement.PointerCanceledEvent);
        overview.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnOverviewKey), true);
        foreach (var element in new[] { timeline, overview })
            if (element is Control control) control.IsTabStop = true;
        taken = true;
    }

    private static PointerEventHandler PointerRoutedHandler(Action<UIElement, PointerRoutedEventArgs> handler) =>
        (sender, args) => handler((UIElement)sender, args);

    private static void Listen(UIElement element, PointerEventHandler handler, RoutedEvent route) => element.AddHandler(route, handler, true);

    private void OnTimelineWheel(UIElement sender, PointerRoutedEventArgs args)
    {
        if (_review is not { } review) return;
        var point = args.GetCurrentPoint(sender);
        var notches = point.Properties.MouseWheelDelta / 120.0;
        var control = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Control);
        var shift = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        var camera = review.Camera;
        // The wheel never merely locks: every event moves the timeline or is ignored at its limit; the page does not scroll under it.
        args.Handled = true;
        if (point.Properties.IsHorizontalMouseWheel) review.PanBy(notches * WheelNotchSeconds * camera.PxPerSecond, 0);
        else if (control && shift) review.ZoomPitchAt(Math.Pow(ReviewState.ZoomStep, notches), point.Position.Y - review.LaneTop);
        else if (control) review.ZoomTimeAt(Math.Pow(ReviewState.ZoomStep, notches), point.Position.X);
        else if (shift) review.PanBy(0, -notches * WheelPitchLanes * camera.PxPerLane);
        else review.PanBy(-notches * WheelNotchSeconds * camera.PxPerSecond, 0);
    }

    private void OnTimelinePressed(UIElement sender, PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(sender);
        if (!point.Properties.IsLeftButtonPressed) return;
        sender.CapturePointer(args.Pointer);
        sender.Focus(FocusState.Pointer);
        _pressed = _last = point.Position;
        _pressing = true;
        _dragged = false;
    }

    // Press and drag pans both axes like a map; under the threshold the press is a click that selects.
    private void OnTimelineMoved(UIElement sender, PointerRoutedEventArgs args)
    {
        if (!_pressing || _review is not { } review) return;
        var point = args.GetCurrentPoint(sender);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _pressing = false; // released where this element could not see it
            return;
        }
        var position = point.Position;
        if (!_dragged && Math.Abs(position.X - _pressed.X) < ClickThreshold && Math.Abs(position.Y - _pressed.Y) < ClickThreshold) return;
        _dragged = true;
        review.PanBy(_last.X - position.X, _last.Y - position.Y);
        _last = position;
    }

    private void OnTimelineReleased(UIElement sender, PointerRoutedEventArgs args)
    {
        if (!_pressing) return;
        _pressing = false;
        sender.ReleasePointerCapture(args.Pointer);
        if (!_dragged && _review is { } review)
        {
            var position = args.GetCurrentPoint(sender).Position;
            review.SelectAt(position.X, position.Y);
        }
    }

    private void OnTimelineCanceled(UIElement sender, PointerRoutedEventArgs args) => _pressing = false;

    private void OnTimelineKey(object sender, KeyRoutedEventArgs args)
    {
        if (_review is not { } review) return;
        args.Handled = true;
        switch (args.Key)
        {
            case VirtualKey.Left: review.PanBy(-WheelNotchSeconds * review.Camera.PxPerSecond, 0); break;
            case VirtualKey.Right: review.PanBy(WheelNotchSeconds * review.Camera.PxPerSecond, 0); break;
            case VirtualKey.Home: review.SeekTo(TimeSpan.Zero); break;
            case VirtualKey.End: review.SeekTo(review.Duration); break;
            default: args.Handled = false; break;
        }
    }

    // A click or drag outside the frame seeks; dragging the frame moves the view.
    private void OnOverviewPressed(UIElement sender, PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(sender);
        if (!point.Properties.IsLeftButtonPressed || _review is not { } review || _overview.Width <= 0) return;
        sender.CapturePointer(args.Pointer);
        sender.Focus(FocusState.Pointer);
        var x = point.Position.X;
        var (left, right) = OverviewDrawable.Frame(review, (float)_overview.Width);
        var inFrame = x >= left && x <= right && right - left < _overview.Width - 1;
        _overviewDrag = inFrame ? OverviewDrag.Frame : OverviewDrag.Seek;
        _frameGrab = x - left;
        if (!inFrame) SeekOverview(x);
    }

    private void OnOverviewMoved(UIElement sender, PointerRoutedEventArgs args)
    {
        if (_overviewDrag == OverviewDrag.None || _review is not { } review || _overview.Width <= 0) return;
        var point = args.GetCurrentPoint(sender);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _overviewDrag = OverviewDrag.None;
            return;
        }
        if (_overviewDrag == OverviewDrag.Seek) SeekOverview(point.Position.X);
        else review.ScrollTo((point.Position.X - _frameGrab) / _overview.Width * review.Duration.TotalSeconds);
    }

    private void OnOverviewReleased(UIElement sender, PointerRoutedEventArgs args)
    {
        if (_overviewDrag == OverviewDrag.None) return;
        _overviewDrag = OverviewDrag.None;
        sender.ReleasePointerCapture(args.Pointer);
    }

    // The strip is the session-position control for the keyboard: Left and Right move one pulse, Home and End the ends.
    private void OnOverviewKey(object sender, KeyRoutedEventArgs args)
    {
        if (_review is not { } review) return;
        var pulse = TimeSpan.FromSeconds(60.0 / review.Grid.Settings.Bpm);
        args.Handled = true;
        switch (args.Key)
        {
            case VirtualKey.Left: review.SeekTo(review.Position - pulse); break;
            case VirtualKey.Right: review.SeekTo(review.Position + pulse); break;
            case VirtualKey.Home: review.SeekTo(TimeSpan.Zero); break;
            case VirtualKey.End: review.SeekTo(review.Duration); break;
            default: args.Handled = false; break;
        }
    }
}
