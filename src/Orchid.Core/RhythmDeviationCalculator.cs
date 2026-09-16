namespace Orchid.Core;

public static class RhythmDeviationCalculator
{
    private static readonly RhythmicValue[] Values =
    [
        RhythmicValue.Whole,
        RhythmicValue.Half,
        RhythmicValue.Quarter,
        RhythmicValue.Eighth,
        RhythmicValue.Sixteenth
    ];

    public static IReadOnlyList<RhythmDeviation> FindClosest(
        Tempo tempo,
        TimeSpan noteOffset,
        RhythmicValue finestSubdivision,
        int count)
    {
        ArgumentNullException.ThrowIfNull(tempo);

        if (noteOffset < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(noteOffset), "Note offset cannot be negative.");
        }

        if (!Enum.IsDefined(finestSubdivision))
        {
            throw new ArgumentOutOfRangeException(nameof(finestSubdivision));
        }

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        }

        return Values
            .Where(value => (int)value <= (int)finestSubdivision)
            .Select(value => Calculate(tempo, noteOffset, value))
            .OrderBy(result => result.AbsoluteDeviation)
            .ThenByDescending(result => result.RhythmicValue)
            .Take(count)
            .ToArray();
    }

    private static RhythmDeviation Calculate(
        Tempo tempo,
        TimeSpan noteOffset,
        RhythmicValue rhythmicValue)
    {
        var subdivisionDuration = tempo.GetDuration(rhythmicValue);
        var exactGridIndex = (decimal)noteOffset.Ticks / subdivisionDuration.Ticks;
        var nearestGridIndex = decimal.ToInt64(decimal.Round(exactGridIndex, MidpointRounding.AwayFromZero));
        var gridOffset = TimeSpan.FromTicks(checked(nearestGridIndex * subdivisionDuration.Ticks));

        return new RhythmDeviation(rhythmicValue, gridOffset, noteOffset - gridOffset);
    }
}
