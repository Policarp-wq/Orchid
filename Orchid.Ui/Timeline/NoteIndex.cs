using Orchid.Ui.Contracts;

namespace Orchid.Ui.Timeline;

/// <summary>
/// Built once per recording. Answers held and visible queries without scanning every note: notes are ordered by attack, and a
/// prefix maximum of their sounding ends skips everything that ended before the queried time.
/// </summary>
public sealed class NoteIndex
{
    private readonly NoteSpan[] _notes;
    private readonly TimeSpan[] _soundEnds;
    private readonly TimeSpan[] _maxSoundEnd;
    private readonly PedalSpan[] _pedal;

    public NoteIndex(RecordingData recording)
    {
        _notes = [.. recording.Notes.OrderBy(note => note.Attack).ThenBy(note => note.Midi)];
        _pedal = [.. recording.Pedal.OrderBy(span => span.Down)];
        _soundEnds = SoundEnds();
        _maxSoundEnd = new TimeSpan[_notes.Length];
        for (var index = 0; index < _notes.Length; index++)
            _maxSoundEnd[index] = index == 0 ? _soundEnds[0] : Max(_maxSoundEnd[index - 1], _soundEnds[index]);
        UsedPitches = [.. _notes.Select(note => note.Midi).Distinct().Order()];
    }

    public IReadOnlyList<NoteSpan> Notes => _notes;
    public IReadOnlyList<PedalSpan> Pedal => _pedal;
    public IReadOnlyList<int> UsedPitches { get; }
    public int? Lowest => UsedPitches.Count == 0 ? null : UsedPitches[0];
    public int? Highest => UsedPitches.Count == 0 ? null : UsedPitches[^1];

    /// <summary>When the note stops sounding: its release, or later while the sustain pedal of its channel holds it.</summary>
    public TimeSpan SoundEndOf(int index) => _soundEnds[index];

    /// <summary>Keys physically held at <paramref name="time"/> (attack &lt;= time &lt; release), lowest first.</summary>
    public IReadOnlyList<NoteSpan> HeldAt(TimeSpan time) =>
        [.. Candidates(time, time).Select(index => _notes[index]).Where(note => note.Release > time).OrderBy(note => note.Midi).ThenBy(note => note.Channel)];

    /// <summary>Notes whose key is released but which still sound because of the pedal, lowest first.</summary>
    public IReadOnlyList<NoteSpan> SustainedAt(TimeSpan time) =>
        [.. Candidates(time, time).Where(index => _notes[index].Release <= time && time < _soundEnds[index])
            .Select(index => _notes[index]).OrderBy(note => note.Midi)];

    public bool PedalDownAt(TimeSpan time) => _pedal.Any(span => span.Down <= time && time < span.Up);

    /// <summary>Indexes of notes sounding somewhere in [from, to], including pedal tails.</summary>
    public IEnumerable<int> Visible(TimeSpan from, TimeSpan to) => Candidates(from, to).Where(index => _soundEnds[index] > from);

    /// <summary>Indexes with attack &lt;= to, skipping the prefix that stopped sounding at or before from.</summary>
    private IEnumerable<int> Candidates(TimeSpan from, TimeSpan to)
    {
        var first = FirstIndex(index => _maxSoundEnd[index] > from);
        var end = FirstIndex(index => _notes[index].Attack > to);
        for (var index = first; index < end; index++) yield return index;
    }

    // Both predicates are monotonic over the attack order, so a binary search finds the first index where they hold.
    private int FirstIndex(Func<int, bool> predicate)
    {
        int low = 0, high = _notes.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (predicate(middle)) high = middle;
            else low = middle + 1;
        }
        return low;
    }

    // A key released under the pedal sounds until the pedal goes up or the same key is struck again.
    private TimeSpan[] SoundEnds()
    {
        var ends = new TimeSpan[_notes.Length];
        var nextStrike = new Dictionary<(int Midi, int Channel), TimeSpan>();
        for (var index = _notes.Length - 1; index >= 0; index--)
        {
            var note = _notes[index];
            ends[index] = note.Release;
            var pedal = _pedal.FirstOrDefault(span => span.Channel == note.Channel && span.Down <= note.Release && note.Release < span.Up);
            if (pedal is not null)
                ends[index] = nextStrike.TryGetValue((note.Midi, note.Channel), out var strike) && strike < pedal.Up ? Max(strike, note.Release) : pedal.Up;
            nextStrike[(note.Midi, note.Channel)] = note.Attack;
        }
        return ends;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;
}
