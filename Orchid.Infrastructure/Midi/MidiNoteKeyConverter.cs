using Orchid.Application;
using Orchid.Core;
using static Orchid.Core.MusicalNote;

namespace Orchid.Infrastructure;

public sealed class MidiNoteKeyConverter : IMidiKeyConverter<MidiKey>
{
    public MusicSound ConvertKeyPressToMidiKey(MidiKey keyPress)
    {
        if (keyPress is not MidiNoteKey key)
            throw new ArgumentException("Expected a MIDI note key.", nameof(keyPress));

        var pitchClass = key.NoteNumber % 12;
        var note = pitchClass switch
        {
            0 or 1 => C,
            2 or 3 => D,
            4 => E,
            5 or 6 => F,
            7 or 8 => G,
            9 or 10 => A,
            11 => B,
            _ => throw new InvalidOperationException("Invalid MIDI pitch class.")
        };
        var alteration = pitchClass is 1 or 3 or 6 or 8 or 10
            ? AlterationSymbol.Sharp : AlterationSymbol.Natural;

        // Scientific pitch notation: MIDI note 60 is C4.
        return new MusicSound(note, key.NoteNumber / 12 - 1, alteration);
    }
}
