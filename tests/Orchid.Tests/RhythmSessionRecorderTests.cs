using Orchid.Application;
using Orchid.Core;

namespace Orchid.Tests;

public sealed class RhythmSessionRecorderTests
{
    [Fact]
    public void RecorderTracksNoteOffsetAndDuration()
    {
        var clock = new FakeClock();
        var recorder = new RhythmSessionRecorder(clock);
        recorder.Start(new Tempo(40, RhythmicValue.Quarter));

        clock.Elapsed = TimeSpan.FromMilliseconds(100);
        recorder.Record(new MidiNoteEventArgs(60, 0, 80, isPressed: true));
        clock.Elapsed = TimeSpan.FromMilliseconds(350);
        recorder.Record(new MidiNoteEventArgs(60, 0, 20, isPressed: false));
        clock.Elapsed = TimeSpan.FromMilliseconds(600);

        var session = recorder.Stop();

        var note = Assert.Single(session.Notes);
        Assert.Equal(60, note.MidiNoteNumber);
        Assert.Equal(TimeSpan.FromMilliseconds(100), note.StartOffset);
        Assert.Equal(TimeSpan.FromMilliseconds(250), note.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(600), session.Duration);
    }

    [Fact]
    public void ZeroVelocityNoteOnReleasesAnActiveNote()
    {
        var clock = new FakeClock();
        var recorder = new RhythmSessionRecorder(clock);
        recorder.Start(new Tempo(80, RhythmicValue.Eighth));

        recorder.Record(new MidiNoteEventArgs(64, 0, 90, isPressed: true));
        clock.Elapsed = TimeSpan.FromMilliseconds(200);
        recorder.Record(new MidiNoteEventArgs(64, 0, 0, isPressed: true));

        var session = recorder.Stop();

        Assert.Equal(TimeSpan.FromMilliseconds(200), Assert.Single(session.Notes).Duration);
    }

    [Fact]
    public void StopClosesNotesThatAreStillPressed()
    {
        var clock = new FakeClock();
        var recorder = new RhythmSessionRecorder(clock);
        recorder.Start(new Tempo(60, RhythmicValue.Quarter));

        clock.Elapsed = TimeSpan.FromMilliseconds(250);
        recorder.Record(new MidiNoteEventArgs(67, 0, 100, isPressed: true));
        clock.Elapsed = TimeSpan.FromMilliseconds(1_000);

        var session = recorder.Stop();

        Assert.Equal(TimeSpan.FromMilliseconds(750), Assert.Single(session.Notes).Duration);
    }

    private sealed class FakeClock : IMonotonicClock
    {
        public TimeSpan Elapsed { get; set; }

        public void Restart()
        {
            Elapsed = TimeSpan.Zero;
        }

        public void Stop()
        {
        }
    }
}
