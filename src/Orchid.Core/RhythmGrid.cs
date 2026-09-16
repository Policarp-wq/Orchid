namespace Orchid.Core;

public static class RhythmGrid
{
    public static IReadOnlyList<TimeSpan> CreateOffsets(
        Tempo tempo,
        RhythmicValue subdivision,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(tempo);

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot be negative.");
        }

        var subdivisionDuration = tempo.GetDuration(subdivision);

        if (subdivisionDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tempo), "Tempo produces a subdivision shorter than one clock tick.");
        }

        var offsets = new List<TimeSpan>();

        for (var index = 0L; ; index++)
        {
            var offsetTicks = checked(index * subdivisionDuration.Ticks);

            if (offsetTicks > duration.Ticks)
            {
                break;
            }

            offsets.Add(TimeSpan.FromTicks(offsetTicks));
        }

        return offsets;
    }
}
