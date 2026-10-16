using Melanchall.DryWetMidi.Multimedia;
using Orchid.Core;
using Orchid.Platform.Windows;
using Orchid.Ui.Contracts;
using Orchid.Ui.Timeline;

namespace Orchid.Windows.Backend;

/// <summary>
/// Runs one <see cref="WindowsRecordingSession"/> and exposes it as snapshots. MIDI callbacks only update a locked key table; the UI reads
/// it once per frame, so a late callback after Stop never reaches a view.
/// </summary>
/// <remarks>
/// shortcut: the backend has no count-in (IMPLEMENTATION-DOD D2). Its clicks start at its own zero, so the first
/// <see cref="SessionSettings.CountInBars"/> bars are reported as the count-in and session offsets are backend time minus that span;
/// notes played during the count-in keep negative offsets. Replace with the backend's count-in once D2 lands.
/// </remarks>
public sealed class WindowsLiveSession : ILiveSession
{
    private readonly WindowsRecordingSession _session;
    private readonly SessionSettings _settings;
    private readonly TimeSpan _countIn;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _run;
    private readonly Lock _gate = new();
    private readonly Dictionary<int, HeldKey> _held = [];
    private Task<RecordingData>? _result;
    private bool _stopped;
    private bool _disposed;

    public WindowsLiveSession(WindowsRecordingSession session, SessionSettings settings)
    {
        _session = session;
        _settings = settings;
        _countIn = settings.Measure * settings.CountInBars;
        session.NoteReceived += OnNote;
        _run = Task.Run(() => session.RunAsync(_stop.Token));
    }

    public LiveSnapshot Read()
    {
        var elapsed = Elapsed();
        var problem = _run.IsFaulted ? Problem(_run.Exception) : null;
        HeldKey[] held;
        lock (_gate) held = problem is null ? [.. _held.Values.OrderBy(key => key.Midi)] : [];
        var phase = problem is not null ? LivePhase.Ended : elapsed < TimeSpan.Zero ? LivePhase.CountIn : LivePhase.Recording;
        var (bar, beat) = GridModel.PositionAt(elapsed, _settings);
        return new LiveSnapshot(phase, elapsed, bar, beat, held, false, problem);
    }

    public Task<RecordingData> StopAsync()
    {
        lock (_gate) return _result ??= CloseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { await StopAsync(); }
        finally
        {
            _session.NoteReceived -= OnNote;
            _session.Dispose();
            _stop.Dispose();
        }
    }

    private async Task<RecordingData> CloseAsync()
    {
        _stop.Cancel();
        // A device or audio failure already showed as the snapshot problem; what was captured is kept either way.
        try { await _run; }
        catch (Exception) { }
        lock (_gate)
        {
            _stopped = true;
            _held.Clear();
        }
        var duration = Elapsed();
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        return new RecordingData(_settings, duration, KeyPressPairing.ToNotes(_session.GetTimings(), _countIn, duration), []);
    }

    private TimeSpan Elapsed() => TimeSpan.FromMilliseconds(_session.ElapsedMilliseconds) - _countIn;

    // Called on the MIDI thread; the attack is the session offset of the callback.
    private void OnNote(int midi, KeyInteraction interaction, long milliseconds)
    {
        lock (_gate)
        {
            if (_stopped) return;
            if (interaction == KeyInteraction.Press)
                _held[midi] = new HeldKey(midi, 0, 0, TimeSpan.FromMilliseconds(milliseconds) - _countIn);
            else _held.Remove(midi);
        }
    }

    private static SessionProblem Problem(AggregateException? failure)
    {
        var exception = failure?.GetBaseException();
        var code = exception is MidiDeviceException ? ProblemCode.MidiDisconnected : ProblemCode.AudioFailed;
        return new SessionProblem(code, exception?.Message);
    }
}
