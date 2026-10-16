using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Fakes;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Tests;

[TestClass]
public class CameraTests
{
    [TestMethod]
    public void Fit_ShortRecordingFillsTheViewport()
    {
        // Arrange
        var camera = Camera(duration: 10, lanes: 12, width: 1000, height: 480);

        // Act
        camera.Fit();

        // Assert
        Assert.AreEqual(100, camera.PxPerSecond);
        Assert.AreEqual(40, camera.PxPerLane);
        Assert.AreEqual(0, camera.TimeOffset);
    }

    [TestMethod]
    public void Fit_LongRecordingStartsAtTheMinimumReadableScale()
    {
        // Arrange
        var camera = Camera(duration: 300, lanes: 12, width: 1000, height: 480);

        // Act
        camera.Fit();

        // Assert
        Assert.AreEqual(TimelineCamera.MinPxPerSecond, camera.PxPerSecond);
        Assert.AreEqual(0, camera.TimeOffset);
    }

    [TestMethod]
    public void Fit_PianoRangeFitsWithoutVerticalScrolling()
    {
        // Arrange
        var lanes = PitchMap.For(new NoteIndex(Fixtures.WideA0ToC8()), folded: false).LaneCount;
        // The lane area of the default 1240 x 900 window.
        var camera = Camera(duration: 300, lanes: lanes, width: 830, height: 470);

        // Act
        camera.Fit();

        // Assert
        Assert.IsTrue(camera.PitchFits);
        Assert.IsGreaterThanOrEqualTo(TimelineCamera.MinPxPerLane, camera.PxPerLane);
    }

    [TestMethod]
    public void Fit_OneOctaveUsesTallLanesUpToTheMaximum()
    {
        // Arrange
        var camera = Camera(duration: 10, lanes: 3, width: 1000, height: 480);

        // Act
        camera.Fit();

        // Assert
        Assert.AreEqual(TimelineCamera.MaxPxPerLane, camera.PxPerLane);
    }

    [TestMethod]
    public void Fit_TooManyLanesKeepsTheMinimumHeightAndScrolls()
    {
        // Arrange
        var camera = Camera(duration: 10, lanes: 128, width: 1000, height: 400);

        // Act
        camera.PanBy(0, 10_000);

        // Assert
        Assert.AreEqual(TimelineCamera.MinPxPerLane, camera.PxPerLane);
        Assert.IsFalse(camera.PitchFits);
        Assert.AreEqual(128 - 400 / TimelineCamera.MinPxPerLane, camera.PitchOffset);
    }

    [TestMethod]
    public void Reset_EmptyRecordingUsesTheFallbackScale()
    {
        // Arrange
        var camera = new TimelineCamera();
        camera.Resize(1000, 480);

        // Act
        camera.Reset(4, 13, isEmpty: true);

        // Assert
        Assert.AreEqual(TimelineCamera.FallbackPxPerSecond, camera.PxPerSecond);
        Assert.AreEqual(TimelineCamera.FallbackPxPerLane, camera.PxPerLane);
    }

    [TestMethod]
    [DataRow(0.0, 0.0)]
    [DataRow(20.0, 15.0)]
    [DataRow(99.0, 90.0)]
    [DataRow(100.0, 90.0)]
    public void OffsetFor_KeepsTheCursorAtTheAnchorAndStopsAtTheEdges(double position, double expected)
    {
        // Arrange
        const double duration = 100, pxPerSecond = 100, width = 1000;

        // Act
        var offset = TimelineCamera.OffsetFor(position, .5, pxPerSecond, duration, width);

        // Assert
        Assert.AreEqual(expected, offset, 1e-9);
    }

    [TestMethod]
    public void OffsetFor_EmptyRecordingStaysAtZero()
    {
        // Arrange

        // Act
        var offset = TimelineCamera.OffsetFor(0, .5, TimelineCamera.FallbackPxPerSecond, 0, 1000);

        // Assert
        Assert.AreEqual(0, offset);
    }

    [TestMethod]
    public void ZoomTimeAt_KeepsTheTimeUnderThePointerFixed()
    {
        // Arrange
        var camera = Camera(duration: 300, lanes: 12, width: 1000, height: 480);
        camera.PanBy(4000, 0);
        var anchored = camera.TimeOffset + 250 / camera.PxPerSecond;

        // Act
        var zoomed = camera.ZoomTimeAt(2, 250);

        // Assert
        Assert.IsTrue(zoomed);
        Assert.AreEqual(80, camera.PxPerSecond);
        Assert.AreEqual(anchored, camera.TimeOffset + 250 / camera.PxPerSecond, 1e-9);
    }

    [TestMethod]
    public void ZoomTimeAt_PastTheLimitsIsIgnored()
    {
        // Arrange
        var camera = Camera(duration: 300, lanes: 12, width: 1000, height: 480);

        // Act
        var zoomedOut = camera.ZoomTimeAt(.5, 0);
        camera.ZoomTimeAt(1000, 0);
        var zoomedInFurther = camera.ZoomTimeAt(2, 0);

        // Assert
        Assert.IsFalse(zoomedOut);
        Assert.IsFalse(zoomedInFurther);
        Assert.AreEqual(TimelineCamera.MaxPxPerSecond, camera.PxPerSecond);
        Assert.IsFalse(camera.CanZoomTimeIn);
    }

    [TestMethod]
    public void ZoomTimeAt_MaximumZoomOnSixHundredSecondsKeepsAVirtualExtent()
    {
        // Arrange
        var camera = Camera(duration: 600, lanes: 40, width: 1200, height: 480);
        camera.ZoomTimeAt(1000, 0);

        // Act
        camera.PanBy(1_200_000, 0);

        // Assert
        Assert.AreEqual(600 - 1200 / TimelineCamera.MaxPxPerSecond, camera.TimeOffset, 1e-9);
        Assert.AreEqual(1200, camera.ViewportWidth);
    }

    [TestMethod]
    public void PanBy_FreesTheCameraSoFollowNoLongerMovesIt()
    {
        // Arrange
        var camera = Camera(duration: 300, lanes: 12, width: 1000, height: 480);
        camera.PanBy(400, 0);
        var panned = camera.TimeOffset;

        // Act
        camera.Follow(200);

        // Assert
        Assert.AreEqual(CameraMode.Free, camera.Mode);
        Assert.AreEqual(panned, camera.TimeOffset);
    }

    [TestMethod]
    public void PanBy_VerticalIsIgnoredWhileThePitchRangeFits()
    {
        // Arrange
        var camera = Camera(duration: 300, lanes: 12, width: 1000, height: 480);

        // Act
        camera.PanBy(0, 200);

        // Assert
        Assert.AreEqual(0, camera.PitchOffset);
    }

    [TestMethod]
    public void Follow_LockedCameraCentresTheCursor()
    {
        // Arrange
        var camera = Camera(duration: 300, lanes: 12, width: 1000, height: 480);

        // Act
        camera.Follow(100);

        // Assert
        Assert.AreEqual(100 - 12.5, camera.TimeOffset, 1e-9);
    }

    [TestMethod]
    public void Resize_FittedViewStaysFitted()
    {
        // Arrange
        var camera = Camera(duration: 10, lanes: 12, width: 1000, height: 480);

        // Act
        camera.Resize(500, 480);

        // Assert
        Assert.AreEqual(50, camera.PxPerSecond);
    }

    private static TimelineCamera Camera(double duration, int lanes, double width, double height)
    {
        var camera = new TimelineCamera();
        camera.Resize(width, height);
        camera.Reset(duration, lanes, isEmpty: false);
        return camera;
    }
}
