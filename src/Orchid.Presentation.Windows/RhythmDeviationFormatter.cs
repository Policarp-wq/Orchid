using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal static class RhythmDeviationFormatter
{
    public static string Format(RhythmDeviation deviation)
    {
        var milliseconds = deviation.AbsoluteDeviation.TotalMilliseconds;

        if (milliseconds < 0.05)
        {
            return $"{FormatRhythmicValue(deviation.RhythmicValue)}: 0 ms";
        }

        var direction = deviation.Deviation > TimeSpan.Zero ? "late" : "early";
        return $"{FormatRhythmicValue(deviation.RhythmicValue)}: {milliseconds:0.#} ms {direction}";
    }

    public static string FormatRhythmicValue(RhythmicValue value)
    {
        return $"1/{(int)value}";
    }
}
