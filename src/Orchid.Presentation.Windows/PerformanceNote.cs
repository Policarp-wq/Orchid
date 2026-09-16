namespace Orchid.Presentation.Windows;

internal sealed record PerformanceNote(int MidiNoteNumber, TimeSpan StartOffset, TimeSpan Duration)
{
    public TimeSpan EndOffset => StartOffset + Duration;
}
