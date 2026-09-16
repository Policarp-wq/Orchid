using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Multimedia;
using Orchid.Application;

namespace Orchid.Presentation.Windows;

internal sealed class DryWetMidiInput : IMidiInput
{
    private InputDevice? activeDevice;
    private bool isDisposed;

    public event EventHandler<MidiNoteEventArgs>? NoteReceived;

    public IReadOnlyList<string> GetDeviceNames()
    {
        ThrowIfDisposed();

        var devices = InputDevice.GetAll().ToArray();

        try
        {
            return devices.Select(device => device.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally
        {
            foreach (var device in devices)
            {
                device.Dispose();
            }
        }
    }

    public void Start(string deviceName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException("A MIDI input device must be selected.", nameof(deviceName));
        }

        Stop();

        activeDevice = InputDevice.GetByName(deviceName);
        activeDevice.SilentNoteOnPolicy = SilentNoteOnPolicy.NoteOff;
        activeDevice.EventReceived += OnEventReceived;

        try
        {
            activeDevice.StartEventsListening();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        if (activeDevice is null)
        {
            return;
        }

        activeDevice.EventReceived -= OnEventReceived;

        try
        {
            activeDevice.StopEventsListening();
        }
        finally
        {
            activeDevice.Dispose();
            activeDevice = null;
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        Stop();
        isDisposed = true;
    }

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs eventArgs)
    {
        switch (eventArgs.Event)
        {
            case NoteOnEvent noteOn when (int)noteOn.Velocity > 0:
                RaiseNoteEvent(noteOn.NoteNumber, noteOn.Channel, noteOn.Velocity, isPressed: true);
                break;
            case NoteOnEvent noteOn:
                RaiseNoteEvent(noteOn.NoteNumber, noteOn.Channel, noteOn.Velocity, isPressed: false);
                break;
            case NoteOffEvent noteOff:
                RaiseNoteEvent(noteOff.NoteNumber, noteOff.Channel, noteOff.Velocity, isPressed: false);
                break;
        }
    }

    private void RaiseNoteEvent(
        SevenBitNumber noteNumber,
        FourBitNumber channel,
        SevenBitNumber velocity,
        bool isPressed)
    {
        NoteReceived?.Invoke(
            this,
            new MidiNoteEventArgs((int)noteNumber, (int)channel, (int)velocity, isPressed));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }
}
