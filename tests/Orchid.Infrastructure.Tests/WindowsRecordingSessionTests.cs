using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Application;
using Orchid.Core;
using Orchid.Platform.Windows;

namespace Orchid.Infrastructure.Tests;

[TestClass]
public class WindowsRecordingSessionTests
{
    [TestMethod]
    public async Task Run_PublishesLiveNotesAndDetachesNotificationAfterStop()
    {
        // Arrange
        var device = new TestInputDevice();
        var clock = new ManualClock();
        using var session = new WindowsRecordingSession(device, new TestSound(), new TestDelay(), clock, 120, 4);
        var notifications = new List<(int, KeyInteraction, long)>();
        session.NoteReceived += (midi, interaction, time) => notifications.Add((midi, interaction, time));
        using var stop = new CancellationTokenSource();
        var running = session.RunAsync(stop.Token);

        // Act
        clock.Milliseconds = 100;
        device.Emit(new NoteOnEvent((SevenBitNumber)66, (SevenBitNumber)100));
        clock.Milliseconds = 400;
        device.Emit(new NoteOnEvent((SevenBitNumber)66, (SevenBitNumber)0));
        device.Emit(new ControlChangeEvent((SevenBitNumber)64, (SevenBitNumber)127));
        stop.Cancel();
        await running;
        device.Emit(new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100));

        // Assert
        CollectionAssert.AreEqual(new[]
        {
            (66, KeyInteraction.Press, 100L),
            (66, KeyInteraction.Release, 400L)
        }, notifications);
        Assert.AreEqual(0, device.SubscriberCount);
    }

    [TestMethod]
    public async Task Run_RecordsConvertedNotesAndStopsBothOperations()
    {
        // Arrange
        var device = new TestInputDevice();
        var sound = new TestSound();
        var clock = new ManualClock { Milliseconds = 5000 };
        using var session = new WindowsRecordingSession(device, sound, new TestDelay(), clock, 120, 4);
        using var stop = new CancellationTokenSource();
        var running = session.RunAsync(stop.Token);

        // Act
        clock.Milliseconds = 5100;
        device.Emit(new NoteOnEvent((SevenBitNumber)66, (SevenBitNumber)100));
        clock.Milliseconds = 5400;
        device.Emit(new NoteOnEvent((SevenBitNumber)66, (SevenBitNumber)0));
        stop.Cancel();
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Milliseconds = 9000;

        // Assert
        CollectionAssert.AreEqual(new[]
        {
            new KeyPressTiming(new KeyPress(new Tone(3), KeyInteraction.Press), 100),
            new KeyPressTiming(new KeyPress(new Tone(3), KeyInteraction.Release), 400)
        }, session.GetTimings());
        Assert.AreEqual(1, sound.StrongBeats);
        Assert.AreEqual(400L, session.ElapsedMilliseconds);
        Assert.IsFalse(device.IsListeningForEvents);
        Assert.AreEqual(0, device.SubscriberCount);
    }

    [TestMethod]
    public async Task Run_AudioFailureStopsMidiAndPropagatesCause()
    {
        // Arrange
        var device = new TestInputDevice();
        var failure = new IOException("Audio output failed.");
        var sound = new TestSound { Failure = failure };
        using var session = new WindowsRecordingSession(device, sound, new TestDelay(), new ManualClock(), 120, 4);

        // Act
        var running = session.RunAsync(default);

        // Assert
        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => running.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreSame(failure, actual);
        Assert.IsFalse(device.IsListeningForEvents);
        Assert.AreEqual(0, device.SubscriberCount);
    }

    [TestMethod]
    public async Task Run_MidiStartFailurePropagatesCauseAndDisposesResources()
    {
        // Arrange
        var failure = new IOException("MIDI input unavailable.");
        var device = new TestInputDevice { StartFailure = failure };
        var sound = new TestSound();
        var session = new WindowsRecordingSession(device, sound, new TestDelay(), new ManualClock(), 120, 4);

        // Act
        var actual = await Assert.ThrowsExactlyAsync<IOException>(() => session.RunAsync(default));
        session.Dispose();

        // Assert
        Assert.AreSame(failure, actual);
        Assert.AreEqual(0, device.SubscriberCount);
        Assert.IsTrue(device.IsDisposed);
        Assert.IsTrue(sound.IsDisposed);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    public async Task Run_AccentsFirstPulseOfEachFourQuarterMeasure(int pulseDenominator)
    {
        // Arrange
        var sound = new TestSound { BeatTarget = pulseDenominator + 1 };
        using var session = new WindowsRecordingSession(new TestInputDevice(), sound,
            new TaskDelayProvider(), new ManualClock(), 60000, pulseDenominator);
        using var stop = new CancellationTokenSource();
        var running = session.RunAsync(stop.Token);

        // Act
        try { await sound.TargetReached.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { stop.Cancel(); await running; }

        // Assert
        CollectionAssert.AreEqual(
            Enumerable.Range(0, pulseDenominator + 1).Select(pulse => pulse % pulseDenominator == 0).ToArray(),
            sound.Beats.Take(pulseDenominator + 1).ToArray());
    }

    private sealed class ManualClock : ITimeProvider
    {
        public long Milliseconds { get; set; }
        public long GetCurrentTimeMilliseconds() => Milliseconds;
    }

    private sealed class TestDelay : IDelayProvider
    {
        public Task Delay(long milliseconds, CancellationToken cancellationToken) =>
            Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private sealed class TestSound : IBeatSoundPlayer, IDisposable
    {
        public int StrongBeats { get; private set; }
        public int WeakBeats { get; private set; }
        public bool IsDisposed { get; private set; }
        public Exception? Failure { get; init; }
        public int BeatTarget { get; init; } = int.MaxValue;
        public List<bool> Beats { get; } = [];
        public TaskCompletionSource TargetReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task PlayStrongBeat(CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            StrongBeats++;
            RecordBeat(true);
            return Task.CompletedTask;
        }
        public Task PlayWeakBeat(CancellationToken cancellationToken)
        {
            WeakBeats++;
            RecordBeat(false);
            return Task.CompletedTask;
        }
        private void RecordBeat(bool strong)
        {
            Beats.Add(strong);
            if (Beats.Count >= BeatTarget) TargetReached.TrySetResult();
        }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class TestInputDevice : IInputDevice
    {
        public event EventHandler<MidiEventReceivedEventArgs>? EventReceived;
        public bool IsListeningForEvents { get; private set; }
        public bool IsDisposed { get; private set; }
        public int SubscriberCount => EventReceived?.GetInvocationList().Length ?? 0;
        public Exception? StartFailure { get; init; }
        public void StartEventsListening()
        {
            if (StartFailure is not null) throw StartFailure;
            IsListeningForEvents = true;
        }
        public void StopEventsListening() => IsListeningForEvents = false;
        public void Dispose() => IsDisposed = true;
        public void Emit(MidiEvent midiEvent) => EventReceived?.Invoke(this, new MidiEventReceivedEventArgs(midiEvent));
    }
}
