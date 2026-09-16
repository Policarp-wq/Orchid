namespace Orchid.Core;

public sealed record RhythmDeviation(
    RhythmicValue RhythmicValue,
    TimeSpan GridOffset,
    TimeSpan Deviation)
{
    public TimeSpan AbsoluteDeviation => Deviation.Duration();
}
