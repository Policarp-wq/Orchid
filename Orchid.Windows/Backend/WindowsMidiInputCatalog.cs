using System.Globalization;
using Melanchall.DryWetMidi.Multimedia;
using Orchid.Platform.Windows;
using Orchid.Ui.Contracts;

namespace Orchid.Windows.Backend;

public sealed class WindowsMidiInputCatalog : IMidiInputCatalog
{
    /// <summary>The id is the device index, which <see cref="WindowsRecorder"/> opens.</summary>
    public Task<IReadOnlyList<MidiInputInfo>> GetInputsAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<MidiInputInfo> inputs = [.. WindowsRecordingSession.GetDevices()
                .Select(device => new MidiInputInfo(device.Index.ToString(CultureInfo.InvariantCulture), device.Name))];
            return Task.FromResult(inputs);
        }
        catch (MidiDeviceException)
        {
            // The setup panel then shows "No MIDI inputs found" and Refresh stays available.
            return Task.FromResult<IReadOnlyList<MidiInputInfo>>([]);
        }
    }
}
