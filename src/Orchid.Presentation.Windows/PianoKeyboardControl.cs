namespace Orchid.Presentation.Windows;

internal sealed class PianoKeyboardControl : Control
{
    private const int FirstPianoMidiNote = 21;
    private const int LastPianoMidiNote = 108;
    private readonly HashSet<int> activeNotes = [];

    public PianoKeyboardControl()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(600, 180);
        BackColor = Color.DimGray;
    }

    public void SetActiveNotes(IEnumerable<int> midiNoteNumbers)
    {
        var nextActiveNotes = midiNoteNumbers.ToHashSet();

        if (activeNotes.SetEquals(nextActiveNotes))
        {
            return;
        }

        activeNotes.Clear();
        activeNotes.UnionWith(nextActiveNotes);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        var graphics = eventArgs.Graphics;
        var whiteNotes = GetWhiteNotes();
        var whiteKeyWidth = ClientSize.Width / (float)whiteNotes.Count;
        var blackKeyWidth = whiteKeyWidth * 0.62f;
        var blackKeyHeight = ClientSize.Height * 0.62f;

        for (var whiteIndex = 0; whiteIndex < whiteNotes.Count; whiteIndex++)
        {
            var midiNoteNumber = whiteNotes[whiteIndex];
            var bounds = new RectangleF(whiteIndex * whiteKeyWidth, 0, whiteKeyWidth, ClientSize.Height);
            using var brush = new SolidBrush(activeNotes.Contains(midiNoteNumber) ? Color.DeepSkyBlue : Color.WhiteSmoke);

            graphics.FillRectangle(brush, bounds);
            graphics.DrawRectangle(Pens.Black, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        for (var midiNoteNumber = FirstPianoMidiNote; midiNoteNumber <= LastPianoMidiNote; midiNoteNumber++)
        {
            if (IsWhiteKey(midiNoteNumber))
            {
                continue;
            }

            var precedingWhiteKeys = CountWhiteKeysBefore(midiNoteNumber);
            var bounds = new RectangleF(
                precedingWhiteKeys * whiteKeyWidth - (blackKeyWidth / 2),
                0,
                blackKeyWidth,
                blackKeyHeight);
            using var brush = new SolidBrush(activeNotes.Contains(midiNoteNumber) ? Color.RoyalBlue : Color.Black);

            graphics.FillRectangle(brush, bounds);
        }
    }

    private static List<int> GetWhiteNotes()
    {
        var notes = new List<int>();

        for (var midiNoteNumber = FirstPianoMidiNote; midiNoteNumber <= LastPianoMidiNote; midiNoteNumber++)
        {
            if (IsWhiteKey(midiNoteNumber))
            {
                notes.Add(midiNoteNumber);
            }
        }

        return notes;
    }

    private static int CountWhiteKeysBefore(int midiNoteNumber)
    {
        var count = 0;

        for (var note = FirstPianoMidiNote; note < midiNoteNumber; note++)
        {
            if (IsWhiteKey(note))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsWhiteKey(int midiNoteNumber)
    {
        return midiNoteNumber % 12 is 0 or 2 or 4 or 5 or 7 or 9 or 11;
    }
}
