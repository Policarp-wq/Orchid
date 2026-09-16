namespace Orchid.Application;

public interface IMidiInput : IDisposable
{
    event EventHandler<MidiNoteEventArgs>? NoteReceived;

    IReadOnlyList<string> GetDeviceNames();

    void Start(string deviceName);

    void Stop();
}
