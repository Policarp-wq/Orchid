namespace Orchid.Ui.Timeline;

public enum CameraMode
{
    /// <summary>The camera follows the cursor.</summary>
    Locked,

    /// <summary>The camera stays where the user panned it; the cursor may be off screen.</summary>
    Free,
}

/// <summary>
/// The visible window of the timeline in seconds and lanes. The extent is virtual (doubles), so no view is ever sized to the recording.
/// </summary>
public sealed class TimelineCamera
{
    // Proposals from IMPLEMENTATION-DOD D10 M4/M5: minimum readable and maximum scales; the fallback is for an empty recording.
    // 4 px lanes, not the proposed 8: 88 keys must open fully visible in the default window, and thin lanes draw octave bands anyway.
    public const double MinPxPerSecond = 40, MaxPxPerSecond = 2000, MinPxPerLane = 4, MaxPxPerLane = 64;
    public const double FallbackPxPerSecond = 160, FallbackPxPerLane = 24;

    private bool _isEmpty = true;
    private bool _timeFitted = true, _pitchFitted = true;

    public double Duration { get; private set; }
    public int LaneCount { get; private set; } = 1;
    public double ViewportWidth { get; private set; }
    public double ViewportHeight { get; private set; }
    public double TimeOffset { get; private set; }
    public double PitchOffset { get; private set; }
    public double PxPerSecond { get; private set; } = FallbackPxPerSecond;
    public double PxPerLane { get; private set; } = FallbackPxPerLane;
    public CameraMode Mode { get; set; }

    /// <summary>Where the cursor sits in the viewport while Locked, as a fraction of its width.</summary>
    public double FollowAnchor { get; set; } = .5;

    public double VisibleSeconds => ViewportWidth / PxPerSecond;
    public double VisibleLanes => ViewportHeight / PxPerLane;
    public bool PitchFits => LaneCount * PxPerLane <= ViewportHeight + .5;
    public bool CanZoomTimeIn => PxPerSecond < MaxPxPerSecond;
    public bool CanZoomTimeOut => PxPerSecond > FitPxPerSecond;
    public bool CanZoomPitchIn => PxPerLane < MaxPxPerLane;
    public bool CanZoomPitchOut => PxPerLane > FitPxPerLane;

    /// <summary>Fits the recording when it is readable at that scale, otherwise the minimum readable scale; also the zoom-out limit.</summary>
    public double FitPxPerSecond => _isEmpty || Duration <= 0 || ViewportWidth <= 0
        ? FallbackPxPerSecond
        : Math.Clamp(ViewportWidth / Duration, MinPxPerSecond, MaxPxPerSecond);

    public double FitPxPerLane => _isEmpty || ViewportHeight <= 0
        ? FallbackPxPerLane
        : Math.Clamp(ViewportHeight / LaneCount, MinPxPerLane, MaxPxPerLane);

    /// <summary>Starts a new recording or pitch layout and fits it.</summary>
    public void Reset(double duration, int laneCount, bool isEmpty)
    {
        Duration = Math.Max(0, duration);
        LaneCount = Math.Max(1, laneCount);
        _isEmpty = isEmpty;
        Mode = CameraMode.Locked;
        Fit();
    }

    /// <summary>Changes the lane count (fold toggle) and refits the pitch axis only.</summary>
    public void SetLaneCount(int laneCount)
    {
        LaneCount = Math.Max(1, laneCount);
        PxPerLane = FitPxPerLane;
        PitchOffset = 0;
        _pitchFitted = true;
    }

    /// <summary>A fitted axis stays fitted across resizes; a zoomed one keeps its scale within the new limits.</summary>
    public void Resize(double width, double height)
    {
        ViewportWidth = Math.Max(0, width);
        ViewportHeight = Math.Max(0, height);
        PxPerSecond = _timeFitted ? FitPxPerSecond : Math.Clamp(PxPerSecond, FitPxPerSecond, MaxPxPerSecond);
        PxPerLane = _pitchFitted ? FitPxPerLane : Math.Clamp(PxPerLane, FitPxPerLane, MaxPxPerLane);
        Clamp();
    }

    public void Fit()
    {
        PxPerSecond = FitPxPerSecond;
        PxPerLane = FitPxPerLane;
        TimeOffset = PitchOffset = 0;
        _timeFitted = _pitchFitted = true;
    }

    /// <summary>Zooms time keeping the time under <paramref name="anchorX"/> fixed. Going past a limit is ignored.</summary>
    public bool ZoomTimeAt(double factor, double anchorX)
    {
        var scale = Math.Clamp(PxPerSecond * factor, FitPxPerSecond, MaxPxPerSecond);
        if (scale == PxPerSecond) return false;
        var anchored = TimeOffset + anchorX / PxPerSecond;
        PxPerSecond = scale;
        TimeOffset = anchored - anchorX / scale;
        _timeFitted = false;
        Clamp();
        return true;
    }

    /// <summary>Zooms pitch keeping the lane under <paramref name="anchorY"/> (relative to the lane area) fixed.</summary>
    public bool ZoomPitchAt(double factor, double anchorY)
    {
        var scale = Math.Clamp(PxPerLane * factor, FitPxPerLane, MaxPxPerLane);
        if (scale == PxPerLane) return false;
        var anchored = PitchOffset + anchorY / PxPerLane;
        PxPerLane = scale;
        PitchOffset = anchored - anchorY / scale;
        _pitchFitted = false;
        Clamp();
        return true;
    }

    /// <summary>Moves the view like a map: positive dx shows later time, positive dy lower lanes. A manual pan frees the camera.</summary>
    public void PanBy(double dx, double dy)
    {
        TimeOffset += dx / PxPerSecond;
        PitchOffset += dy / PxPerLane;
        Mode = CameraMode.Free;
        Clamp();
    }

    /// <summary>Keeps the cursor at the anchor while Locked; at the edges the camera stops and the cursor leaves the anchor.</summary>
    public void Follow(double position)
    {
        if (Mode == CameraMode.Locked) TimeOffset = OffsetFor(position, FollowAnchor, PxPerSecond, Duration, ViewportWidth);
    }

    /// <summary>Puts <paramref name="start"/> at the left edge, clamped, and frees the camera (overview frame drag).</summary>
    public void ScrollTo(double start)
    {
        TimeOffset = start;
        Mode = CameraMode.Free;
        Clamp();
    }

    /// <summary>The left edge that puts <paramref name="position"/> at <paramref name="anchor"/> of the viewport, clamped to the recording.</summary>
    public static double OffsetFor(double position, double anchor, double pxPerSecond, double duration, double viewportWidth)
    {
        var visible = viewportWidth / pxPerSecond;
        return Math.Clamp(position - anchor * visible, 0, Math.Max(0, duration - visible));
    }

    private void Clamp()
    {
        TimeOffset = Math.Clamp(TimeOffset, 0, Math.Max(0, Duration - VisibleSeconds));
        PitchOffset = Math.Clamp(PitchOffset, 0, Math.Max(0, LaneCount - VisibleLanes));
    }
}
