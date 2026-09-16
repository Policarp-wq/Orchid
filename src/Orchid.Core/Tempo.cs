namespace Orchid.Core;

public sealed record Tempo
{
    public Tempo(decimal beatsPerMinute, RhythmicValue beatUnit)
    {
        if (beatsPerMinute <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), "Beats per minute must be greater than zero.");
        }

        if (!Enum.IsDefined(beatUnit))
        {
            throw new ArgumentOutOfRangeException(nameof(beatUnit), "The beat unit is not supported.");
        }

        BeatsPerMinute = beatsPerMinute;
        BeatUnit = beatUnit;
    }

    public decimal BeatsPerMinute { get; }

    public RhythmicValue BeatUnit { get; }

    public TimeSpan GetDuration(RhythmicValue rhythmicValue)
    {
        if (!Enum.IsDefined(rhythmicValue))
        {
            throw new ArgumentOutOfRangeException(nameof(rhythmicValue), "The rhythmic value is not supported.");
        }

        var beatDurationTicks = TimeSpan.TicksPerMinute / BeatsPerMinute;
        var durationTicks = beatDurationTicks * (int)BeatUnit / (int)rhythmicValue;

        return TimeSpan.FromTicks(decimal.ToInt64(decimal.Round(durationTicks, MidpointRounding.AwayFromZero)));
    }

    public TimeSpan GetMeasureDuration()
    {
        return GetDuration(RhythmicValue.Whole);
    }
}
