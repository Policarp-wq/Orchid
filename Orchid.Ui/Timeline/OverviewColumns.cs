namespace Orchid.Ui.Timeline;

/// <summary>
/// The lowest and highest sounding pitch per pixel column of the overview strip, or -1 for silence. Computed in one pass over the notes
/// when a recording loads or the strip is resized, so drawing costs one column per pixel whatever the note count.
/// </summary>
public sealed class OverviewColumns
{
    private OverviewColumns(int[] low, int[] high) => (Low, High) = (low, high);

    public int[] Low { get; }
    public int[] High { get; }
    public int Width => Low.Length;

    public static OverviewColumns Compute(NoteIndex notes, double duration, int width)
    {
        width = Math.Max(0, width);
        var low = Enumerable.Repeat(-1, width).ToArray();
        var high = Enumerable.Repeat(-1, width).ToArray();
        if (width == 0 || duration <= 0) return new OverviewColumns(low, high);
        var columnsPerSecond = width / duration;
        foreach (var note in notes.Notes)
        {
            var first = Math.Clamp((int)(note.Attack.TotalSeconds * columnsPerSecond), 0, width - 1);
            var last = Math.Clamp((int)(note.Release.TotalSeconds * columnsPerSecond), first, width - 1);
            for (var column = first; column <= last; column++)
            {
                if (low[column] < 0 || note.Midi < low[column]) low[column] = note.Midi;
                if (note.Midi > high[column]) high[column] = note.Midi;
            }
        }
        return new OverviewColumns(low, high);
    }
}
