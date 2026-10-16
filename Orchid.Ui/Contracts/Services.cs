namespace Orchid.Ui.Contracts;

// The only door between the UI and a backend. Implementations live in the head (adapters) or in Fakes.

public sealed record MidiInputInfo(string Id, string Name);

public interface IMidiInputCatalog
{
    Task<IReadOnlyList<MidiInputInfo>> GetInputsAsync(CancellationToken cancellationToken);
}

public interface IRecorder
{
    /// <summary>Starts the count-in and capture.</summary>
    /// <exception cref="SessionStartException">The device or audio cannot start.</exception>
    Task<ILiveSession> StartAsync(MidiInputInfo input, SessionSettings settings, CancellationToken cancellationToken);
}

public interface ILiveSession : IAsyncDisposable
{
    /// <summary>Read once per UI frame. Thread-safe; the backend never calls into UI code.</summary>
    LiveSnapshot Read();

    /// <summary>Idempotent. Returns the closed, immutable recording.</summary>
    Task<RecordingData> StopAsync();
}

public interface IRecordingStore
{
    Task<SaveResult> SaveAsync(RecordingData recording, CancellationToken cancellationToken);
    Task<LoadResult> LoadAsync(string path, CancellationToken cancellationToken);
    Task<RecordingData?> FindUnfinishedAsync(CancellationToken cancellationToken);
}

public sealed record OrchidServices(IMidiInputCatalog Inputs, IRecorder Recorder, IRecordingStore Store, TimeProvider Clock);

public sealed class SessionStartException(ProblemCode code, string? detail = null)
    : Exception($"The session could not start: {code}.")
{
    public ProblemCode Code { get; } = code;
    public string? Detail { get; } = detail;
}
