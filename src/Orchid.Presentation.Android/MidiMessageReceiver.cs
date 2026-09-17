using Android.Media.Midi;

namespace Orchid.Presentation.Android;

internal sealed class MidiMessageReceiver(Action<MidiNoteMessage> noteReceived) : MidiReceiver
{
    private readonly MidiStreamParser parser = new(noteReceived);

    public override void OnSend(byte[]? message, int offset, int count, long timestamp)
    {
        if (message is null || count == 0)
        {
            return;
        }

        parser.Parse(message, offset, count, timestamp);
    }
}
