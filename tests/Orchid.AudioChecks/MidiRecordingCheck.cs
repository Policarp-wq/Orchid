using Melanchall.DryWetMidi.Multimedia;
using Orchid.Application;
using Orchid.Platform.Windows;

namespace Orchid.AudioChecks;

sealed class MidiRecordingCheck(InputDevice device, DryWetMidiInput input, TimingStorage storage)
{
    public static void ListDevices()
    {
        var count = InputDevice.GetDevicesCount();
        if (count == 0) Console.WriteLine("No MIDI input devices found.");
        for (var index = 0; index < count; index++)
        {
            using var device = InputDevice.GetByIndex(index);
            Console.WriteLine($"{index}: {device.Name}");
        }
    }

    public async Task Run()
    {
        using var stop = new CancellationTokenSource();
        void Stop(object? sender, ConsoleCancelEventArgs args) { args.Cancel = true; stop.Cancel(); }
        void LogInput(object? sender, MidiEventReceivedEventArgs args) =>
            Console.WriteLine($"RAW {DateTimeOffset.Now:HH:mm:ss.fff} {args.Event}");
        void LogError(object? sender, ErrorOccurredEventArgs args) => Console.Error.WriteLine(args.Exception);

        Console.CancelKeyPress += Stop;
        device.EventReceived += LogInput;
        device.ErrorOccurred += LogError;
        Console.WriteLine($"Listening to '{device.Name}'. Home sound: C4. Press Ctrl+C to stop.");
        try { await input.RunAsync(stop.Token); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally
        {
            Console.CancelKeyPress -= Stop;
            device.EventReceived -= LogInput;
            device.ErrorOccurred -= LogError;
        }

        Console.WriteLine("Stored events (intervals in whole tones relative to C4):");
        foreach (var timing in storage.GetTimings())
            Console.WriteLine(FormattableString.Invariant(
                $"{timing.TimeMs,8} ms | {timing.KeyPress.KeyInteraction,-7} | {timing.KeyPress.Tone.ToNum(),5} tones"));
    }
}
