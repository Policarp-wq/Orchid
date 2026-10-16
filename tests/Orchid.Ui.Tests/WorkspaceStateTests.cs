using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.State;
using Orchid.Ui.Theme;

namespace Orchid.Ui.Tests;

[TestClass]
public class WorkspaceStateTests
{
    [TestMethod]
    public async Task Initialize_SelectsTheFirstInputAndStartsInReady()
    {
        // Arrange

        // Act
        var rig = await Rig.CreateAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.Ready, rig.State.Mode);
        Assert.AreEqual("Piano", rig.State.Setup.SelectedInput?.Name);
        Assert.IsTrue(rig.State.Setup.CanStart);
    }

    [TestMethod]
    public async Task Start_WithoutAnInputStaysInReady()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Catalog.Inputs.Clear();
        await rig.State.Setup.RefreshAsync(rig.Catalog, CancellationToken.None);

        // Act
        await rig.State.StartAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.Ready, rig.State.Mode);
        Assert.IsTrue(rig.State.Setup.HasNoInputs);
    }

    [TestMethod]
    public async Task Start_ThroughCountInAndRecordingToReview()
    {
        // Arrange
        var rig = await Rig.CreateAsync();

        // Act
        await rig.State.StartAsync();
        var afterStart = rig.State.Mode;
        rig.Clock.Milliseconds = 2000;
        rig.State.Tick();
        var afterCountIn = rig.State.Mode;
        rig.Clock.Milliseconds = 2500;
        rig.State.Tick();
        await rig.State.StopAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.CountIn, afterStart);
        Assert.AreEqual(WorkspaceMode.Recording, afterCountIn);
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
        Assert.IsTrue(rig.State.Review.HasRecording);
    }

    [TestMethod]
    public async Task Start_WithoutCountInRecordsImmediately()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.State.Setup.CountInBars = 0;

        // Act
        await rig.State.StartAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.Recording, rig.State.Mode);
    }

    [TestMethod]
    public async Task Stop_DuringCountInReturnsToReadyWithoutAReview()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        await rig.State.StartAsync();
        rig.Clock.Milliseconds = 500;
        rig.State.Tick();

        // Act
        await rig.State.StopAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.Ready, rig.State.Mode);
        Assert.IsFalse(rig.State.Review.HasRecording);
        Assert.IsTrue(rig.State.Setup.IsUnlocked);
        Assert.HasCount(0, rig.Store.Files);
    }

    [TestMethod]
    public async Task Stop_WhileRecordingOpensTheReviewAndSavesIt()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.State.Setup.CountInBars = 0;
        await rig.State.StartAsync();
        rig.Clock.Milliseconds = 350;
        rig.State.Tick();

        // Act
        await rig.State.StopAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
        Assert.AreEqual(TimeSpan.FromMilliseconds(350), rig.State.Review.Duration);
        Assert.AreEqual(SaveState.Saved, rig.State.Status.SaveState);
        Assert.AreEqual("recording-1.orchid", rig.State.Status.FileName);
        Assert.AreEqual(TimeSpan.Zero, rig.State.Review.Position);
    }

    [TestMethod]
    public async Task Stop_ClosesAKeyThatIsStillHeldAtTheSessionEnd()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.State.Setup.CountInBars = 0;
        await rig.State.StartAsync();
        rig.Clock.Milliseconds = 500;

        // Act
        await rig.State.StopAsync();

        // Assert
        var held = rig.State.Review.Recording.Notes.Single(note => note.Midi == 64);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), held.Release);
        Assert.AreEqual(ClosedBy.SessionEnd, held.ClosedBy);
    }

    [TestMethod]
    public async Task Settings_AreIgnoredWhileASessionRuns()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        var setup = rig.State.Setup;
        var strongBeats = setup.StrongBeats;
        await rig.State.StartAsync();

        // Act
        setup.Bpm = 90;
        setup.PulseDenominator = 8;
        setup.CountInBars = 2;
        setup.SelectedInput = setup.Inputs[1];
        setup.StrongBeats = [true, true, true, true];

        // Assert
        Assert.AreEqual(120, setup.Bpm);
        Assert.AreEqual(4, setup.PulseDenominator);
        Assert.AreEqual(1, setup.CountInBars);
        Assert.AreEqual("Piano", setup.SelectedInput?.Name);
        CollectionAssert.AreEqual(strongBeats.ToArray(), setup.StrongBeats.ToArray());
        Assert.IsFalse(setup.CanStart);
    }

    [TestMethod]
    public async Task Settings_UnlockAfterStop()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        await rig.State.StartAsync();
        await rig.State.StopAsync();

        // Act
        rig.State.Setup.Bpm = 90;

        // Assert
        Assert.AreEqual(90, rig.State.Setup.Bpm);
    }

    [TestMethod]
    public async Task Start_UsesTheSettingsOfSetup()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.State.Setup.Bpm = 90;
        rig.State.Setup.PulseDenominator = 8;
        rig.State.Setup.StrongBeats = [true, false, false, false, true, false, false, false];
        rig.State.Setup.CountInBars = 0;
        await rig.State.StartAsync();
        rig.Clock.Milliseconds = 100;

        // Act
        await rig.State.StopAsync();

        // Assert
        var settings = rig.State.Review.Recording.Settings;
        Assert.AreEqual(90, settings.Bpm);
        Assert.AreEqual(8, settings.PulseDenominator);
        Assert.AreEqual(0, settings.CountInBars);
        Assert.AreEqual(8, settings.StrongBeats.Count);
    }

    [TestMethod]
    public async Task Start_FailureShowsTheProblemAndUnlocksSettings()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Recorder.StartFailure = ProblemCode.MidiUnavailable;

        // Act
        await rig.State.StartAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.Ready, rig.State.Mode);
        Assert.AreEqual(Strings.ProblemMidiUnavailable, rig.State.Status.BannerText);
        Assert.IsTrue(rig.State.Setup.IsUnlocked);
    }

    [TestMethod]
    public async Task Save_FailureKeepsTheRecordingAndOffersRetry()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Store.SaveFailure = ProblemCode.SaveFailed;
        rig.State.Setup.CountInBars = 0;
        await rig.State.StartAsync();
        rig.Clock.Milliseconds = 300;

        // Act
        await rig.State.StopAsync();

        // Assert
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
        Assert.IsTrue(rig.State.Review.HasRecording);
        Assert.AreEqual(SaveState.Failed, rig.State.Status.SaveState);
        Assert.AreEqual(Strings.ProblemSaveFailed, rig.State.Status.BannerText);
        Assert.AreEqual(Strings.Retry, rig.State.Status.BannerAction);
    }

    [TestMethod]
    public async Task Save_DiskLowAlsoOffersRetry()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Store.SaveFailure = ProblemCode.DiskLow;
        await rig.OpenAsync(Fixtures.Shared());

        // Act
        await rig.State.SaveAsync();

        // Assert
        Assert.AreEqual(Strings.ProblemDiskLow, rig.State.Status.BannerText);
        Assert.AreEqual(Strings.Retry, rig.State.Status.BannerAction);
    }

    [TestMethod]
    public async Task Save_RetrySucceedsAndClearsTheBanner()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Store.SaveFailure = ProblemCode.SaveFailed;
        await rig.OpenAsync(Fixtures.Shared());
        await rig.State.SaveAsync();
        rig.Store.SaveFailure = null;

        // Act
        rig.State.BannerActionCommand.Execute(null);

        // Assert
        Assert.AreEqual(SaveState.Saved, rig.State.Status.SaveState);
        Assert.IsFalse(rig.State.Status.HasBanner);
        Assert.AreEqual("recording-1.orchid", rig.State.Status.FileName);
    }

    [TestMethod]
    public async Task Banner_DismissClearsAProblemThatIsNotASaveFailure()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Recorder.StartFailure = ProblemCode.AudioFailed;
        await rig.State.StartAsync();

        // Act
        rig.State.BannerActionCommand.Execute(null);

        // Assert
        Assert.IsFalse(rig.State.Status.HasBanner);
    }

    [TestMethod]
    public async Task Open_MissingFileShowsABannerAndStaysInReady()
    {
        // Arrange
        var rig = await Rig.CreateAsync();

        // Act
        await rig.State.OpenAsync("nowhere");

        // Assert
        Assert.AreEqual(WorkspaceMode.Ready, rig.State.Mode);
        Assert.AreEqual(Strings.LoadMissing, rig.State.Status.BannerText);
    }

    [TestMethod]
    public async Task Open_InvalidAndUnsupportedFilesShowTheirOwnMessages()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Store.Files["bad"] = new LoadResult.Invalid();
        rig.Store.Files["new"] = new LoadResult.UnsupportedVersion();

        // Act
        await rig.State.OpenAsync("bad");
        var invalid = rig.State.Status.BannerText;
        await rig.State.OpenAsync("new");
        var unsupported = rig.State.Status.BannerText;

        // Assert
        Assert.AreEqual(Strings.LoadInvalid, invalid);
        Assert.AreEqual(Strings.LoadUnsupported, unsupported);
        Assert.IsFalse(rig.State.Review.HasRecording);
    }

    [TestMethod]
    public async Task Open_ShowsTheFileInReviewAtTheStart()
    {
        // Arrange
        var rig = await Rig.CreateAsync();

        // Act
        await rig.OpenAsync(Fixtures.Shared());

        // Assert
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
        Assert.AreEqual("opened", rig.State.Status.FileName);
        Assert.AreEqual(SaveState.Saved, rig.State.Status.SaveState);
        Assert.AreEqual(TimeSpan.Zero, rig.State.Review.Position);
        Assert.IsFalse(rig.State.Status.HasBanner);
    }

    [TestMethod]
    public async Task Open_AnEmptyRecordingShowsTheEmptyState()
    {
        // Arrange
        var rig = await Rig.CreateAsync();

        // Act
        await rig.OpenAsync(Fixtures.Empty());

        // Assert
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
        Assert.IsTrue(rig.State.Review.IsEmpty);
        Assert.AreEqual(TimeSpan.FromSeconds(4), rig.State.Review.Duration);
        Assert.HasCount(0, rig.State.Review.HeldChips);
        Assert.AreEqual(Strings.HeldNone, rig.State.Review.HeldSummaryText);
    }

    [TestMethod]
    public async Task Recovery_IsOfferedAtStartAndOpensInReview()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Store.Unfinished = Fixtures.Shared();
        await rig.State.InitializeAsync(CancellationToken.None);

        // Act
        var offered = rig.State.Status.HasRecovery;
        await rig.State.RecoverAsync();

        // Assert
        Assert.IsTrue(offered);
        Assert.IsFalse(rig.State.Status.HasRecovery);
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
        Assert.IsTrue(rig.State.Review.HasRecording);
    }

    [TestMethod]
    public async Task Recovery_CanBeDiscarded()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Store.Unfinished = Fixtures.Shared();
        await rig.State.InitializeAsync(CancellationToken.None);

        // Act
        rig.State.DiscardRecoveryCommand.Execute(null);

        // Assert
        Assert.IsFalse(rig.State.Status.HasRecovery);
        Assert.AreEqual(WorkspaceMode.Ready, rig.State.Mode);
    }

    [TestMethod]
    public async Task IsTicking_OnlyWhileLiveOrPlaying()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        var states = new List<bool> { rig.State.IsTicking };

        // Act
        await rig.State.StartAsync();
        states.Add(rig.State.IsTicking);
        rig.Clock.Milliseconds = 2500;
        rig.State.Tick();
        await rig.State.StopAsync();
        states.Add(rig.State.IsTicking);
        rig.State.Review.Play();
        states.Add(rig.State.IsTicking);
        rig.State.Review.Pause();
        states.Add(rig.State.IsTicking);

        // Assert
        CollectionAssert.AreEqual(new[] { false, true, false, true, false }, states);
    }

    [TestMethod]
    public async Task IsTicking_RaisesPropertyChangedSoTheHostCanStartItsTimer()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        var raised = new List<string?>();
        rig.State.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        // Act
        await rig.State.StartAsync();

        // Assert
        CollectionAssert.Contains(raised, nameof(WorkspaceState.IsTicking));
    }

    [TestMethod]
    public async Task Disconnect_StopsIntoReviewWithTheBannerAndKeepsWhatWasCaptured()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        rig.Recorder.DisconnectAt = TimeSpan.FromMilliseconds(500);
        rig.State.Setup.CountInBars = 0;
        await rig.State.StartAsync();

        // Act
        rig.Clock.Milliseconds = 700;
        rig.State.Tick();

        // Assert
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
        Assert.AreEqual(Strings.ProblemMidiDisconnected, rig.State.Status.BannerText);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), rig.State.Review.Duration);
        Assert.AreEqual(ClosedBy.Disconnect, rig.State.Review.Recording.Notes.Single(note => note.Midi == 64).ClosedBy);
    }

    [TestMethod]
    public async Task Live_ShowsHeldKeysLowestFirstAndThePedal()
    {
        // Arrange
        var rig = await Rig.CreateAsync(Fixtures.Pedal());
        rig.State.Setup.CountInBars = 0;
        await rig.State.StartAsync();

        // Act
        rig.Clock.Milliseconds = 350;
        rig.State.Tick();

        // Assert
        CollectionAssert.AreEqual(new[] { "C4", "E4" }, rig.State.Live.HeldChips.Select(chip => chip.Name).ToArray());
        Assert.IsTrue(rig.State.Live.PedalDown);
        Assert.AreEqual(Strings.PedalDown, rig.State.Live.PedalText);
    }

    [TestMethod]
    public async Task Live_CountInShowsTheCountAndRecordingShowsBarAndBeat()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        await rig.State.StartAsync();

        // Act
        rig.Clock.Milliseconds = 600;
        rig.State.Tick();
        var countIn = (rig.State.Live.IsCountIn, rig.State.Live.CountText, rig.State.Live.CurrentBeat);
        rig.Clock.Milliseconds = 2000 + 1100;
        rig.State.Tick();

        // Assert
        Assert.AreEqual((true, "2", 1), countIn);
        Assert.IsFalse(rig.State.Live.IsCountIn);
        Assert.AreEqual(Display.Format(Strings.LiveBar, 1), rig.State.Live.BarText);
        Assert.AreEqual(Display.Format(Strings.LiveBeat, 3, 4), rig.State.Live.BeatText);
    }

    [TestMethod]
    public async Task NewSession_ReturnsToReadyAndKeepsTheLastReview()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        await rig.OpenAsync(Fixtures.Shared());

        // Act
        rig.State.NewSessionCommand.Execute(null);
        var inReady = rig.State.Mode;
        rig.State.ShowReviewCommand.Execute(null);

        // Assert
        Assert.AreEqual(WorkspaceMode.Ready, inReady);
        Assert.AreEqual(WorkspaceMode.Review, rig.State.Mode);
    }

    [TestMethod]
    public async Task Review_IsHiddenDuringARecordingAndNothingElseOpensIt()
    {
        // Arrange
        var rig = await Rig.CreateAsync();
        await rig.OpenAsync(Fixtures.Shared());
        rig.State.NewSessionCommand.Execute(null);
        await rig.State.StartAsync();

        // Act
        rig.State.ShowReviewCommand.Execute(null);
        await rig.State.OpenAsync("opened");

        // Assert
        Assert.IsTrue(rig.State.IsLive);
        Assert.IsFalse(rig.State.IsReview);
    }
}
