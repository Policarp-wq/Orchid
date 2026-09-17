namespace Orchid.Presentation.Android;

internal sealed class MidiStreamParser(Action<MidiNoteMessage> noteReceived)
{
    private int runningStatus = -1;
    private int firstDataByte = -1;

    public void Parse(byte[] data, int offset, int count, long timestampNanoseconds)
    {
        ArgumentNullException.ThrowIfNull(data);

        var endOffset = checked(offset + count);

        for (var index = offset; index < endOffset; index++)
        {
            ParseByte(data[index], timestampNanoseconds);
        }
    }

    private void ParseByte(int value, long timestampNanoseconds)
    {
        if (value >= 0xf8)
        {
            return;
        }

        if ((value & 0x80) != 0)
        {
            runningStatus = value is >= 0x80 and <= 0xef ? value : -1;
            firstDataByte = -1;
            return;
        }

        if (runningStatus < 0)
        {
            return;
        }

        var messageType = runningStatus & 0xf0;

        if (messageType is 0xc0 or 0xd0)
        {
            firstDataByte = -1;
            return;
        }

        if (firstDataByte < 0)
        {
            firstDataByte = value;
            return;
        }

        ProcessMessage(messageType, firstDataByte, value, timestampNanoseconds);
        firstDataByte = -1;
    }

    private void ProcessMessage(int messageType, int noteNumber, int velocity, long timestampNanoseconds)
    {
        if (messageType is not (0x80 or 0x90))
        {
            return;
        }

        var isPressed = messageType == 0x90 && velocity > 0;
        noteReceived(new MidiNoteMessage(
            isPressed,
            noteNumber,
            (runningStatus & 0x0f) + 1,
            velocity,
            timestampNanoseconds));
    }
}
