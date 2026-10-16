namespace Orchid.Ui.Controls;

/// <summary>The arithmetic of <see cref="NumberStepper"/>: clamped steps and press-and-hold repeats. Pure, so it is tested without a window.</summary>
public static class StepperRepeat
{
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(400), Interval = TimeSpan.FromMilliseconds(80);

    /// <summary>Repeated steps after the first one, for a button held this long: none before the delay, then one every interval.</summary>
    public static int RepeatsAfter(TimeSpan held) => held < Delay ? 0 : 1 + (int)((held - Delay) / Interval);

    /// <summary>Moves by <paramref name="steps"/> times <paramref name="step"/> and stops at the limits.</summary>
    public static int Move(int value, int steps, int step, int minimum, int maximum) =>
        (int)Math.Clamp((long)value + (long)steps * step, minimum, maximum);
}
