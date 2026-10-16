using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.State;
using Orchid.Ui.Theme;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Tests;

[TestClass]
public class ReviewStateTests
{
    private static ReviewState Loaded(ManualClock clock, RecordingData? recording = null)
    {
        var review = new ReviewState(clock);
        review.Load(recording ?? Fixtures.Shared());
        review.SetViewport(1000, 400, 28);
        return review;
    }

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    [TestMethod]
    [DataRow(0, new string[] { })]
    [DataRow(100, new[] { "C4" })]
    [DataRow(300, new[] { "C4", "E4" })]
    [DataRow(400, new[] { "E4" })]
    [DataRow(600, new string[] { })]
    [DataRow(900, new[] { "G4" })]
    [DataRow(1000, new string[] { })]
    public void SeekTo_SharedFixtureShowsTheKeysHeldAtThePosition(int milliseconds, string[] expected)
    {
        // Arrange
        var review = Loaded(new ManualClock());

        // Act
        review.SeekTo(Ms(milliseconds));

        // Assert
        CollectionAssert.AreEqual(expected, review.HeldChips.Select(chip => chip.Name).ToArray());
        CollectionAssert.AreEqual(expected, review.Held.Select(note => Display.NoteName(note.Midi)).ToArray());
    }

    [TestMethod]
    public void SeekTo_ExactReleaseLetsTheKeyGo()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        review.SeekTo(Ms(399));

        // Act
        review.SeekTo(Ms(400));

        // Assert
        Assert.HasCount(1, review.Held);
        Assert.AreEqual(64, review.Held[0].Midi);
    }

    [TestMethod]
    public void SeekTo_ChipsCarryMidiAttackAndDuration()
    {
        // Arrange
        var review = Loaded(new ManualClock());

        // Act
        review.SeekTo(Ms(100));

        // Assert
        var expected = Display.Format(Strings.HeldDetails, 60, Display.Seconds(Ms(100)), Display.Milliseconds(Ms(300)));
        Assert.AreEqual(expected, review.HeldChips[0].Details);
    }

    [TestMethod]
    public void SeekTo_InAGapClearsTheKeysAndKeepsBarAndBeat()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        review.SeekTo(Ms(100));

        // Act
        review.SeekTo(Ms(700));

        // Assert
        Assert.HasCount(0, review.HeldChips);
        Assert.AreEqual(Strings.HeldNone, review.HeldSummaryText);
        Assert.AreEqual("1", review.BarText);
        Assert.AreEqual(Display.Format(Strings.BeatOf, 2, 4), review.BeatText);
    }

    [TestMethod]
    public void SeekTo_ClampsToTheRecording()
    {
        // Arrange
        var review = Loaded(new ManualClock());

        // Act
        review.SeekTo(Ms(-5));
        var before = review.Position;
        review.SeekTo(Ms(5000));

        // Assert
        Assert.AreEqual(TimeSpan.Zero, before);
        Assert.AreEqual(review.Duration, review.Position);
    }

    [TestMethod]
    public void SeekTo_SeparatesHeldKeysFromTheOnesThePedalKeepsSounding()
    {
        // Arrange
        var review = Loaded(new ManualClock(), Fixtures.Pedal());

        // Act
        review.SeekTo(Ms(500));

        // Assert
        CollectionAssert.AreEqual(new[] { "E4" }, review.HeldChips.Select(chip => chip.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "C4" }, review.SustainedChips.Select(chip => chip.Name).ToArray());
        Assert.IsTrue(review.HasPedal);
    }

    [TestMethod]
    public void Play_AdvancesFromTheClockAndShowsTheKeysAtTheNewPosition()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.SeekTo(Ms(100));
        review.Play();

        // Act
        clock.Milliseconds = 250;
        review.Tick();

        // Assert
        Assert.AreEqual(Ms(350), review.Position);
        CollectionAssert.AreEqual(new[] { "C4", "E4" }, review.HeldChips.Select(chip => chip.Name).ToArray());
        Assert.IsTrue(review.IsPlaying);
    }

    [TestMethod]
    public void Play_ResumesWithoutCountingPausedTime()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.SeekTo(Ms(100));
        review.Play();
        clock.Milliseconds = 300;
        review.Pause();
        clock.Milliseconds += 5000;

        // Act
        review.Play();
        clock.Milliseconds += 200;
        review.Tick();

        // Assert
        Assert.AreEqual(Ms(600), review.Position);
    }

    [TestMethod]
    public void Pause_FreezesThePosition()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.Play();
        clock.Milliseconds = 200;
        review.Pause();

        // Act
        clock.Milliseconds = 900;
        review.Tick();

        // Assert
        Assert.AreEqual(Ms(200), review.Position);
        Assert.IsFalse(review.IsPlaying);
    }

    [TestMethod]
    public void Tick_StopsAtTheEndAfterADelayedRefresh()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.SeekTo(Ms(700));
        review.Play();

        // Act
        clock.Milliseconds = 3000;
        review.Tick();

        // Assert
        Assert.AreEqual(review.Duration, review.Position);
        Assert.IsFalse(review.IsPlaying);
    }

    [TestMethod]
    public void Play_AtTheEndStartsOverFromTheBeginning()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.SeekTo(review.Duration);

        // Act
        review.Play();

        // Assert
        Assert.AreEqual(TimeSpan.Zero, review.Position);
        Assert.IsTrue(review.IsPlaying);
    }

    [TestMethod]
    public void SeekTo_DuringPlaybackStopsItAtTheNewPosition()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.Play();
        clock.Milliseconds = 100;
        review.Tick();

        // Act
        review.SeekTo(Ms(800));
        clock.Milliseconds = 400;
        review.Tick();

        // Assert
        Assert.IsFalse(review.IsPlaying);
        Assert.AreEqual(Ms(800), review.Position);
    }

    [TestMethod]
    public void Rewind_StopsPlaybackAndReturnsToZero()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.Play();
        clock.Milliseconds = 300;
        review.Tick();

        // Act
        review.RewindCommand.Execute(null);

        // Assert
        Assert.AreEqual(TimeSpan.Zero, review.Position);
        Assert.IsFalse(review.IsPlaying);
    }

    [TestMethod]
    public void Play_WithoutARecordingDoesNothing()
    {
        // Arrange
        var review = new ReviewState(new ManualClock());

        // Act
        review.Play();

        // Assert
        Assert.IsFalse(review.IsPlaying);
        Assert.IsFalse(review.HasRecording);
    }

    [TestMethod]
    public void PlayPauseText_FollowsPlayback()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        var before = review.PlayPauseText;

        // Act
        review.PlayPauseCommand.Execute(null);

        // Assert
        Assert.AreEqual(Strings.Play, before);
        Assert.AreEqual(Strings.Pause, review.PlayPauseText);
    }

    [TestMethod]
    public void SelectAt_ANoteSeeksToItsExactAttack()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        var transform = review.Transform;
        var x = transform.TimeToX(.2);
        var y = transform.PitchToY(60) + 5;

        // Act
        review.SelectAt(x, y);

        // Assert
        Assert.AreEqual(Ms(100), review.Position);
        Assert.AreEqual(60, review.SelectedNote?.Midi);
        CollectionAssert.AreEqual(new[] { "C4" }, review.HeldChips.Select(chip => chip.Name).ToArray());
    }

    [TestMethod]
    public void SelectAt_EmptySpaceSeeksToTheTimeUnderThePointerAndSelectsNothing()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        review.SelectAt(review.Transform.TimeToX(.2), review.Transform.PitchToY(60) + 5);

        // Act
        review.SelectAt(review.Transform.TimeToX(.65), review.Transform.Top + 3);

        // Assert
        Assert.AreEqual(650, review.Position.TotalMilliseconds, 1);
        Assert.IsNull(review.SelectedNote);
    }

    [TestMethod]
    public void SelectAt_StopsPlayback()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        review.Play();

        // Act
        review.SelectAt(review.Transform.TimeToX(.5), review.Transform.PitchToY(64) + 5);

        // Assert
        Assert.IsFalse(review.IsPlaying);
        Assert.AreEqual(Ms(300), review.Position);
    }

    [TestMethod]
    public void PanBy_FreesTheCameraAndFollowLocksItAgain()
    {
        // Arrange
        var review = Loaded(new ManualClock(), Fixtures.Narrow10s());
        review.ZoomTimeAt(4, 0);
        var followingBefore = review.IsFollowing;

        // Act
        review.PanBy(50, 0);
        var followingAfterPan = review.IsFollowing;
        review.FollowCommand.Execute(null);

        // Assert
        Assert.IsTrue(followingBefore);
        Assert.IsFalse(followingAfterPan);
        Assert.IsTrue(review.IsFollowing);
    }

    [TestMethod]
    public void PanBy_NeverMovesThePlayhead()
    {
        // Arrange
        var review = Loaded(new ManualClock(), Fixtures.Narrow10s());
        review.SeekTo(TimeSpan.FromSeconds(2));
        review.ZoomTimeAt(4, 0);

        // Act
        review.PanBy(200, 30);

        // Assert
        Assert.AreEqual(TimeSpan.FromSeconds(2), review.Position);
    }

    [TestMethod]
    public void ZoomTime_AtTheFitScaleCannotZoomOutAndStopsAtTheMaximum()
    {
        // Arrange
        var review = Loaded(new ManualClock(), Fixtures.Narrow10s());
        var canZoomOutAtFit = review.CanZoomTimeOut;

        // Act
        for (var step = 0; step < 40; step++) review.ZoomTimeInCommand.Execute(null);

        // Assert
        Assert.IsFalse(canZoomOutAtFit);
        Assert.IsTrue(review.CanZoomTimeOut);
        Assert.IsFalse(review.CanZoomTimeIn);
        Assert.AreEqual(TimelineCamera.MaxPxPerSecond, review.Camera.PxPerSecond);
    }

    [TestMethod]
    public void Zoom_NeverChangesThePositionOrTheSelectedNote()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        review.SelectAt(review.Transform.TimeToX(.2), review.Transform.PitchToY(60) + 5);

        // Act
        review.ZoomTimeInCommand.Execute(null);
        review.ZoomPitchInCommand.Execute(null);

        // Assert
        Assert.AreEqual(Ms(100), review.Position);
        Assert.AreEqual(60, review.SelectedNote?.Midi);
    }

    [TestMethod]
    public void Fit_RestoresTheFittedViewAndLocksTheCamera()
    {
        // Arrange
        var review = Loaded(new ManualClock(), Fixtures.Narrow10s());
        var fitted = review.Camera.PxPerSecond;
        review.ZoomTimeInCommand.Execute(null);
        review.PanBy(20, 0);

        // Act
        review.FitCommand.Execute(null);

        // Assert
        Assert.AreEqual(fitted, review.Camera.PxPerSecond);
        Assert.AreEqual(0, review.Camera.TimeOffset);
        Assert.IsTrue(review.IsFollowing);
    }

    [TestMethod]
    public void Fold_GivesALaneOnlyToPitchesThatOccur()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        var linearLanes = review.Pitches.LaneCount;

        // Act
        review.FoldCommand.Execute(null);

        // Assert
        Assert.IsTrue(review.IsFolded);
        Assert.AreEqual(10, linearLanes);
        Assert.AreEqual(3, review.Pitches.LaneCount);
        Assert.AreEqual(Strings.FoldOnHint, review.FoldHint);
    }

    [TestMethod]
    public void ViewChanged_IsRaisedBySeekGridSettingsAndFold()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        var raised = 0;
        review.ViewChanged += (_, _) => raised++;

        // Act
        review.SeekTo(Ms(300));
        var afterSeek = raised;
        review.GridSettings = review.GridSettings with { Subdivision = 16 };
        var afterGrid = raised;
        review.IsFolded = true;

        // Assert
        Assert.IsGreaterThan(0, afterSeek);
        Assert.IsGreaterThan(afterSeek, afterGrid);
        Assert.IsGreaterThan(afterGrid, raised);
    }

    [TestMethod]
    public void GridSettings_AreDisplayOnlyAndNeverTouchTheRecording()
    {
        // Arrange
        var review = Loaded(new ManualClock());
        var recording = review.Recording;

        // Act
        review.GridSettings = new GridSettings(16, false, false);

        // Assert
        Assert.AreSame(recording, review.Recording);
        Assert.IsFalse(review.GridSettings.ShowSubdivisions);
    }

    [TestMethod]
    public void Load_ShowsTheRecordingFittedAtTheStartWithNothingSelected()
    {
        // Arrange
        var clock = new ManualClock();
        var review = Loaded(clock);
        review.SelectAt(review.Transform.TimeToX(.2), review.Transform.PitchToY(60) + 5);
        review.Play();

        // Act
        review.Load(Fixtures.Narrow10s());

        // Assert
        Assert.AreEqual(TimeSpan.Zero, review.Position);
        Assert.IsNull(review.SelectedNote);
        Assert.IsFalse(review.IsPlaying);
        Assert.AreEqual(TimeSpan.FromSeconds(10), review.Duration);
        Assert.IsTrue(review.IsFollowing);
    }

    [TestMethod]
    public void Texts_DescribeTheRecordingForTheHeaderAndTheToolbar()
    {
        // Arrange
        var review = Loaded(new ManualClock());

        // Act
        var summary = review.SummaryText;

        // Assert
        Assert.AreEqual(Display.NoteCount(3), review.NoteCountText);
        Assert.AreEqual(Display.Format(Strings.TimeTotal, Display.Clock(Ms(1000))), review.DurationText);
        Assert.AreEqual(Display.Format(Strings.LastReviewSummary, review.NoteCountText, Display.Clock(Ms(1000))), summary);
        Assert.AreEqual(string.Join(" · ", Display.Format(Strings.BpmValue, 120), Display.Format(Strings.PulseContext, 4)), review.ContextText);
    }
}
