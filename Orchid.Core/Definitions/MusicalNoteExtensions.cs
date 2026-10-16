namespace Orchid.Core;

public static class MusicalNoteExtensions
{
    public static Tone Difference(this MusicalNote note, MusicalNote other)
    {
        var a = (int)note;
        var b = (int)other;
        if(a < 0 || b < 0 || a > 6 || b > 6)
        {
            throw new ArgumentOutOfRangeException("MusicalNote values must be between 0 and 6.");
        }
        
        var difference = (float)(a - b);
        if (a < ((int)MusicalNote.F) != (b < (int)MusicalNote.F))
            difference -= Math.Sign(difference) * Tone.HalfTone;

        return Tone.FromNum(difference);
    }
}
