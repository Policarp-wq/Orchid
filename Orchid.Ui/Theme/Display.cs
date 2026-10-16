using System.Globalization;
using Orchid.Ui.Contracts;

namespace Orchid.Ui.Theme;

/// <summary>Display text built from resources in the current culture. Saved data stays language-neutral.</summary>
public static class Display
{
    // shortcut: letter names only; the solfege and German settings arrive with IMPLEMENTATION-DOD D11.
    private static readonly string[] NoteNames = ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    public static string NoteName(int midi) => NoteNames[midi % 12] + (midi / 12 - 1).ToString(Culture);

    public static string Format(string format, params object[] arguments) => string.Format(Culture, format, arguments);

    /// <summary>A clock readout with a fixed number of digits: mm:ss.ff, or h:mm:ss.ff from one hour.</summary>
    public static string Clock(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        var separator = Culture.NumberFormat.NumberDecimalSeparator;
        var hundredths = (time.Milliseconds / 10).ToString("00", Culture);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}{separator}{hundredths}"
            : $"{(int)time.TotalMinutes:00}:{time.Seconds:00}{separator}{hundredths}";
    }

    /// <summary>Whole seconds as m:ss, for the small live elapsed time.</summary>
    public static string Elapsed(TimeSpan time) =>
        time < TimeSpan.Zero ? "0:00" : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    public static string Seconds(TimeSpan time) => Format(Strings.TimeSeconds, time.TotalSeconds.ToString("0.000", Culture));

    public static string Milliseconds(TimeSpan time) => Format(Strings.Milliseconds, Math.Round(time.TotalMilliseconds).ToString("0", Culture));

    // shortcut: two plural forms (English); Russian one/few/many needs the resource plural format of D11.
    public static string NoteCount(int count) => count == 1 ? Strings.NoteCountOne : Format(Strings.NoteCountMany, count);

    public static string CountIn(int bars) => bars switch
    {
        0 => Strings.CountInOff,
        1 => Strings.CountInOne,
        _ => Format(Strings.CountInMany, bars),
    };

    public static string StrongBeats(IReadOnlyList<bool> beats)
    {
        var strong = Enumerable.Range(0, beats.Count).Where(beat => beats[beat]).Select(beat => beat + 1).ToArray();
        return strong.Length switch
        {
            0 => Strings.StrongNone,
            var count when count == beats.Count => Format(Strings.StrongAll, count),
            1 => Format(Strings.StrongOne, strong[0], beats.Count),
            _ => Format(Strings.StrongMany, string.Join(", ", strong), beats.Count),
        };
    }

    public static string Problem(ProblemCode code) => code switch
    {
        ProblemCode.MidiDisconnected => Strings.ProblemMidiDisconnected,
        ProblemCode.MidiUnavailable => Strings.ProblemMidiUnavailable,
        ProblemCode.AudioFailed => Strings.ProblemAudioFailed,
        ProblemCode.SaveFailed => Strings.ProblemSaveFailed,
        ProblemCode.DiskLow => Strings.ProblemDiskLow,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}
