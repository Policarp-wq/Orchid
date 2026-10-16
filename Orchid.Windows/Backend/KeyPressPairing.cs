using Orchid.Application;
using Orchid.Core;
using Orchid.Ui.Contracts;

namespace Orchid.Windows.Backend;

/// <summary>
/// Pairs the stored Press/Release timings into note intervals until Application keeps intervals itself (IMPLEMENTATION-DOD D1).
/// shortcut: TimingStorage bins timestamps into 20 ms and keeps no channel or velocity, and MIDI is rebuilt from the tone relative to
/// the fixed home sound C4; map Application's intervals one to one once D1 lands.
/// </summary>
public static class KeyPressPairing
{
    private const int HomeMidi = 60;

    /// <param name="origin">Backend time of session offset zero (the end of the count-in).</param>
    /// <param name="duration">Session offset of Stop; keys still held then are closed there.</param>
    public static NoteSpan[] ToNotes(IEnumerable<KeyPressTiming> timings, TimeSpan origin, TimeSpan duration)
    {
        var held = new Dictionary<int, TimeSpan>();
        var notes = new List<NoteSpan>();
        foreach (var timing in timings)
        {
            var midi = HomeMidi + (int)(timing.KeyPress.Tone.ToNum() * 2);
            var time = TimeSpan.FromMilliseconds(timing.TimeMs) - origin;
            if (time > duration) time = duration;
            if (held.Remove(midi, out var attack)) notes.Add(Note(midi, attack, time, ClosedBy.Release));
            // An unmatched release has no attack; a repeated press closes the previous interval.
            if (timing.KeyPress.KeyInteraction == KeyInteraction.Press) held[midi] = time;
        }
        notes.AddRange(held.Select(key => Note(key.Key, key.Value, duration, ClosedBy.SessionEnd)));
        return [.. notes.OrderBy(note => note.Attack).ThenBy(note => note.Midi)];
    }

    // Velocity is not stored by the backend yet.
    private static NoteSpan Note(int midi, TimeSpan attack, TimeSpan release, ClosedBy closedBy) =>
        new(midi, 0, 0, attack, release, closedBy);
}
