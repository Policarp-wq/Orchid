namespace Orchid.Ui.Timeline;

/// <summary>
/// Pitch to lane and back; lane 0 is the top (highest) lane. The single pitch-to-y source for drawing, labels, hit testing and the overview.
/// Linear maps every semitone of a range; folded maps only the given pitches, so lane distance no longer equals the interval.
/// </summary>
public sealed class PitchMap
{
    // Empty recordings fall back to one octave from middle C.
    private const int FallbackLow = 60, FallbackHigh = 72;
    private readonly int[] _laneOf = Enumerable.Repeat(-1, 128).ToArray();
    private readonly int[] _pitchOf;

    private PitchMap(IEnumerable<int> pitches, bool folded)
    {
        _pitchOf = [.. pitches.OrderDescending()];
        for (var lane = 0; lane < _pitchOf.Length; lane++) _laneOf[_pitchOf[lane]] = lane;
        IsFolded = folded;
    }

    public bool IsFolded { get; }
    public int LaneCount => _pitchOf.Length;

    /// <summary>Every semitone from <paramref name="low"/> to <paramref name="high"/>, inclusive.</summary>
    public static PitchMap Linear(int low, int high) => new(Enumerable.Range(low, high - low + 1), folded: false);

    public static PitchMap Folded(IReadOnlyList<int> pitches) => new(pitches, folded: true);

    /// <summary>The recorded range with one lane of margin on each side, or only the used pitches when folded.</summary>
    public static PitchMap For(NoteIndex notes, bool folded)
    {
        if (notes.Lowest is not { } low || notes.Highest is not { } high) return Linear(FallbackLow, FallbackHigh);
        return folded ? Folded(notes.UsedPitches) : Linear(Math.Max(0, low - 1), Math.Min(127, high + 1));
    }

    /// <summary>The lane of a pitch, or -1 when the map has no lane for it.</summary>
    public int LaneOf(int midi) => midi is >= 0 and < 128 ? _laneOf[midi] : -1;

    public int PitchOf(int lane) => _pitchOf[lane];
}
