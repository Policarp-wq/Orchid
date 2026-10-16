using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Timeline.Drawing;

/// <summary>
/// The label column beside the timeline: a name on every lane when lanes are tall, only the C otherwise, and the keys held at the position
/// marked at any lane height. When the pitch range does not fit it also draws a narrow full-range strip with the visible window and the
/// counts of notes above and below it.
/// </summary>
public sealed class PitchAxisDrawable(ReviewState review) : IDrawable
{
    private const float StripWidth = 8, StripMargin = 3, MinLabelGap = 12;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var palette = Palette.Current;
        var camera = review.Camera;
        var transform = review.Transform;
        var pitches = review.Pitches;
        float top = (float)review.LaneTop, laneArea = (float)camera.ViewportHeight, laneHeight = (float)camera.PxPerLane;
        var zoomed = !camera.PitchFits;
        var labelWidth = dirtyRect.Width - (zoomed ? StripWidth + 2 * StripMargin : 0) - 6;

        canvas.FillColor = palette.Surface;
        canvas.FillRectangle(dirtyRect);
        canvas.SaveState();
        canvas.ClipRectangle(0, top, dirtyRect.Width, laneArea);
        canvas.FontSize = 11;
        var tall = laneHeight >= TimelineDrawable.TallLane;
        var first = Math.Max(0, (int)Math.Floor(camera.PitchOffset));
        var last = Math.Min(pitches.LaneCount - 1, (int)Math.Ceiling(camera.PitchOffset + camera.VisibleLanes));
        var held = review.Held.Select(note => note.Midi).ToHashSet();
        var lastLabelY = float.NegativeInfinity;
        for (var lane = first; lane <= last; lane++)
        {
            var midi = pitches.PitchOf(lane);
            var y = (float)transform.LaneToY(lane);
            var marked = held.Contains(midi);
            if (marked)
            {
                // At any lane height the mark is visible: a thin lane still gets a bar a few pixels high.
                canvas.FillColor = palette.SelectedFill;
                canvas.FillRoundedRectangle(2, y, labelWidth + 2, Math.Max(3, laneHeight - 1), 2);
            }
            var named = tall || midi % 12 == 0 || (pitches.IsFolded && (lane == 0 || pitches.PitchOf(lane - 1) / 12 != midi / 12));
            if (!named || (!tall && y - lastLabelY < MinLabelGap)) continue;
            lastLabelY = y;
            canvas.FontColor = marked ? palette.SelectedText : palette.TextSecondary;
            canvas.DrawString(Display.NoteName(midi), 4, y, labelWidth, Math.Max(laneHeight, 12), HorizontalAlignment.Left, VerticalAlignment.Center);
        }
        canvas.RestoreState();
        if (zoomed) DrawZoomed(canvas, palette, dirtyRect.Width, top, laneArea);
    }

    private void DrawZoomed(ICanvas canvas, Palette palette, float width, float top, float laneArea)
    {
        var camera = review.Camera;
        var lanes = review.Pitches.LaneCount;
        var stripX = width - StripWidth - StripMargin;
        var scale = laneArea / lanes;
        canvas.FillColor = palette.SubtleSurface;
        canvas.FillRectangle(stripX, top, StripWidth, laneArea);
        var windowTop = top + (float)camera.PitchOffset * scale;
        var windowHeight = Math.Max(3, (float)camera.VisibleLanes * scale);
        canvas.StrokeColor = palette.GridBar;
        canvas.StrokeSize = 1.5f;
        canvas.DrawRectangle(stripX, windowTop, StripWidth, windowHeight);

        var (above, below) = NotesOutsideWindow();
        canvas.FontSize = 10;
        canvas.FontColor = palette.TextSecondary;
        var textWidth = stripX - 6;
        if (above > 0) DrawCount(canvas, palette, Display.Format(Strings.NotesAbove, above), top + 1, textWidth);
        if (below > 0) DrawCount(canvas, palette, Display.Format(Strings.NotesBelow, below), top + laneArea - 13, textWidth);
    }

    private static void DrawCount(ICanvas canvas, Palette palette, string text, float y, float width)
    {
        canvas.FillColor = palette.Surface;
        canvas.FillRectangle(0, y, width + 4, 12);
        canvas.FontColor = palette.TextSecondary;
        canvas.DrawString(text, 4, y, width, 12, HorizontalAlignment.Left, VerticalAlignment.Center);
    }

    /// <summary>Notes sounding in the visible time range whose lane is above or below the visible lanes.</summary>
    private (int Above, int Below) NotesOutsideWindow()
    {
        var camera = review.Camera;
        var notes = review.Notes;
        var firstLane = camera.PitchOffset;
        var endLane = camera.PitchOffset + camera.VisibleLanes;
        int above = 0, below = 0;
        var from = TimeSpan.FromSeconds(camera.TimeOffset);
        foreach (var index in notes.Visible(from, from + TimeSpan.FromSeconds(camera.VisibleSeconds)))
        {
            var lane = review.Pitches.LaneOf(notes.Notes[index].Midi);
            if (lane < 0) continue;
            if (lane + 1 <= firstLane) above++;
            else if (lane >= endLane) below++;
        }
        return (above, below);
    }
}
