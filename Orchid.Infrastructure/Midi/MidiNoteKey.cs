using Orchid.Application;

namespace Orchid.Infrastructure;

public sealed class MidiNoteKey : MidiKey
{
    public int NoteNumber { get; }

    public MidiNoteKey(int noteNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(noteNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(noteNumber, 127);
        NoteNumber = noteNumber;
    }
}
