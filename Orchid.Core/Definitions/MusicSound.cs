namespace Orchid.Core;

public record MusicSound(MusicalNote Note, int Octave, AlterationSymbol AlterationSymbol = AlterationSymbol.Natural)
{
    public const int OctaveToneCount = 6;

    public static Tone operator -(MusicSound left, MusicSound right) =>
        Tone.FromNum(
            (left.Octave - right.Octave) * OctaveToneCount
            + left.Note.Difference(right.Note).ToNum()
            + left.AlterationOffset - right.AlterationOffset);

    private float AlterationOffset => AlterationSymbol switch
    {
        AlterationSymbol.Natural => 0f,
        AlterationSymbol.Sharp => Tone.HalfTone,
        AlterationSymbol.Flat => -Tone.HalfTone,
        _ => throw new ArgumentOutOfRangeException(nameof(AlterationSymbol))
    };
}
