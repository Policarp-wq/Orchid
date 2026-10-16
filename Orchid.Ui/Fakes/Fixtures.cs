using Orchid.Ui.Contracts;

namespace Orchid.Ui.Fakes;

/// <summary>
/// Deterministic recordings for tests and Windows acceptance runs (docs/orchid-design/IMPLEMENTATION-DOD.md, "Shared acceptance fixture").
/// </summary>
public static class Fixtures
{
    public static readonly IReadOnlyList<string> Names =
    [
        nameof(Shared), nameof(Empty), nameof(TwoEvents10MsApart), nameof(SamePitchTwoChannels), nameof(Narrow10s),
        nameof(WideA0ToC8), nameof(FiveMinutes), nameof(OneHour), nameof(SurfaceLimit600s), nameof(ManyHeld), nameof(Pedal),
    ];

    public static SessionSettings DefaultSettings { get; } = new(120, 4, 4, 4, [true, false, false, false], 1);

    /// <summary>120 BPM, 1/4, 4/4, 1000 ms: C4 100-400, E4 300-600, G4 from 900 closed by Stop at 1000.</summary>
    public static RecordingData Shared() => new(DefaultSettings, Ms(1000),
    [
        Note(60, 100, 400),
        Note(64, 300, 600),
        new NoteSpan(67, 0, 90, Ms(900), Ms(1000), ClosedBy.SessionEnd),
    ], []);

    public static RecordingData Empty() => new(DefaultSettings, Ms(4000), [], []);

    public static RecordingData TwoEvents10MsApart() => new(DefaultSettings, Ms(1000), [Note(60, 100, 500), Note(62, 110, 500)], []);

    public static RecordingData SamePitchTwoChannels() => new(DefaultSettings, Ms(1000),
        [Note(60, 100, 500), Note(60, 200, 800) with { Channel = 1 }], []);

    public static RecordingData Narrow10s() => Generate(1, TimeSpan.FromSeconds(10), 60, 71, notesPerSecond: 4, gapEvery: null);

    public static RecordingData WideA0ToC8()
    {
        var recording = Generate(2, TimeSpan.FromMinutes(5), 21, 108, notesPerSecond: 8, gapEvery: null);
        // Pin both ends of the 88-key range so the fixture always spans A0 to C8.
        return recording with { Notes = [Note(21, 0, 600), .. recording.Notes, Note(108, 299_000, 299_900)] };
    }

    public static RecordingData FiveMinutes() => Generate(3, TimeSpan.FromMinutes(5), 48, 84, notesPerSecond: 8, gapEvery: null);

    /// <summary>One hour with 75 silent seconds after every 3:45 of playing.</summary>
    public static RecordingData OneHour() => Generate(4, TimeSpan.FromHours(1), 36, 96, notesPerSecond: 8, gapEvery: TimeSpan.FromMinutes(5));

    public static RecordingData SurfaceLimit600s() => Generate(5, TimeSpan.FromSeconds(600), 48, 84, notesPerSecond: 6, gapEvery: null);

    /// <summary>0 keys at 0.5 s, then 1, 3, 6 and 10 keys held over [2k+1, 2k+2) seconds.</summary>
    public static RecordingData ManyHeld()
    {
        int[] counts = [1, 3, 6, 10];
        var notes = counts.SelectMany((count, block) => Enumerable.Range(0, count)
            .Select(key => Note(48 + key * 4, 1000 + block * 2000, 2000 + block * 2000))).ToArray();
        return new RecordingData(DefaultSettings, Ms(9000), notes, []);
    }

    /// <summary>The shared fixture with the sustain pedal down from 350 to 800 ms.</summary>
    public static RecordingData Pedal() => Shared() with { Pedal = [new PedalSpan(0, Ms(350), Ms(800), ClosedBy.Release)] };

    public static RecordingData ByName(string name) => name switch
    {
        nameof(Shared) => Shared(),
        nameof(Empty) => Empty(),
        nameof(TwoEvents10MsApart) => TwoEvents10MsApart(),
        nameof(SamePitchTwoChannels) => SamePitchTwoChannels(),
        nameof(Narrow10s) => Narrow10s(),
        nameof(WideA0ToC8) => WideA0ToC8(),
        nameof(FiveMinutes) => FiveMinutes(),
        nameof(OneHour) => OneHour(),
        nameof(SurfaceLimit600s) => SurfaceLimit600s(),
        nameof(ManyHeld) => ManyHeld(),
        nameof(Pedal) => Pedal(),
        _ => throw new ArgumentException($"Unknown fixture '{name}'. Known: {string.Join(", ", Names)}.", nameof(name)),
    };

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static NoteSpan Note(int midi, int attackMs, int releaseMs) => new(midi, 0, 80, Ms(attackMs), Ms(releaseMs), ClosedBy.Release);

    // Attacks near an eighth-note grid with a small human offset, random lengths and occasional chords.
    private static RecordingData Generate(int seed, TimeSpan duration, int low, int high, int notesPerSecond, TimeSpan? gapEvery)
    {
        var random = new Random(seed);
        var notes = new List<NoteSpan>();
        var step = TimeSpan.FromSeconds(1.0 / notesPerSecond);
        for (var slot = 0L; slot * step < duration - TimeSpan.FromSeconds(1); slot++)
        {
            var at = slot * step;
            if (gapEvery is { } period && at.Ticks % period.Ticks >= period.Ticks * 3 / 4) continue;
            var chord = random.NextDouble() < .15 ? 3 : 1;
            var root = random.Next(low, high - (chord - 1) * 4 + 1);
            for (var key = 0; key < chord; key++)
            {
                var attack = at + TimeSpan.FromMilliseconds(random.Next(-20, 21));
                if (attack < TimeSpan.Zero) attack = TimeSpan.Zero;
                var release = attack + TimeSpan.FromMilliseconds(random.Next(120, 900));
                notes.Add(new NoteSpan(Math.Min(high, root + key * 4), 0, random.Next(40, 120), attack, release, ClosedBy.Release));
            }
        }
        return new RecordingData(DefaultSettings, duration, [.. notes.OrderBy(note => note.Attack)], []);
    }
}
