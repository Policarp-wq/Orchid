using Orchid.Core;

namespace Orchid.Tests;

public sealed class TempoTests
{
    [Fact]
    public void FortyQuarterNoteBeatsPerMinuteProducesExpectedDurations()
    {
        var tempo = new Tempo(40, RhythmicValue.Quarter);

        Assert.Equal(TimeSpan.FromSeconds(6), tempo.GetDuration(RhythmicValue.Whole));
        Assert.Equal(TimeSpan.FromSeconds(3), tempo.GetDuration(RhythmicValue.Half));
        Assert.Equal(TimeSpan.FromSeconds(1.5), tempo.GetDuration(RhythmicValue.Quarter));
        Assert.Equal(TimeSpan.FromSeconds(0.75), tempo.GetDuration(RhythmicValue.Eighth));
        Assert.Equal(TimeSpan.FromSeconds(0.375), tempo.GetDuration(RhythmicValue.Sixteenth));
    }

    [Theory]
    [InlineData(RhythmicValue.Whole, 10)]
    [InlineData(RhythmicValue.Half, 20)]
    [InlineData(RhythmicValue.Quarter, 40)]
    [InlineData(RhythmicValue.Eighth, 80)]
    [InlineData(RhythmicValue.Sixteenth, 160)]
    public void EquivalentPulseSettingsProduceTheSameWholeNoteDuration(
        RhythmicValue beatUnit,
        int beatsPerMinute)
    {
        var tempo = new Tempo(beatsPerMinute, beatUnit);

        Assert.Equal(TimeSpan.FromSeconds(6), tempo.GetDuration(RhythmicValue.Whole));
    }
}
