using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Tests;

[TestClass]
public class GridModelTests
{
    // Dense enough that no level is thinned out.
    private const double Dense = 2000;

    [TestMethod]
    [DataRow(1, 1, 0.5)]
    [DataRow(2, 2, 1.0)]
    [DataRow(4, 4, 2.0)]
    [DataRow(8, 8, 4.0)]
    [DataRow(16, 16, 8.0)]
    public void Lines_ClicksPerMeasureFollowThePulse(int pulse, int expectedClicks, double measureSeconds)
    {
        // Arrange
        var grid = Grid(pulse);

        // Act
        var lines = grid.Lines(0, measureSeconds - 1e-9, Dense, new GridSettings(ShowSubdivisions: false));

        // Assert
        Assert.HasCount(expectedClicks, lines);
        Assert.AreEqual(GridLevel.Measure, lines[0].Level);
        Assert.IsTrue(lines.Skip(1).All(line => line.Level == GridLevel.Beat));
        Assert.AreEqual(.5, lines.Count > 1 ? lines[1].Time : .5, 1e-12);
    }

    [TestMethod]
    public void Lines_StrongFlagsFollowThePattern()
    {
        // Arrange
        var grid = new GridModel(Fixtures.DefaultSettings with { StrongBeats = [false, false, true, false] });

        // Act
        var lines = grid.Lines(0, 1.9, Dense, new GridSettings(ShowSubdivisions: false));

        // Assert
        CollectionAssert.AreEqual(new[] { false, false, true, false }, lines.Select(line => line.Strong).ToArray());
        Assert.AreEqual(GridLevel.Measure, lines[0].Level);
    }

    [TestMethod]
    public void Lines_NumberMeasuresFromTheSessionOrigin()
    {
        // Arrange
        var grid = Grid(4);

        // Act
        var measures = grid.Lines(3, 9, Dense, new GridSettings()).Where(line => line.Level == GridLevel.Measure);

        // Assert
        CollectionAssert.AreEqual(new[] { 3, 4, 5 }, measures.Select(line => line.Bar).ToArray());
    }

    [TestMethod]
    [DataRow(4, 0)]
    [DataRow(8, 4)]
    [DataRow(16, 12)]
    public void Lines_SubdivisionsAlignWithBeats(int subdivision, int expectedSubdivisions)
    {
        // Arrange
        var grid = Grid(4);

        // Act
        var lines = grid.Lines(0, 1.999, Dense, new GridSettings(subdivision));

        // Assert
        Assert.AreEqual(expectedSubdivisions, lines.Count(line => line.Level == GridLevel.Subdivision));
        Assert.AreEqual(4, lines.Count(line => line.Level != GridLevel.Subdivision));
    }

    [TestMethod]
    public void Lines_ThinOutWithTheScale()
    {
        // Arrange
        var grid = Grid(4);

        // Act
        var dense = grid.Lines(0, 10, 200, new GridSettings(16));
        var beats = grid.Lines(0, 10, 40, new GridSettings(16));
        var measures = grid.Lines(0, 10, 10, new GridSettings(16));
        var sparse = grid.Lines(0, 10, 2, new GridSettings(16));

        // Assert
        Assert.IsTrue(dense.Any(line => line.Level == GridLevel.Subdivision));
        Assert.IsFalse(beats.Any(line => line.Level == GridLevel.Subdivision));
        Assert.IsTrue(beats.Any(line => line.Level == GridLevel.Beat));
        Assert.IsTrue(measures.All(line => line.Level == GridLevel.Measure));
        Assert.HasCount(6, measures);
        Assert.HasCount(2, sparse);
    }

    [TestMethod]
    public void Lines_NeverCloserThanTheMinimumSpacing()
    {
        // Arrange
        var grid = Grid(16);

        // Act
        var lines = grid.Lines(0, 60, 40, new GridSettings(16));
        var gaps = lines.Zip(lines.Skip(1)).Select(pair => (pair.Second.Time - pair.First.Time) * 40);

        // Assert
        Assert.IsTrue(gaps.All(gap => gap >= GridModel.MinLineSpacing - 1e-9));
    }

    [TestMethod]
    public void Lines_HaveNoDriftAfterAnHour()
    {
        // Arrange
        var grid = new GridModel(Fixtures.DefaultSettings with { Bpm = 97 });
        var pulse = 60.0 / 97;

        // Act
        var lines = grid.Lines(3599, 3600, Dense, new GridSettings(ShowSubdivisions: false));

        // Assert
        Assert.IsTrue(lines.All(line => Math.Abs(line.Time / pulse - Math.Round(line.Time / pulse)) < 1e-9));
    }

    [TestMethod]
    public void Lines_EmptyRangeHasNoLines()
    {
        // Arrange
        var grid = Grid(4);

        // Act
        var lines = grid.Lines(5, 4, Dense, new GridSettings());

        // Assert
        Assert.HasCount(0, lines);
    }

    [TestMethod]
    [DataRow(0, 1, 1)]
    [DataRow(1499, 1, 3)]
    [DataRow(2000, 2, 1)]
    [DataRow(-500, 0, 4)]
    public void PositionAt_GivesBarAndBeat(int milliseconds, int bar, int beat)
    {
        // Arrange
        var grid = Grid(4);

        // Act
        var position = grid.PositionAt(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        Assert.AreEqual((bar, beat), position);
    }

    [TestMethod]
    public void TimeLabels_UseARoundStepThatDoesNotOverlap()
    {
        // Arrange
        const double pxPerSecond = 40;

        // Act
        var labels = GridModel.TimeLabels(0, 30, pxPerSecond, duration: 30);

        // Assert
        Assert.AreEqual(2, labels[1].Time - labels[0].Time, 1e-9);
        Assert.AreEqual(0, labels[0].Time);
    }

    [TestMethod]
    [DataRow(12.0, 30.0, "12 s")]
    [DataRow(75.0, 300.0, "1:15")]
    [DataRow(3725.0, 3600.0, "1:02:05")]
    public void FormatTime_FollowsTheRecordingLength(double seconds, double duration, string expected)
    {
        // Arrange

        // Act
        var text = GridModel.FormatTime(seconds, duration, fractional: false);

        // Assert
        Assert.AreEqual(expected, text);
    }

    private static GridModel Grid(int pulse) =>
        new(Fixtures.DefaultSettings with { PulseDenominator = pulse, StrongBeats = [.. Enumerable.Range(0, pulse).Select(beat => beat == 0)] });
}
