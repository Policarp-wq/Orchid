namespace Orchid.Ui.Timeline;

public enum LayoutKind
{
    /// <summary>Wide window: the moment panel sits beside the timeline.</summary>
    WideSide,

    /// <summary>Wide but not wide enough for the side panel: the moment panel is a compact row below the timeline.</summary>
    WideStacked,

    /// <summary>Narrow or short (a landscape phone is wide but short).</summary>
    Compact,
}

public sealed record LayoutThresholds(double CompactMaxWidth, double CompactMaxHeight, double SidePanelMinWidth);

public static class LayoutRules
{
    public static LayoutKind For(double width, double height, LayoutThresholds thresholds) =>
        width < thresholds.CompactMaxWidth || height < thresholds.CompactMaxHeight ? LayoutKind.Compact
        : width >= thresholds.SidePanelMinWidth ? LayoutKind.WideSide
        : LayoutKind.WideStacked;
}
