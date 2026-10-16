using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Orchid.Core.Tests;

[TestClass]
public class MetronomeTests
{
    [TestMethod]
    [DataRow(120, 10, 50)]
    [DataRow(137, 2000, 0)]
    public async Task Start_UsesAbsoluteDeadlines(int bpm, int beatCount, int subscriberMilliseconds)
    {
        // Arrange
        var clock = new ManualClock();
        var delay = new RecordingDelay(clock);
        using var stop = new CancellationTokenSource();
        var timestamps = new List<long>();
        var subscriber = new CallbackBeat(() =>
        {
            timestamps.Add(clock.Milliseconds);
            clock.Milliseconds += subscriberMilliseconds;
            if (timestamps.Count == beatCount) stop.Cancel();
        });
        var metronome = new Metronome([subscriber], delay, bpm, clock);
        var interval = 60000.0 / bpm;
        var expected = Enumerable.Range(0, beatCount).Select(i => (long)Math.Ceiling(i * interval)).ToArray();

        // Act
        try { await metronome.Start(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }

        // Assert
        CollectionAssert.AreEqual(expected, timestamps.ToArray());
        Assert.IsGreaterThanOrEqualTo(beatCount - 1, delay.Calls);
    }

    [TestMethod]
    public async Task Start_DoesNotSkipBeatsAfterSlowProcessing()
    {
        // Arrange
        var clock = new ManualClock();
        var delay = new RecordingDelay(clock);
        using var stop = new CancellationTokenSource();
        var timestamps = new List<long>();
        var subscriber = new CallbackBeat(() =>
        {
            timestamps.Add(clock.Milliseconds);
            if (timestamps.Count == 1) clock.Milliseconds += 1250;
            if (timestamps.Count == 5) stop.Cancel();
        });
        var metronome = new Metronome([subscriber], delay, 120, clock);
        long[] expected = [0, 1250, 1250, 1500, 2000];

        // Act
        try { await metronome.Start(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }

        // Assert
        CollectionAssert.AreEqual(expected, timestamps.ToArray());
    }

    [TestMethod]
    public async Task Start_LogsCriticalWhenCombinedProcessingExceedsInterval()
    {
        // Arrange
        var clock = new ManualClock();
        var delay = new RecordingDelay(clock);
        using var stop = new CancellationTokenSource();
        using var listener = new RecordingTraceListener();
        var trace = new TraceSource("MetronomeTests", SourceLevels.All);
        trace.Listeners.Clear();
        trace.Listeners.Add(listener);
        var first = new CallbackBeat(() => clock.Milliseconds += 300);
        var second = new CallbackBeat(() =>
        {
            clock.Milliseconds += 300;
            stop.Cancel();
        });
        var metronome = new Metronome([first, second], delay, 120, clock, trace);

        // Act
        try { await metronome.Start(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }

        // Assert
        CollectionAssert.AreEqual(new[] { TraceEventType.Critical }, listener.EventTypes.ToArray());
    }

    [TestMethod]
    [DataRow(499)]
    [DataRow(500)]
    public async Task Start_DoesNotLogCriticalWithinInterval(int processingMilliseconds)
    {
        // Arrange
        var clock = new ManualClock();
        var delay = new RecordingDelay(clock);
        using var stop = new CancellationTokenSource();
        using var listener = new RecordingTraceListener();
        var trace = new TraceSource("MetronomeTests", SourceLevels.All);
        trace.Listeners.Clear();
        trace.Listeners.Add(listener);
        var subscriber = new CallbackBeat(() =>
        {
            clock.Milliseconds += processingMilliseconds;
            stop.Cancel();
        });
        var metronome = new Metronome([subscriber], delay, 120, clock, trace);

        // Act
        try { await metronome.Start(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }

        // Assert
        Assert.AreEqual(0, listener.EventTypes.Count);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Constructor_RejectsNonPositiveBpm(int bpm)
    {
        // Arrange
        var delay = new RecordingDelay(new ManualClock());

        // Act
        Action act = () => new Metronome([], delay, bpm);

        // Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(act);
    }

    [TestMethod]
    public async Task Start_RejectsAlreadyCancelledToken()
    {
        // Arrange
        var delay = new RecordingDelay(new ManualClock());
        var metronome = new Metronome([], delay, 120);
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        // Act
        Func<Task> act = () => metronome.Start(stop.Token);

        // Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(act);
    }

    private sealed class ManualClock : TimeProvider
    {
        public long Milliseconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }

    private sealed class RecordingDelay(ManualClock clock) : IDelayProvider
    {
        public int Calls { get; private set; }
        public Task Delay(long milliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            clock.Milliseconds += milliseconds;
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CallbackBeat(Action callback) : IBeatSubscriber
    {
        public Task OnBeat(CancellationToken cancellationToken)
        {
            callback();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTraceListener : TraceListener
    {
        public List<TraceEventType> EventTypes { get; } = [];

        public override void TraceEvent(TraceEventCache? eventCache, string source,
            TraceEventType eventType, int id, string? message) => EventTypes.Add(eventType);

        public override void Write(string? message) { }
        public override void WriteLine(string? message) { }
    }
}
