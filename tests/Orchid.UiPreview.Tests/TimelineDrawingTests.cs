using Microsoft.Maui.Graphics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.UiPreview;

namespace Orchid.UiPreview.Tests;

[TestClass]
public class TimelineDrawingTests
{
    [TestMethod]
    public void TimeToX_UsesSameScaleForShortAndLongRecordings()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        session.LoadRecording([], 8);
        var drawing = new TimelineDrawing(session);
        var original = drawing.TimeToX(3);

        // Act
        session.LoadRecording([], 300);
        var afterLongRecording = drawing.TimeToX(3);

        // Assert
        Assert.AreEqual(original, afterLongRecording);
        Assert.AreEqual(160f, drawing.TimeToX(4) - drawing.TimeToX(3));
    }

    [TestMethod]
    public void Bounds_PitchRowsKeepHeightAcrossOctaves()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        var drawing = new TimelineDrawing(session);

        // Act
        var middle = drawing.Bounds(new PreviewNote(60, .1, .4));
        var high = drawing.Bounds(new PreviewNote(84, .1, .4));

        // Assert
        Assert.AreEqual(22f, middle.Height);
        Assert.AreEqual(middle.Height, high.Height);
        Assert.AreEqual(24 * 24f, middle.Y - high.Y);
    }

    [TestMethod]
    [DataRow(0f, 80.0, 430f, 80f)]
    [DataRow(400f, 80.0, 430f, 430f)]
    [DataRow(50f, -80.0, 430f, 0f)]
    [DataRow(10f, 5.0, -5f, 0f)]
    public void PanOffset_StaysInsideTheScrollableRange(float current, double delta, float max, float expected)
    {
        // Arrange

        // Act
        var actual = TimelineDrawing.PanOffset(current, delta, max);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void HitTest_UsesScrolledViewportCoordinates()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        var note = session.Notes[6];
        var drawing = new TimelineDrawing(session) { ViewportOffset = 400 };

        // Act
        var bounds = drawing.Bounds(note);
        var actual = drawing.HitTest(new PointF(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));

        // Assert
        Assert.AreEqual(note, actual);
        Assert.AreEqual((float)(note.Attack * 160 - 400), bounds.X, .001f);
    }
}
