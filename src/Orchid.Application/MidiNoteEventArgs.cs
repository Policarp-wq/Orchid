namespace Orchid.Application;

public sealed class MidiNoteEventArgs(
    int midiNoteNumber,
    int channel,
    int velocity,
    bool isPressed) : EventArgs
{
    private readonly int validatedMidiNoteNumber = ValidateRange(midiNoteNumber, 0, 127, nameof(midiNoteNumber));
    private readonly int validatedChannel = ValidateRange(channel, 0, 15, nameof(channel));
    private readonly int validatedVelocity = ValidateRange(velocity, 0, 127, nameof(velocity));

    public int MidiNoteNumber => validatedMidiNoteNumber;

    public int Channel => validatedChannel;

    public int Velocity => validatedVelocity;

    public bool IsPressed { get; } = isPressed && velocity > 0;

    private static int ValidateRange(int value, int minimum, int maximum, string parameterName)
    {
        return value >= minimum && value <= maximum
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {minimum} and {maximum}.");
    }
}
