namespace Orchid.Application;

public sealed record RecordedNote(
    int MidiNoteNumber,
    int Channel,
    int Velocity,
    TimeSpan StartOffset,
    TimeSpan Duration)
{
    public TimeSpan EndOffset => StartOffset + Duration;
}
