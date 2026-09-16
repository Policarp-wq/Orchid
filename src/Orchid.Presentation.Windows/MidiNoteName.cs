namespace Orchid.Presentation.Windows;

internal static class MidiNoteName
{
    private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static string Get(int midiNoteNumber)
    {
        if (midiNoteNumber is < 0 or > 127)
        {
            throw new ArgumentOutOfRangeException(nameof(midiNoteNumber));
        }

        return $"{Names[midiNoteNumber % Names.Length]}{(midiNoteNumber / Names.Length) - 1}";
    }
}
