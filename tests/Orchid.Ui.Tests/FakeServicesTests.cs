using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;

namespace Orchid.Ui.Tests;

[TestClass]
public class FakeServicesTests
{
    private static readonly MidiInputInfo Input = new("fake", "Fake piano");

    [TestMethod]
    public async Task ScriptedRecorder_CountsInBeforeBarOne()
    {
        // Arrange
        var clock = new ManualClock();
        var recorder = new ScriptedRecorder(clock, Fixtures.Shared());
        await using var session = await recorder.StartAsync(Input, Fixtures.DefaultSettings, CancellationToken.None);

        // Act
        clock.Milliseconds = 600;
        var countIn = session.Read();
        clock.Milliseconds = 2000;
        var barOne = session.Read();

        // Assert
        Assert.AreEqual(LivePhase.CountIn, countIn.Phase);
        Assert.AreEqual(0, countIn.Bar);
        Assert.AreEqual(2, countIn.Beat);
        Assert.AreEqual(LivePhase.Recording, barOne.Phase);
        Assert.AreEqual((1, 1), (barOne.Bar, barOne.Beat));
    }

    [TestMethod]
    public async Task ScriptedRecorder_ReportsKeysHeldAtTheElapsedTime()
    {
        // Arrange
        var clock = new ManualClock();
        var settings = Fixtures.DefaultSettings with { CountInBars = 0 };
        await using var session = await new ScriptedRecorder(clock, Fixtures.Pedal()).StartAsync(Input, settings, CancellationToken.None);

        // Act
        clock.Milliseconds = 350;
        var snapshot = session.Read();

        // Assert
        CollectionAssert.AreEqual(new[] { 60, 64 }, snapshot.Held.Select(key => key.Midi).ToArray());
        Assert.IsTrue(snapshot.PedalDown);
        Assert.AreEqual(TimeSpan.FromMilliseconds(350), snapshot.Elapsed);
    }

    [TestMethod]
    public async Task ScriptedRecorder_StopClosesHeldNotesAtTheSessionEnd()
    {
        // Arrange
        var clock = new ManualClock();
        var settings = Fixtures.DefaultSettings with { CountInBars = 0 };
        await using var session = await new ScriptedRecorder(clock, Fixtures.Shared()).StartAsync(Input, settings, CancellationToken.None);
        clock.Milliseconds = 350;

        // Act
        var recording = await session.StopAsync();
        clock.Milliseconds = 900;
        var again = await session.StopAsync();

        // Assert
        Assert.AreEqual(TimeSpan.FromMilliseconds(350), recording.Duration);
        Assert.HasCount(2, recording.Notes);
        Assert.IsTrue(recording.Notes.All(note => note.Release == recording.Duration && note.ClosedBy == ClosedBy.SessionEnd));
        Assert.AreSame(recording, again);
    }

    [TestMethod]
    public async Task ScriptedRecorder_DisconnectEndsTheSessionWithAProblem()
    {
        // Arrange
        var clock = new ManualClock();
        var settings = Fixtures.DefaultSettings with { CountInBars = 0 };
        var recorder = new ScriptedRecorder(clock, Fixtures.Shared()) { DisconnectAt = TimeSpan.FromMilliseconds(200) };
        await using var session = await recorder.StartAsync(Input, settings, CancellationToken.None);
        clock.Milliseconds = 500;

        // Act
        var snapshot = session.Read();
        var recording = await session.StopAsync();

        // Assert
        Assert.AreEqual(LivePhase.Ended, snapshot.Phase);
        Assert.AreEqual(ProblemCode.MidiDisconnected, snapshot.Problem?.Code);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), recording.Duration);
        Assert.AreEqual(ClosedBy.Disconnect, recording.Notes.Single().ClosedBy);
    }

    [TestMethod]
    public async Task ScriptedRecorder_StartFailureCarriesTheCode()
    {
        // Arrange
        var recorder = new ScriptedRecorder(new ManualClock(), Fixtures.Shared()) { StartFailure = ProblemCode.AudioFailed };

        // Act
        var failure = await Assert.ThrowsExactlyAsync<SessionStartException>(
            () => recorder.StartAsync(Input, Fixtures.DefaultSettings, CancellationToken.None));

        // Assert
        Assert.AreEqual(ProblemCode.AudioFailed, failure.Code);
    }

    [TestMethod]
    public async Task InMemoryStore_LoadsWhatWasSaved()
    {
        // Arrange
        var store = new InMemoryRecordingStore();
        var recording = Fixtures.Shared();

        // Act
        var saved = (SaveResult.Saved)await store.SaveAsync(recording, CancellationToken.None);
        var loaded = await store.LoadAsync(saved.Path, CancellationToken.None);

        // Assert
        Assert.AreSame(recording, ((LoadResult.Loaded)loaded).Data);
    }

    [TestMethod]
    public async Task InMemoryStore_ReportsSaveFailure()
    {
        // Arrange
        var store = new InMemoryRecordingStore { SaveFailure = ProblemCode.DiskLow };

        // Act
        var result = await store.SaveAsync(Fixtures.Shared(), CancellationToken.None);

        // Assert
        Assert.AreEqual(new SaveResult.Failed(ProblemCode.DiskLow), result);
    }

    [TestMethod]
    public async Task InMemoryStore_UnknownPathIsMissing()
    {
        // Arrange
        var store = new InMemoryRecordingStore();

        // Act
        var result = await store.LoadAsync("nowhere.orchid", CancellationToken.None);

        // Assert
        Assert.IsInstanceOfType<LoadResult.Missing>(result);
    }

    [TestMethod]
    public async Task FakeCatalog_RefreshSeesAddedAndRemovedInputs()
    {
        // Arrange
        var catalog = new FakeMidiInputCatalog { Inputs = { Input } };
        var before = await catalog.GetInputsAsync(CancellationToken.None);

        // Act
        catalog.Inputs.Clear();
        var after = await catalog.GetInputsAsync(CancellationToken.None);

        // Assert
        Assert.HasCount(1, before);
        Assert.HasCount(0, after);
    }
}
