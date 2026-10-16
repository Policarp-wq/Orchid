using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Tests;

[TestClass]
public class NoteIndexTests
{
    private static readonly SessionSettings Settings = Fixtures.DefaultSettings;

    [TestMethod]
    public void HeldAt_ReleasingOneChannelKeepsTheSamePitchOnAnother()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.SamePitchTwoChannels());

        // Act
        var held = index.HeldAt(TimeSpan.FromMilliseconds(600));

        // Assert
        Assert.HasCount(1, held);
        Assert.AreEqual(1, held[0].Channel);
    }

    [TestMethod]
    public void HeldAt_SeparatesEventsTenMillisecondsApart()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.TwoEvents10MsApart());

        // Act
        var first = index.HeldAt(TimeSpan.FromMilliseconds(105));
        var both = index.HeldAt(TimeSpan.FromMilliseconds(110));

        // Assert
        CollectionAssert.AreEqual(new[] { 60 }, first.Select(note => note.Midi).ToArray());
        CollectionAssert.AreEqual(new[] { 60, 62 }, both.Select(note => note.Midi).ToArray());
    }

    [TestMethod]
    public void HeldAt_ListsLowestPitchFirst()
    {
        // Arrange
        var recording = new RecordingData(Settings, TimeSpan.FromSeconds(1), [Note(72, 0, 500), Note(60, 100, 500), Note(67, 50, 500)], []);
        var index = new NoteIndex(recording);

        // Act
        var held = index.HeldAt(TimeSpan.FromMilliseconds(200));

        // Assert
        CollectionAssert.AreEqual(new[] { 60, 67, 72 }, held.Select(note => note.Midi).ToArray());
    }

    [TestMethod]
    public void HeldAt_PedalKeepsKeyStatePhysical()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Pedal());

        // Act
        var held = index.HeldAt(TimeSpan.FromMilliseconds(500));

        // Assert
        CollectionAssert.AreEqual(new[] { 64 }, held.Select(note => note.Midi).ToArray());
    }

    [TestMethod]
    [DataRow(450, new[] { 60 })]
    [DataRow(700, new[] { 60, 64 })]
    [DataRow(800, new int[] { })]
    public void SustainedAt_ReleasedUnderThePedalSoundsUntilPedalUp(int milliseconds, int[] expected)
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Pedal());

        // Act
        var sustained = index.SustainedAt(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        CollectionAssert.AreEqual(expected, sustained.Select(note => note.Midi).ToArray());
    }

    [TestMethod]
    public void SoundEndOf_SameKeyStruckAgainEndsTheTail()
    {
        // Arrange
        var recording = new RecordingData(Settings, TimeSpan.FromSeconds(2),
            [Note(60, 0, 200), Note(60, 500, 700)], [new PedalSpan(0, TimeSpan.Zero, TimeSpan.FromSeconds(1), ClosedBy.Release)]);
        var index = new NoteIndex(recording);

        // Act
        var firstEnd = index.SoundEndOf(0);
        var secondEnd = index.SoundEndOf(1);

        // Assert
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), firstEnd);
        Assert.AreEqual(TimeSpan.FromSeconds(1), secondEnd);
    }

    [TestMethod]
    public void SoundEndOf_PedalOnAnotherChannelDoesNotSustain()
    {
        // Arrange
        var recording = new RecordingData(Settings, TimeSpan.FromSeconds(2),
            [Note(60, 0, 200)], [new PedalSpan(1, TimeSpan.Zero, TimeSpan.FromSeconds(1), ClosedBy.Release)]);
        var index = new NoteIndex(recording);

        // Act
        var end = index.SoundEndOf(0);

        // Assert
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), end);
    }

    [TestMethod]
    public void PedalDownAt_FollowsThePedalSpan()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Pedal());

        // Act
        var before = index.PedalDownAt(TimeSpan.FromMilliseconds(349));
        var during = index.PedalDownAt(TimeSpan.FromMilliseconds(350));
        var after = index.PedalDownAt(TimeSpan.FromMilliseconds(800));

        // Assert
        Assert.IsFalse(before);
        Assert.IsTrue(during);
        Assert.IsFalse(after);
    }

    [TestMethod]
    public void Visible_OnOneHourReturnsOnlyTheNotesOfTheWindow()
    {
        // Arrange
        var recording = Fixtures.OneHour();
        var index = new NoteIndex(recording);
        var from = TimeSpan.FromMinutes(30);
        var to = from + TimeSpan.FromSeconds(10);

        // Act
        var visible = index.Visible(from, to).Select(i => index.Notes[i]).ToArray();

        // Assert
        Assert.IsTrue(visible.All(note => note.Attack <= to && note.Release > from));
        Assert.AreEqual(recording.Notes.Count(note => note.Attack <= to && note.Release > from), visible.Length);
        Assert.IsLessThan(200, visible.Length);
    }

    [TestMethod]
    public void Empty_HasNoRangeAndNothingHeld()
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Empty());

        // Act
        var held = index.HeldAt(TimeSpan.FromSeconds(1));

        // Assert
        Assert.IsNull(index.Lowest);
        Assert.IsNull(index.Highest);
        Assert.HasCount(0, held);
    }

    private static NoteSpan Note(int midi, int attackMs, int releaseMs) =>
        new(midi, 0, 80, TimeSpan.FromMilliseconds(attackMs), TimeSpan.FromMilliseconds(releaseMs), ClosedBy.Release);
}
