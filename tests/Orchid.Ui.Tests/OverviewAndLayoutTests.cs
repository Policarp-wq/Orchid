using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Fakes;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Tests;

[TestClass]
public class OverviewAndLayoutTests
{
    [TestMethod]
    public void OverviewColumns_ShowSilenceAndTheSoundingRange()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Shared());

        // Act
        var columns = OverviewColumns.Compute(index, duration: 1, width: 10);

        // Assert
        Assert.AreEqual(-1, columns.Low[0]);
        Assert.AreEqual((60, 64), (columns.Low[3], columns.High[3]));
        Assert.AreEqual(-1, columns.Low[7]);
        Assert.AreEqual((67, 67), (columns.Low[9], columns.High[9]));
    }

    [TestMethod]
    public void OverviewColumns_HaveOneEntryPerPixelWhateverTheNoteCount()
    {
        // Arrange
        var fiveMinutes = new NoteIndex(Fixtures.FiveMinutes());
        var oneHour = new NoteIndex(Fixtures.OneHour());

        // Act
        var short_ = OverviewColumns.Compute(fiveMinutes, 300, 800);
        var long_ = OverviewColumns.Compute(oneHour, 3600, 800);

        // Assert
        Assert.AreEqual(800, short_.Width);
        Assert.AreEqual(800, long_.Width);
        Assert.IsTrue(long_.Low.Contains(-1));
    }

    [TestMethod]
    [DataRow(1240.0, 900.0, LayoutKind.WideSide)]
    [DataRow(800.0, 900.0, LayoutKind.WideStacked)]
    [DataRow(500.0, 900.0, LayoutKind.Compact)]
    [DataRow(900.0, 400.0, LayoutKind.Compact)]
    public void LayoutRules_PickTheShellFromTheSize(double width, double height, LayoutKind expected)
    {
        // Arrange
        var thresholds = new LayoutThresholds(600, 480, 960);

        // Act
        var kind = LayoutRules.For(width, height, thresholds);

        // Assert
        Assert.AreEqual(expected, kind);
    }
}
