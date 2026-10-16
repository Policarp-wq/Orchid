using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Controls;

namespace Orchid.Ui.Tests;

[TestClass]
public class ControlLogicTests
{
    [TestMethod]
    public void BeatRows_FourBeatsInAWideBarShareOneRowAtTheMaximumBlockWidth()
    {
        // Arrange

        // Act
        var layout = BeatRows.For(4, 400, 28, 56, 8);

        // Assert
        Assert.AreEqual(new BeatRows.Layout(4, 1, 56), layout);
    }

    [TestMethod]
    public void BeatRows_BlocksShrinkTowardsTheMinimumBeforeAnythingWraps()
    {
        // Arrange

        // Act
        var layout = BeatRows.For(4, 200, 28, 56, 8);

        // Assert
        Assert.AreEqual(new BeatRows.Layout(4, 1, 44), layout);
    }

    [TestMethod]
    public void BeatRows_AMeasureThatWouldGoBelowTheMinimumWrapsOntoFurtherRows()
    {
        // Arrange

        // Act
        var layout = BeatRows.For(16, 300, 28, 56, 8);

        // Assert
        Assert.AreEqual(8, layout.Columns);
        Assert.AreEqual(2, layout.Rows);
        Assert.AreEqual(30.5, layout.BlockWidth);
    }

    [TestMethod]
    public void BeatRows_FillsRowsEvenlyInsteadOfLeavingAShortLastRow()
    {
        // Arrange

        // Act
        var layout = BeatRows.For(16, 420, 28, 56, 8);

        // Assert
        Assert.AreEqual(8, layout.Columns);
        Assert.AreEqual(2, layout.Rows);
    }

    [TestMethod]
    public void BeatRows_NeverDropsBelowOneColumnOrTheMinimumWidth()
    {
        // Arrange

        // Act
        var layout = BeatRows.For(3, 20, 28, 56, 8);

        // Assert
        Assert.AreEqual(new BeatRows.Layout(1, 3, 28), layout);
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    public void BeatRows_ABarThatIsNotMeasuredYetIsOneRow(double width)
    {
        // Arrange

        // Act
        var layout = BeatRows.For(6, width, 28, 56, 8);

        // Assert
        Assert.AreEqual(new BeatRows.Layout(6, 1, 56), layout);
    }

    [TestMethod]
    public void BeatRows_NoBeatsNeedNoRows()
    {
        // Arrange

        // Act
        var layout = BeatRows.For(0, 400, 28, 56, 8);

        // Assert
        Assert.AreEqual(0, layout.Rows);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(399, 0)]
    [DataRow(400, 1)]
    [DataRow(479, 1)]
    [DataRow(480, 2)]
    [DataRow(1000, 8)]
    public void StepperRepeat_RepeatsStartAfter400MsAndComeEvery80Ms(int heldMilliseconds, int expected)
    {
        // Arrange

        // Act
        var repeats = StepperRepeat.RepeatsAfter(TimeSpan.FromMilliseconds(heldMilliseconds));

        // Assert
        Assert.AreEqual(expected, repeats);
    }

    [TestMethod]
    [DataRow(100, 3, 5, 115)]
    [DataRow(238, 5, 1, 240)]
    [DataRow(32, -5, 1, 30)]
    [DataRow(int.MaxValue, 1, 1, 240)]
    [DataRow(int.MinValue, -1, 1, 30)]
    public void StepperRepeat_MovesInWholeStepsAndStopsAtTheLimits(int value, int steps, int step, int expected)
    {
        // Arrange

        // Act
        var moved = StepperRepeat.Move(value, steps, step, 30, 240);

        // Assert
        Assert.AreEqual(expected, moved);
    }
}
