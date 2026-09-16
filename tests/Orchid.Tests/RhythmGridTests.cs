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
}
