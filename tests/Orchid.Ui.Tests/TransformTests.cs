using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Tests;

[TestClass]
public class TransformTests
{
    [TestMethod]
    public void PitchMap_LinearAddsOneLaneOfMarginOnEachSide()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Shared());

        // Act
        var map = PitchMap.For(index, folded: false);

        // Assert
        Assert.AreEqual(67 - 60 + 3, map.LaneCount);
        Assert.AreEqual(68, map.PitchOf(0));
        Assert.AreEqual(59, map.PitchOf(map.LaneCount - 1));
    }

    [TestMethod]
    public void PitchMap_FoldedGivesLanesOnlyToUsedPitches()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Shared());

        // Act
        var map = PitchMap.For(index, folded: true);

        // Assert
        Assert.AreEqual(3, map.LaneCount);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new[] { map.LaneOf(67), map.LaneOf(64), map.LaneOf(60) });
        Assert.AreEqual(-1, map.LaneOf(62));
    }

    [TestMethod]
    public void PitchMap_EmptyRecordingFallsBackToOneOctave()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Empty());

        // Act
        var map = PitchMap.For(index, folded: false);

        // Assert
        Assert.AreEqual(13, map.LaneCount);
    }

    [TestMethod]
    [DataRow(50.0, 10.0)]
    [DataRow(400.0, 25.0)]
    public void Transform_MapsTimeWithTheCameraScale(double pxPerSecond, double width)
    {
        // Arrange
        var transform = Transform(pxPerSecond, out _);

        // Act
        var x = transform.TimeToX(3);
        var back = transform.XToTime(x + width);

        // Assert
        Assert.AreEqual(3 * transform.Camera.PxPerSecond, x, 1e-9);
        Assert.AreEqual(3 + width / transform.Camera.PxPerSecond, back, 1e-9);
    }

    [TestMethod]
    public void Transform_PitchToYAndBackAgree()
    {
        // Arrange
        var transform = Transform(100, out _);

        // Act
        var y = transform.PitchToY(64);
        var pitch = transform.YToPitch(y + transform.Camera.PxPerLane / 2);

        // Assert
        Assert.AreEqual(64, pitch);
        Assert.AreEqual(transform.Top + transform.Pitches.LaneOf(64) * transform.Camera.PxPerLane, y, 1e-9);
    }

    [TestMethod]
    public void NoteBox_IsNeverThinnerThanTwoPixels()
    {
        // Arrange
        var transform = Transform(50, out _);
        var note = new NoteSpan(60, 0, 80, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(101), ClosedBy.Release);

        // Act
        var box = transform.NoteBox(note);

        // Assert
        Assert.AreEqual(TimelineTransform.MinNoteWidth, box.Width);
    }

    [TestMethod]
    public void HitTest_FindsTheNoteUnderThePoint()
    {
        // Arrange
        var transform = Transform(1000, out var index);
        var e4 = index.Notes.Single(note => note.Midi == 64);
        var box = transform.NoteBox(e4);

        // Act
        var hit = transform.HitTest(index, box.X + 5, box.Y + 2);
        var miss = transform.HitTest(index, transform.TimeToX(.8), box.Y + 2);

        // Assert
        Assert.AreEqual(e4, hit);
        Assert.IsNull(miss);
    }

    private static TimelineTransform Transform(double pxPerSecond, out NoteIndex index)
    {
        index = new NoteIndex(Fixtures.Shared());
        var map = PitchMap.For(index, folded: false);
        var camera = new TimelineCamera();
        camera.Resize(1000, 400);
        // A long duration keeps the fit scale low, so the requested scale is inside the zoom limits.
        camera.Reset(100, map.LaneCount, isEmpty: false);
        camera.ZoomTimeAt(pxPerSecond / camera.PxPerSecond, 0);
        return new TimelineTransform(camera, map, Top: 28);
    }
}
