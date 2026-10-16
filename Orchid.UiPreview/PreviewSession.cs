using Orchid.Application;
using Orchid.Core;

namespace Orchid.UiPreview;

public record PreviewNote(int Midi, double Attack, double Release)
{
    private static readonly string[] Names = ["C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B"];
    public string Name => Names[Midi % 12] + (Midi / 12 - 1);
    public double Duration => Release - Attack;
}

public class PreviewSession(TimeProvider clock)
{
    public double Duration { get; private set; } = 8;
    public double Position { get; private set; } = 3.08;
    public bool IsPlaying { get; private set; }
    public int Bpm { get; set; } = 120;
    public int PulseDenominator { get; set; } = 4;
    // One flag per pulse of the measure (PulseDenominator pulses); true marks a strong beat.
    public IReadOnlyList<bool> StrongBeats { get; set; } = DefaultStrongBeats(4);
    public static bool[] DefaultStrongBeats(int pulses) => [.. Enumerable.Range(0, pulses).Select(pulse => pulse == 0)];

    public static bool[] WithBeat(IReadOnlyList<bool> beats, int index, bool strong)
    {
        var copy = beats.ToArray();
        copy[index] = strong;
        return copy;
    }

    public static string DescribeStrongBeats(IReadOnlyList<bool> beats)
    {
        var strong = Enumerable.Range(0, beats.Count).Where(beat => beats[beat]).Select(beat => beat + 1).ToArray();
        return strong.Length switch
        {
            0 => "No strong beats",
            var count when count == beats.Count => $"Every beat is strong ({count})",
            1 => $"Beat {strong[0]} of {beats.Count}",
            _ => $"Beats {string.Join(", ", strong)} of {beats.Count}",
        };
    }
    public double PulseSeconds => 60.0 / Bpm;
    public double QuarterSeconds => PulseSeconds * PulseDenominator / 4;
    public IReadOnlyList<PreviewNote> Notes { get; private set; } = CreateNotes();
    public IEnumerable<PreviewNote> HeldNotes => IsRecording
        ? _liveHeld.Values : Notes.Where(note => note.Attack <= Position && Position < note.Release);
    public bool IsRecording { get; private set; }
    private readonly Dictionary<int, PreviewNote> _liveHeld = [];
    private long _startedAt;
    private double _startedPosition;

    public void BeginRecording()
    {
        LoadRecording([], 0);
        IsRecording = true;
    }

    public void ReceiveLiveNote(int midi, KeyInteraction interaction, double position)
    {
        if (!IsRecording) return;
        ArgumentOutOfRangeException.ThrowIfNegative(midi);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(midi, 127);
        _liveHeld.Remove(midi);
        if (interaction == KeyInteraction.Press)
            _liveHeld.Add(midi, new PreviewNote(midi, position, double.PositiveInfinity));
        SetRecordingPosition(Math.Max(Position, position));
    }

    public void Seek(double position)
    {
        IsPlaying = false;
        Position = Math.Clamp(position, 0, Duration);
    }

    public void Play()
    {
        if (Position >= Duration) Position = 0;
        _startedPosition = Position;
        _startedAt = clock.GetTimestamp();
        IsPlaying = true;
    }

    public void Tick()
    {
        if (!IsPlaying) return;
        Position = Math.Min(Duration, _startedPosition + clock.GetElapsedTime(_startedAt).TotalSeconds);
        if (Position >= Duration) IsPlaying = false;
    }

    public void Pause()
    {
        Tick();
        IsPlaying = false;
    }

    public void LoadRecording(IEnumerable<KeyPressTiming> timings, double duration)
    {
        if (!double.IsFinite(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
        IsRecording = false;
        _liveHeld.Clear();
        var held = new Dictionary<int, double>();
        var notes = new List<PreviewNote>();
        foreach (var timing in timings)
        {
            var midi = 60 + (int)(timing.KeyPress.Tone.ToNum() * 2);
            var time = Math.Clamp(timing.TimeMs / 1000.0, 0, duration);
            if (held.Remove(midi, out var attack)) notes.Add(new PreviewNote(midi, attack, time));
            // An unmatched release has no attack; a repeated press closes the previous interval.
            if (timing.KeyPress.KeyInteraction == KeyInteraction.Press) held[midi] = time;
        }
        notes.AddRange(held.Select(note => new PreviewNote(note.Key, note.Value, duration)));
        Notes = notes.OrderBy(note => note.Attack).ToArray();
        Duration = duration;
        Seek(0);
    }

    public void SetRecordingPosition(double position)
    {
        Duration = position;
        Position = position;
    }

    private static PreviewNote[] CreateNotes()
    {
        int[] pitches = [60, 62, 64, 66, 67, 69, 71, 72, 71, 69, 67, 65, 64, 62, 60, 60];
        double[] offsets = [.012, -.021, .015, .032, -.008, .024, .018, -.016, .006, .027, -.012, .041, .019, -.005, .014, .022];
        return pitches.Select((midi, i) =>
        {
            var attack = i * .5 + offsets[i];
            return new PreviewNote(midi, attack, attack + (i == 6 ? .7 : .31 + i % 3 * .016));
        }).ToArray();
    }
}
