using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Application;
using Orchid.Core;
using Orchid.Platform.Windows;

namespace Orchid.Infrastructure.Tests;

[TestClass]
public class DryWetMidiInputTests
{
    [TestMethod]
    [DataRow(true, 100, KeyInteraction.Press)]
    [DataRow(true, 0, KeyInteraction.Release)]
    [DataRow(false, 64, KeyInteraction.Release)]
    public async Task Run_ForwardsNoteInteractionToListener(bool noteOn, int velocity, KeyInteraction interaction)
    {
        // Arrange
        var device = new TestInputDevice();
        var storage = new RecordingStorage();
        var input = CreateInput(device, storage);
        using var stop = new CancellationTokenSource();
        var running = input.RunAsync(stop.Token);
        MidiEvent midiEvent = noteOn
            ? new NoteOnEvent((SevenBitNumber)72, (SevenBitNumber)velocity)
            : new NoteOffEvent((SevenBitNumber)72, (SevenBitNumber)velocity);

        // Act
        device.Emit(midiEvent);
        stop.Cancel();
        await Finish(running);

        // Assert
        CollectionAssert.AreEqual(new[] { new KeyPress(new Tone(6), interaction) }, storage.Saved);
        Assert.IsFalse(device.IsListeningForEvents);
        Assert.AreEqual(0, device.SubscriberCount);
        Assert.IsFalse(device.IsDisposed);
    }

    [TestMethod]
    public async Task Run_IgnoresNonNoteEvents()
    {
        // Arrange
        var device = new TestInputDevice();
        var storage = new RecordingStorage();
        var input = CreateInput(device, storage);
        using var stop = new CancellationTokenSource();
        var running = input.RunAsync(stop.Token);

        // Act
        device.Emit(new ControlChangeEvent((SevenBitNumber)64, (SevenBitNumber)127));
        device.Emit(new TimingClockEvent());
        stop.Cancel();
        await Finish(running);

        // Assert
        Assert.AreEqual(0, storage.Saved.Count);
    }

    [TestMethod]
    public async Task Run_CancellationStopsListeningAndDetachesHandler()
    {
        // Arrange
        var device = new TestInputDevice();
        var storage = new RecordingStorage();
        var input = CreateInput(device, storage);
        using var stop = new CancellationTokenSource();
        var running = input.RunAsync(stop.Token);
        var wasListening = device.IsListeningForEvents;

        // Act
        stop.Cancel();
        await Finish(running);
        device.Emit(new NoteOnEvent((SevenBitNumber)60, (SevenBitNumber)100));

        // Assert
        Assert.IsTrue(wasListening);
        Assert.AreEqual(1, device.StopCount);
        Assert.AreEqual(0, device.SubscriberCount);
        Assert.AreEqual(0, storage.Saved.Count);
    }

    [TestMethod]
    public async Task Run_FailedStartDetachesHandlerAndPropagatesError()
    {
        // Arrange
        var failure = new InvalidOperationException("Device could not start.");
        var device = new TestInputDevice { StartFailure = failure };
        var input = CreateInput(device, new RecordingStorage());

        // Act
        var running = input.RunAsync(default);

        // Assert
        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => running);
        Assert.AreSame(failure, actual);
        Assert.AreEqual(0, device.SubscriberCount);
    }

    private static DryWetMidiInput CreateInput(TestInputDevice device, RecordingStorage storage) =>
        new(device, new KeyListener(new MusicSound(MusicalNote.C, 4),
            new MidiKeyConverterProvider(new MidiNoteKeyConverter()), storage));

    private static async Task Finish(Task running)
    {
        try { await running; }
        catch (OperationCanceledException) { }
    }

    private sealed class RecordingStorage : ITimingStorage
    {
        public List<KeyPress> Saved { get; } = [];
        public Task SaveTimingAsync(KeyPress timing, CancellationToken cancellationToken)
        {
            Saved.Add(timing);
            return Task.CompletedTask;
        }
    }

    private sealed class TestInputDevice : IInputDevice
    {
        public event EventHandler<MidiEventReceivedEventArgs>? EventReceived;
        public bool IsListeningForEvents { get; private set; }
        public bool IsDisposed { get; private set; }
        public int SubscriberCount => EventReceived?.GetInvocationList().Length ?? 0;
        public int StopCount { get; private set; }
        public Exception? StartFailure { get; init; }
        public void StartEventsListening()
        {
            if (StartFailure is not null) throw StartFailure;
            IsListeningForEvents = true;
        }
        public void StopEventsListening() { StopCount++; IsListeningForEvents = false; }
        public void Dispose() { IsDisposed = true; }
        public void Emit(MidiEvent midiEvent) => EventReceived?.Invoke(this, new MidiEventReceivedEventArgs(midiEvent));
    }
}
