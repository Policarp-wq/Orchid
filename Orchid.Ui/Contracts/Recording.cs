namespace Orchid.Ui.Contracts;

// Times are TimeSpan session offsets (100 ns resolution). Negative offsets are allowed for count-in notes.

public sealed record SessionSettings(
    int Bpm, int PulseDenominator, int MeterNumerator, int MeterDenominator,
    IReadOnlyList<bool> StrongBeats, int CountInBars)
{
    public int ClicksPerMeasure => MeterNumerator * PulseDenominator / MeterDenominator;
    public TimeSpan Pulse => TimeSpan.FromMinutes(1) / Bpm;
    public TimeSpan Measure => Pulse * ClicksPerMeasure;
}

public enum LivePhase { CountIn, Recording, Ended }

/// <summary><see cref="Bar"/> is below 1 during the count-in.</summary>
public sealed record LiveSnapshot(
    LivePhase Phase, TimeSpan Elapsed, int Bar, int Beat,
    IReadOnlyList<HeldKey> Held, bool PedalDown, SessionProblem? Problem);

public sealed record HeldKey(int Midi, int Channel, int Velocity, TimeSpan Attack);

public enum ClosedBy { Release, SessionEnd, Disconnect, Recovery }

public sealed record NoteSpan(int Midi, int Channel, int Velocity, TimeSpan Attack, TimeSpan Release, ClosedBy ClosedBy);

public sealed record PedalSpan(int Channel, TimeSpan Down, TimeSpan Up, ClosedBy ClosedBy);

public sealed record RecordingData(
    SessionSettings Settings, TimeSpan Duration,
    IReadOnlyList<NoteSpan> Notes, IReadOnlyList<PedalSpan> Pedal);

public abstract record SaveResult
{
    public sealed record Saved(string Path) : SaveResult;
    public sealed record Failed(ProblemCode Code) : SaveResult;
}

public abstract record LoadResult
{
    public sealed record Loaded(RecordingData Data, string Name) : LoadResult;
    public sealed record Missing : LoadResult;
    public sealed record Invalid : LoadResult;
    public sealed record UnsupportedVersion : LoadResult;
}

public enum ProblemCode { MidiDisconnected, MidiUnavailable, AudioFailed, SaveFailed, DiskLow }

public sealed record SessionProblem(ProblemCode Code, string? Detail);
