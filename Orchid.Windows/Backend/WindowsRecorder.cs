using System.Globalization;
using Melanchall.DryWetMidi.Multimedia;
using Orchid.Platform.Windows;
using Orchid.Ui.Contracts;

namespace Orchid.Windows.Backend;

/// <summary>Starts a <see cref="WindowsRecordingSession"/>: DryWetMidi input, KeyListener, TimingStorage and the NAudio metronome.</summary>
public sealed class WindowsRecorder : IRecorder
{
    public Task<ILiveSession> StartAsync(MidiInputInfo input, SessionSettings settings, CancellationToken cancellationToken)
    {
        if (!int.TryParse(input.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            throw new SessionStartException(ProblemCode.MidiUnavailable, $"Unknown input id '{input.Id}'.");
        WindowsRecordingSession session;
        try
        {
            // Backend gap: Open takes no strong-beat pattern, so clicks accent only beat 1 (IMPLEMENTATION-DOD D2).
            session = WindowsRecordingSession.Open(index, settings.Bpm, settings.PulseDenominator);
        }
        // A device index that vanished since the last refresh is out of range.
        catch (Exception exception) when (exception is MidiDeviceException or ArgumentException)
        {
            throw new SessionStartException(ProblemCode.MidiUnavailable, exception.Message);
        }
        catch (Exception exception)
        {
            // Open creates only the MIDI device and the audio output; a failure that is not the device is the audio.
            throw new SessionStartException(ProblemCode.AudioFailed, exception.Message);
        }
        return Task.FromResult<ILiveSession>(new WindowsLiveSession(session, settings));
    }
}
