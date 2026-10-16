using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Timeline.Drawing;

/// <summary>
/// The timeline surface: ruler, pitch lanes, grid, notes with pedal tails, pedal strip and playhead. Everything goes through the review
/// transform and only the notes that are visible are visited, so the cost does not depend on the length of the recording.
/// </summary>
public sealed class TimelineDrawable(ReviewState review) : IDrawable
{
    /// <summary>From this lane height up: one row per semitone with black-key rows, and a label in a wide enough note.</summary>
    public const float TallLane = 12;

    private const float MinLabelWidth = 36, TimeRowHeight = 13, PlateHeight = 14;

    private const float HandleSize = 5, HotHandleSize = 7;

    /// <summary>Height of the pedal strip under the lanes; set by the view together with the viewport.</summary>
    public float PedalStripHeight { get; set; }

    /// <summary>Set by the view while the pointer can grab the playhead.</summary>
    public bool PlayheadHot { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var palette = Palette.Current;
        var camera = review.Camera;
        float width = dirtyRect.Width, top = (float)review.LaneTop, laneBottom = top + (float)camera.ViewportHeight;
        double from = camera.TimeOffset, to = from + camera.VisibleSeconds;
        var lines = review.Grid.Lines(Math.Max(0, from - PulseSeconds), to, camera.PxPerSecond, review.GridSettings);

        canvas.FillColor = palette.Surface;
        canvas.FillRectangle(dirtyRect);
        canvas.SaveState();
        canvas.ClipRectangle(0, top, width, laneBottom - top);
        DrawLanes(canvas, palette, width);
        DrawGridLanes(canvas, palette, lines, laneBottom);
        DrawNotes(canvas, palette, from, to, top, laneBottom);
        if (review.IsEmpty) DrawEmpty(canvas, palette, width, top, laneBottom);
        canvas.RestoreState();

        DrawRuler(canvas, palette, lines, from, to, width, top);
        if (review.HasPedal) DrawPedalStrip(canvas, palette, from, to, laneBottom);
        DrawPlayhead(canvas, palette, dirtyRect.Height);
    }

    private double PulseSeconds => 60.0 / review.Grid.Settings.Bpm;

    private float X(double seconds) => (float)review.Transform.TimeToX(seconds);

    private void DrawLanes(ICanvas canvas, Palette palette, float width)
    {
        var camera = review.Camera;
        var transform = review.Transform;
        var pitches = review.Pitches;
        var tall = camera.PxPerLane >= TallLane;
        var first = Math.Max(0, (int)Math.Floor(camera.PitchOffset));
        var last = Math.Min(pitches.LaneCount - 1, (int)Math.Ceiling(camera.PitchOffset + camera.VisibleLanes));
        var laneHeight = (float)camera.PxPerLane;
        canvas.StrokeColor = palette.GridBeat;
        canvas.StrokeSize = 1;
        for (var lane = first; lane <= last; lane++)
        {
            var midi = pitches.PitchOf(lane);
            var y = (float)transform.LaneToY(lane);
            // Tall lanes tint the black keys; thin ones alternate octave bands instead of drawing a row per semitone.
            canvas.FillColor = palette.KeyRow;
            if (tall ? IsBlack(midi) : midi / 12 % 2 == 0) canvas.FillRectangle(0, y, width, laneHeight);
            var octaveEnds = pitches.IsFolded
                ? lane + 1 < pitches.LaneCount && pitches.PitchOf(lane + 1) / 12 != midi / 12
                : midi % 12 == 0;
            if (octaveEnds && (tall || pitches.IsFolded)) canvas.DrawLine(0, y + laneHeight, width, y + laneHeight);
        }
    }

    private void DrawGridLanes(ICanvas canvas, Palette palette, IReadOnlyList<GridLine> lines, float laneBottom)
    {
        var top = (float)review.LaneTop;
        var pulseWidth = (float)(PulseSeconds * review.Camera.PxPerSecond);
        canvas.FillColor = palette.StrongBeatTint;
        foreach (var line in lines.Where(line => line.Strong)) canvas.FillRectangle(X(line.Time), top, pulseWidth, laneBottom - top);
        foreach (var line in lines)
        {
            if (line.Level == GridLevel.Subdivision) continue;
            var measure = line.Level == GridLevel.Measure;
            canvas.StrokeColor = measure ? palette.GridBar : palette.GridBeat;
            canvas.StrokeSize = measure ? 1.5f : 1;
            canvas.DrawLine(X(line.Time), top, X(line.Time), laneBottom);
        }
    }

    private void DrawNotes(ICanvas canvas, Palette palette, double from, double to, float top, float laneBottom)
    {
        var transform = review.Transform;
        var notes = review.Notes;
        var position = review.Position;
        var laneHeight = (float)review.Camera.PxPerLane;
        var labels = laneHeight >= TallLane;
        var gap = laneHeight >= 6 ? 1f : 0f;
        canvas.FontSize = 10;
        canvas.StrokeSize = 1;
        foreach (var index in notes.Visible(TimeSpan.FromSeconds(from), TimeSpan.FromSeconds(to)))
        {
            var note = notes.Notes[index];
            var box = transform.NoteBox(note);
            if (double.IsNaN(box.Y) || box.Bottom < top || box.Y > laneBottom) continue;
            var held = note.Attack <= position && position < note.Release;
            var rect = new RectF((float)box.X, (float)box.Y, (float)box.Width, (float)box.Height - gap);
            var soundEnd = notes.SoundEndOf(index);
            if (soundEnd > note.Release)
            {
                // The part that sounds only because of the pedal: a lighter tail after the key release.
                var tailX = X(note.Release.TotalSeconds);
                canvas.FillColor = palette.TailFill;
                canvas.FillRectangle(tailX, rect.Y, Math.Max((float)TimelineTransform.MinNoteWidth, X(soundEnd.TotalSeconds) - tailX), rect.Height);
            }
            canvas.FillColor = held ? palette.SelectedFill : palette.NoteFill;
            canvas.FillRoundedRectangle(rect, laneHeight >= 8 ? 3 : 0);
            if (laneHeight >= 8)
            {
                canvas.StrokeColor = held ? palette.SelectedStroke : palette.NoteStroke;
                canvas.DrawRoundedRectangle(rect, 3);
            }
            if (!labels || rect.Width < MinLabelWidth) continue;
            canvas.FontColor = held ? palette.SelectedText : palette.NoteText;
            canvas.DrawString(Display.NoteName(note.Midi), rect, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }

    private void DrawEmpty(ICanvas canvas, Palette palette, float width, float top, float laneBottom)
    {
        canvas.FontSize = 14;
        canvas.FontColor = palette.TextSecondary;
        canvas.DrawString(Strings.EmptyRecording, 0, top, width, laneBottom - top, HorizontalAlignment.Center, VerticalAlignment.Center);
    }

    // Measures show their number on a plate, beats their plain number, subdivisions are ticks; seconds label the row above.
    private void DrawRuler(ICanvas canvas, Palette palette, IReadOnlyList<GridLine> lines, double from, double to, float width, float top)
    {
        var camera = review.Camera;
        canvas.FillColor = palette.Surface;
        canvas.FillRectangle(0, 0, width, top);
        canvas.StrokeSize = 1;
        canvas.FontSize = 10;
        canvas.FontColor = palette.TextSecondary;
        foreach (var (time, text) in GridModel.TimeLabels(from, to, camera.PxPerSecond, review.Duration.TotalSeconds))
            canvas.DrawString(text, X(time) + 3, 0, 72, TimeRowHeight, HorizontalAlignment.Left, VerticalAlignment.Center);
        foreach (var line in lines)
        {
            var x = X(line.Time);
            if (line.Level == GridLevel.Subdivision)
            {
                canvas.StrokeColor = palette.GridTick;
                canvas.DrawLine(x, top - 6, x, top);
                continue;
            }
            var measure = line.Level == GridLevel.Measure;
            canvas.StrokeColor = measure ? palette.GridBar : palette.GridBeat;
            canvas.StrokeSize = measure ? 1.5f : 1;
            canvas.DrawLine(x, measure ? 0 : top - PlateHeight - 1, x, top);
            if (!line.Labeled) continue;
            if (measure)
            {
                var text = line.Bar.ToString(System.Globalization.CultureInfo.CurrentCulture);
                var plate = 8 + 7 * text.Length;
                canvas.FillColor = palette.BarLabelFill;
                canvas.FillRoundedRectangle(x + 2, top - PlateHeight - 1, plate, PlateHeight, 4);
                canvas.FontColor = palette.BarLabelText;
                canvas.DrawString(text, x + 2, top - PlateHeight - 1, plate, PlateHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            else
            {
                canvas.FontColor = palette.TextSecondary;
                canvas.DrawString(line.Beat.ToString(System.Globalization.CultureInfo.CurrentCulture), x - 8, top - PlateHeight - 1, 16, PlateHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
        canvas.StrokeColor = palette.GridBeat;
        canvas.StrokeSize = 1;
        canvas.DrawLine(0, top, width, top);
    }

    private void DrawPedalStrip(ICanvas canvas, Palette palette, double from, double to, float laneBottom)
    {
        canvas.FillColor = palette.PedalFill;
        foreach (var span in review.Notes.Pedal)
        {
            if (span.Up.TotalSeconds < from) continue;
            if (span.Down.TotalSeconds > to) break;
            var x = X(span.Down.TotalSeconds);
            canvas.FillRectangle(x, laneBottom, Math.Max((float)TimelineTransform.MinNoteWidth, X(span.Up.TotalSeconds) - x), PedalStripHeight);
        }
    }

    // The handle in the ruler shows that the playhead can be dragged; it grows while the pointer is over the playhead or the ruler.
    private void DrawPlayhead(ICanvas canvas, Palette palette, float height)
    {
        var x = X(review.Position.TotalSeconds);
        var handle = PlayheadHot ? HotHandleSize : HandleSize;
        canvas.StrokeColor = palette.Accent;
        canvas.StrokeSize = PlayheadHot ? 3 : 2;
        canvas.DrawLine(x, 0, x, height);
        var path = new PathF();
        path.MoveTo(x - handle, 0);
        path.LineTo(x + handle, 0);
        path.LineTo(x, handle * 1.4f);
        path.Close();
        canvas.FillColor = palette.Accent;
        canvas.FillPath(path);
    }

    internal static bool IsBlack(int midi) => midi % 12 is 1 or 3 or 6 or 8 or 10;
}
