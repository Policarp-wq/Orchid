using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Orchid.Application;
using Orchid.Core;
using Orchid.Infrastructure;

namespace Orchid.Platform.Windows;

public sealed class DryWetMidiInput(IInputDevice device, KeyListener listener)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        device.EventReceived += OnEventReceived;
        try
        {
            device.StartEventsListening();
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            device.EventReceived -= OnEventReceived;
            if (device.IsListeningForEvents) device.StopEventsListening();
        }
    }

    // ponytail: callbacks can overlap and outlive Stop; coordinate completion if storage requires it.
    private async void OnEventReceived(object? sender, MidiEventReceivedEventArgs args)
    {
        switch (args.Event)
        {
            case NoteOnEvent noteOn:
                var interaction = (int)noteOn.Velocity == 0 ? KeyInteraction.Release : KeyInteraction.Press;
                await listener.RememberKeyPress(new MidiNoteKey((int)noteOn.NoteNumber), interaction,
                    CancellationToken.None).ConfigureAwait(false);
                break;
            case NoteOffEvent noteOff:
                await listener.RememberKeyPress(new MidiNoteKey((int)noteOff.NoteNumber), KeyInteraction.Release,
                    CancellationToken.None).ConfigureAwait(false);
                break;
        }
    }
}
