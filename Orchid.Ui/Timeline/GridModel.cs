using System.Globalization;
using Orchid.Ui.Contracts;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Timeline;

public enum GridLevel { Measure, Beat, Subdivision }

/// <summary>A grid line. <see cref="Strong"/> marks a strong beat column; <see cref="Labeled"/> asks the ruler for its number.</summary>
public readonly record struct GridLine(double Time, GridLevel Level, int Bar, int Beat, bool Strong, bool Labeled);

/// <summary>Display settings of the grid. They never change capture or stored data.</summary>
public sealed record GridSettings(int Subdivision = 8, bool ShowSubdivisions = true, bool ShowStrongBeats = true)
{
    public static IReadOnlyList<int> Subdivisions { get; } = [4, 8, 16];
}

/// <summary>
/// Grid lines of a session from its origin. A line's time comes from its index, never from summing intervals, so an hour has no drift.
/// Levels closer than <see cref="MinLineSpacing"/> are left out, so zooming out leaves measures and zooming in adds beats, then subdivisions.
/// </summary>
public sealed class GridModel(SessionSettings settings)
{
    public const double MinLineSpacing = 12, MinBeatLabelSpacing = 24, MinBarLabelSpacing = 36, MinTimeLabelSpacing = 72;
    private static readonly double[] TimeSteps = [.1, .2, .5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];

    public SessionSettings Settings => settings;

    public IReadOnlyList<GridLine> Lines(double from, double to, double pxPerSecond, GridSettings grid)
    {
        // Pulse and subdivision are both a whole note divided by a power of two, so all lines sit on one integer grid of units.
        // From BPM directly: a TimeSpan pulse is rounded to 100 ns and would drift by a third of a millisecond over an hour.
        var whole = 60.0 / settings.Bpm * settings.PulseDenominator;
        var unitsPerWhole = Math.Max(settings.PulseDenominator, grid.Subdivision);
        var unit = whole / unitsPerWhole;
        long perBeat = unitsPerWhole / settings.PulseDenominator, perSubdivision = unitsPerWhole / grid.Subdivision;
        var perMeasure = perBeat * settings.ClicksPerMeasure;
        var measureStride = Stride(perMeasure * unit * pxPerSecond, MinLineSpacing);
        var labelStride = Stride(perMeasure * unit * pxPerSecond, MinBarLabelSpacing);
        var beats = settings.ClicksPerMeasure > 1 && perBeat * unit * pxPerSecond >= MinLineSpacing;
        var subdivisions = grid.ShowSubdivisions && perSubdivision < perBeat && perSubdivision * unit * pxPerSecond >= MinLineSpacing;
        var beatLabels = beats && perBeat * unit * pxPerSecond >= MinBeatLabelSpacing;
        var step = subdivisions ? perSubdivision : beats ? perBeat : perMeasure * measureStride;

        var lines = new List<GridLine>();
        if (!(to >= from)) return lines;
        for (var units = (long)Math.Ceiling(from / unit / step) * step; units * unit <= to; units += step)
        {
            var bar = (int)Math.Floor(units / (double)perMeasure);
            var inMeasure = units - bar * perMeasure;
            var beat = (int)(inMeasure / perBeat) + 1;
            var onBeat = inMeasure % perBeat == 0;
            var level = inMeasure == 0 ? GridLevel.Measure : onBeat ? GridLevel.Beat : GridLevel.Subdivision;
            if (level == GridLevel.Measure && bar % measureStride != 0) continue;
            var strong = onBeat && grid.ShowStrongBeats && beats && IsStrong(beat);
            var labeled = level == GridLevel.Measure ? bar % labelStride == 0 : level == GridLevel.Beat && beatLabels;
            lines.Add(new GridLine(units * unit, level, bar + 1, beat, strong, labeled));
        }
        return lines;
    }

    /// <summary>Bar (from 1) and beat (from 1) of a session offset; negative offsets fall in the count-in bars 0, -1, ...</summary>
    public (int Bar, int Beat) PositionAt(TimeSpan time) => PositionAt(time, settings);

    public static (int Bar, int Beat) PositionAt(TimeSpan time, SessionSettings settings)
    {
        var click = (long)Math.Floor(time.TotalSeconds * settings.Bpm / 60);
        var clicks = settings.ClicksPerMeasure;
        var bar = (long)Math.Floor(click / (double)clicks);
        return ((int)bar + 1, (int)(click - bar * clicks) + 1);
    }

    /// <summary>Ruler time labels: the smallest round step that keeps them <see cref="MinTimeLabelSpacing"/> apart.</summary>
    public static IReadOnlyList<(double Time, string Text)> TimeLabels(double from, double to, double pxPerSecond, double duration)
    {
        var step = TimeSteps.FirstOrDefault(candidate => candidate * pxPerSecond >= MinTimeLabelSpacing, TimeSteps[^1]);
        var labels = new List<(double, string)>();
        for (var index = (long)Math.Ceiling(Math.Max(0, from) / step); index * step <= to; index++)
            labels.Add((index * step, FormatTime(index * step, duration, step < 1)));
        return labels;
    }

    /// <summary>Seconds, m:ss or h:mm:ss depending on the recording length, in the current culture.</summary>
    public static string FormatTime(double seconds, double duration, bool fractional)
    {
        var culture = CultureInfo.CurrentCulture;
        var time = TimeSpan.FromSeconds(Math.Round(seconds, 1));
        if (duration < 60) return string.Format(culture, Strings.TimeSeconds, seconds.ToString(fractional ? "0.0" : "0", culture));
        var tenths = fractional ? culture.NumberFormat.NumberDecimalSeparator + (time.Milliseconds / 100) : "";
        return duration < 3600
            ? $"{(int)time.TotalMinutes}:{time.Seconds:00}{tenths}"
            : $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";
    }

    private bool IsStrong(int beat) => settings.StrongBeats.Count == settings.ClicksPerMeasure && settings.StrongBeats[beat - 1];

    // The smallest power-of-two count of measures whose spacing reaches the minimum.
    private static int Stride(double spacing, double minimum)
    {
        var stride = 1;
        while (spacing * stride < minimum && stride < 1 << 20) stride *= 2;
        return stride;
    }
}
