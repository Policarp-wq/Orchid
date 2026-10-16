using Orchid.Ui.Contracts;

namespace Orchid.Ui.Timeline;

public readonly record struct Box(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;
}

/// <summary>
/// Camera and pitch map applied to a viewport whose lane area starts at <see cref="Top"/>. The only time-to-x and pitch-to-y source for
/// drawing, hit testing, labels, the ruler, the playhead and the overview frame.
/// </summary>
public readonly record struct TimelineTransform(TimelineCamera Camera, PitchMap Pitches, double Top)
{
    /// <summary>Note blocks are never thinner than this, however long the recording.</summary>
    public const double MinNoteWidth = 2;

    public double TimeToX(double seconds) => (seconds - Camera.TimeOffset) * Camera.PxPerSecond;

    public double XToTime(double x) => Camera.TimeOffset + x / Camera.PxPerSecond;

    public double LaneToY(int lane) => Top + (lane - Camera.PitchOffset) * Camera.PxPerLane;

    /// <summary>The top of the pitch's lane, or NaN when the map has no lane for it.</summary>
    public double PitchToY(int midi) => Pitches.LaneOf(midi) is var lane and >= 0 ? LaneToY(lane) : double.NaN;

    public int? YToPitch(double y)
    {
        var lane = (int)Math.Floor((y - Top) / Camera.PxPerLane + Camera.PitchOffset);
        return lane >= 0 && lane < Pitches.LaneCount ? Pitches.PitchOf(lane) : null;
    }

    public Box NoteBox(NoteSpan note) => SpanBox(note.Midi, note.Attack, note.Release);

    public Box SpanBox(int midi, TimeSpan from, TimeSpan to)
    {
        var x = TimeToX(from.TotalSeconds);
        return new Box(x, PitchToY(midi), Math.Max(MinNoteWidth, TimeToX(to.TotalSeconds) - x), Camera.PxPerLane);
    }

    /// <summary>The last-attacked note whose block contains the point.</summary>
    public NoteSpan? HitTest(NoteIndex notes, double x, double y)
    {
        // A minimum-width block can extend past its release, so widen the query by that much.
        var time = XToTime(x);
        var slack = MinNoteWidth / Camera.PxPerSecond;
        var from = TimeSpan.FromSeconds(time - slack);
        var to = TimeSpan.FromSeconds(time + slack);
        var transform = this;
        return notes.Visible(from, to).Select(index => notes.Notes[index]).LastOrDefault(note => transform.NoteBox(note).Contains(x, y));
    }
}
