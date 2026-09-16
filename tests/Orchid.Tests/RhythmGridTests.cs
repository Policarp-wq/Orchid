using Orchid.Core;

namespace Orchid.Tests;

public sealed class RhythmGridTests
{
    [Fact]
    public void GridUsesAbsoluteMultiplesOfSubdivisionDuration()
    {
        var tempo = new Tempo(40, RhythmicValue.Quarter);

        var offsets = RhythmGrid.CreateOffsets(
            tempo,
            RhythmicValue.Sixteenth,
            TimeSpan.FromSeconds(3));

        Assert.Equal(9, offsets.Count);
        Assert.Equal(TimeSpan.Zero, offsets[0]);
        Assert.Equal(TimeSpan.FromSeconds(1.5), offsets[4]);
        Assert.Equal(TimeSpan.FromSeconds(3), offsets[8]);
    }

    [Fact]
    public void ClosestDeviationsOnlyIncludeValuesUpToSelectedGrid()
    {
        var tempo = new Tempo(40, RhythmicValue.Quarter);

        var deviations = RhythmDeviationCalculator.FindClosest(
            tempo,
            TimeSpan.FromMilliseconds(800),
            RhythmicValue.Eighth,
            count: 2);

        Assert.Collection(
            deviations,
            deviation =>
            {
                Assert.Equal(RhythmicValue.Eighth, deviation.RhythmicValue);
                Assert.Equal(TimeSpan.FromMilliseconds(50), deviation.Deviation);
            },
            deviation =>
            {
                Assert.Equal(RhythmicValue.Quarter, deviation.RhythmicValue);
                Assert.Equal(TimeSpan.FromMilliseconds(-700), deviation.Deviation);
            });
    }

    [Fact]
    public void SixteenthIsExcludedWhenEighthGridIsSelected()
    {
        var tempo = new Tempo(40, RhythmicValue.Quarter);

        var deviations = RhythmDeviationCalculator.FindClosest(
            tempo,
            TimeSpan.FromMilliseconds(750),
            RhythmicValue.Eighth,
            count: 5);

        Assert.DoesNotContain(deviations, result => result.RhythmicValue == RhythmicValue.Sixteenth);
    }
}
