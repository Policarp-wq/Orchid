namespace Orchid.Core;

public record TimeSignature
{
    public int BeatsPerMeasure { get; init; }
    public int BeatUnit { get; init; }

    private TimeSignature(int beatsPerMeasure, int beatUnit)
    {
        BeatsPerMeasure = beatsPerMeasure;
        BeatUnit = beatUnit;
    }
    public static TimeSignature CommonTime => new TimeSignature(4,  4);
    public static TimeSignature CutTime => new TimeSignature(2, 2);
    public static TimeSignature WaltzTime => new TimeSignature(3, 4);
}
