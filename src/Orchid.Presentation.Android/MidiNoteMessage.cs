namespace Orchid.Presentation.Android;

internal sealed record MidiNoteMessage(
    bool IsPressed,
    int MidiNoteNumber,
    int Channel,
    int Velocity,
    long TimestampNanoseconds);
