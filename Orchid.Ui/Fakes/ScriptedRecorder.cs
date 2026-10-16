using Orchid.Ui.Contracts;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Fakes;

/// <summary>Plays a recording as a live performance on the given clock: count-in, beats from the settings, held keys at the elapsed time.</summary>
public sealed class ScriptedRecorder(TimeProvider clock, RecordingData script) : IRecorder
{
    /// <summary>Set to make Start fail with this code.</summary>
    public ProblemCode? StartFailure { get; set; }

    /// <summary>Session offset at which the MIDI device disconnects.</summary>
    public TimeSpan? DisconnectAt { get; set; }

    public Task<ILiveSession> StartAsync(MidiInputInfo input, SessionSettings settings, CancellationToken cancellationToken)
    {
        if (StartFailure is { } code) throw new SessionStartException(code);
        return Task.FromResult<ILiveSession>(new Session(clock, script, settings, DisconnectAt));
    }

    private sealed class Session(TimeProvider clock, RecordingData script, SessionSettings settings, TimeSpan? disconnectAt) : ILiveSession
    {
        private readonly long _startedAt = clock.GetTimestamp();
        private readonly TimeSpan _countIn = settings.Measure * settings.CountInBars;
        private readonly Lock _gate = new();
        private RecordingData? _result;

        public LiveSnapshot Read()
        {
            var now = clock.GetElapsedTime(_startedAt) - _countIn;
            if (disconnectAt is { } lost && now >= lost)
                return Snapshot(LivePhase.Ended, lost, [], false, new SessionProblem(ProblemCode.MidiDisconnected, null));
            if (now < TimeSpan.Zero) return Snapshot(LivePhase.CountIn, now, [], false, null);
            HeldKey[] held = [.. script.Notes.Where(note => note.Attack <= now && now < note.Release)
                .Select(note => new HeldKey(note.Midi, note.Channel, note.Velocity, note.Attack))];
            var pedal = script.Pedal.Any(span => span.Down <= now && now < span.Up);
            return Snapshot(LivePhase.Recording, now, held, pedal, null);
        }

        public Task<RecordingData> StopAsync()
        {
            lock (_gate) _result ??= Close();
            return Task.FromResult(_result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private RecordingData Close()
        {
            var now = clock.GetElapsedTime(_startedAt) - _countIn;
            var lost = disconnectAt is { } at && now >= at;
            var end = lost ? disconnectAt!.Value : TimeSpan.FromTicks(Math.Max(0, now.Ticks));
            var closedBy = lost ? ClosedBy.Disconnect : ClosedBy.SessionEnd;
            NoteSpan[] notes = [.. script.Notes.Where(note => note.Attack < end)
                .Select(note => note.Release > end ? note with { Release = end, ClosedBy = closedBy } : note)];
            PedalSpan[] pedal = [.. script.Pedal.Where(span => span.Down < end)
                .Select(span => span.Up > end ? span with { Up = end, ClosedBy = closedBy } : span)];
            return new RecordingData(settings, end, notes, pedal);
        }

        private LiveSnapshot Snapshot(LivePhase phase, TimeSpan elapsed, IReadOnlyList<HeldKey> held, bool pedal, SessionProblem? problem)
        {
            var (bar, beat) = GridModel.PositionAt(elapsed, settings);
            return new LiveSnapshot(phase, elapsed, bar, beat, held, pedal, problem);
        }
    }
}
