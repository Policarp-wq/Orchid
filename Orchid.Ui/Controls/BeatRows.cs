namespace Orchid.Ui.Controls;

/// <summary>How the beat columns of one measure sit in rows. Pure, so the wrapping rule is tested without a window.</summary>
public static class BeatRows
{
    public readonly record struct Layout(int Columns, int Rows, double BlockWidth);

    /// <summary>
    /// Columns share the width equally; a block is at most <paramref name="maxBlock"/> wide and stays at least <paramref name="minBlock"/>,
    /// so a long measure wraps onto further rows. Rows are filled evenly (16 beats in a row of 12 give 8 + 8, not 12 + 4).
    /// A width that is not measured yet lays everything out in one row.
    /// </summary>
    public static Layout For(int beats, double width, double minBlock, double maxBlock, double spacing)
    {
        if (beats <= 0) return new Layout(1, 0, minBlock);
        if (!(width > 0)) return new Layout(beats, 1, maxBlock);
        var fits = Math.Max(1, (int)Math.Floor((width + spacing) / (minBlock + spacing)));
        var rows = (int)Math.Ceiling(beats / (double)Math.Min(beats, fits));
        var columns = (int)Math.Ceiling(beats / (double)rows);
        var block = Math.Clamp((width - spacing * (columns - 1)) / columns, minBlock, maxBlock);
        return new Layout(columns, rows, block);
    }
}
