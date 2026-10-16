using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Ui.Contracts;
using Orchid.Ui.Fakes;
using Orchid.Ui.Timeline;

namespace Orchid.Ui.Tests;

[TestClass]
public class FixtureTests
{
    [TestMethod]
    [DataRow(0, new int[] { })]
    [DataRow(100, new[] { 60 })]
    [DataRow(300, new[] { 60, 64 })]
    [DataRow(400, new[] { 64 })]
    [DataRow(600, new int[] { })]
    [DataRow(900, new[] { 67 })]
    [DataRow(1000, new int[] { })]
    public void Shared_HeldAtMatchesTheAcceptanceTable(int milliseconds, int[] expected)
    {
        // Arrange
        var index = new NoteIndex(Fixtures.Shared());

        // Act
        var held = index.HeldAt(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        CollectionAssert.AreEqual(expected, held.Select(note => note.Midi).ToArray());
    }

    [TestMethod]
    public void Shared_KeepsTheSynthesizedClosureAtStop()
    {
        // Arrange
        var recording = Fixtures.Shared();

        // Act
        var last = recording.Notes[^1];

        // Assert
        Assert.AreEqual(ClosedBy.SessionEnd, last.ClosedBy);
        Assert.AreEqual(recording.Duration, last.Release);
    }

    [TestMethod]
    public void FiveMinutes_HasTwoThousandNotesOverThreeOctavesWithOverlaps()
    {
        // Arrange
        var recording = Fixtures.FiveMinutes();

        // Act
        var index = new NoteIndex(recording);
        var overlaps = recording.Notes.Zip(recording.Notes.Skip(1)).Count(pair => pair.Second.Attack < pair.First.Release);

        // Assert
        Assert.AreEqual(TimeSpan.FromMinutes(5), recording.Duration);
        Assert.IsGreaterThanOrEqualTo(2000, recording.Notes.Count);
        Assert.IsGreaterThanOrEqualTo(36, index.Highest!.Value - index.Lowest!.Value);
        Assert.IsGreaterThan(0, overlaps);
    }

    [TestMethod]
    public void OneHour_HasTwentyThousandNotesAndMinuteLongGaps()
    {
        // Arrange
        var recording = Fixtures.OneHour();

        // Act
        TimeSpan latestRelease = TimeSpan.Zero, longestGap = TimeSpan.Zero;
        foreach (var note in recording.Notes)
        {
            if (note.Attack - latestRelease > longestGap) longestGap = note.Attack - latestRelease;
            if (note.Release > latestRelease) latestRelease = note.Release;
        }

        // Assert
        Assert.AreEqual(TimeSpan.FromHours(1), recording.Duration);
        Assert.IsGreaterThanOrEqualTo(20_000, recording.Notes.Count);
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMinutes(1), longestGap);
    }

    [TestMethod]
    public void WideA0ToC8_SpansThePianoRangeOverFiveMinutes()
    {
        // Arrange
        var recording = Fixtures.WideA0ToC8();

        // Act
        var index = new NoteIndex(recording);

        // Assert
        Assert.AreEqual(21, index.Lowest);
        Assert.AreEqual(108, index.Highest);
        Assert.AreEqual(TimeSpan.FromMinutes(5), recording.Duration);
    }

    [TestMethod]
    public void Narrow10s_StaysInsideOneOctave()
    {
        // Arrange
        var recording = Fixtures.Narrow10s();

        // Act
        var index = new NoteIndex(recording);

        // Assert
        Assert.AreEqual(TimeSpan.FromSeconds(10), recording.Duration);
        Assert.IsLessThan(12, index.Highest!.Value - index.Lowest!.Value);
    }

    [TestMethod]
    [DataRow(500, 0)]
    [DataRow(1500, 1)]
    [DataRow(3500, 3)]
    [DataRow(5500, 6)]
    [DataRow(7500, 10)]
    public void ManyHeld_HoldsTheStatedKeyCounts(int milliseconds, int expected)
    {
        // Arrange
        var index = new NoteIndex(Fixtures.ManyHeld());

        // Act
        var held = index.HeldAt(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        Assert.HasCount(expected, held);
    }

    [TestMethod]
    public void Generated_FixturesAreDeterministic()
    {
        // Arrange
        var first = Fixtures.FiveMinutes();

        // Act
        var second = Fixtures.FiveMinutes();

        // Assert
        CollectionAssert.AreEqual(first.Notes.ToArray(), second.Notes.ToArray());
    }

    [TestMethod]
    public void ByName_KnowsEveryListedFixture()
    {
        // Arrange
        var names = Fixtures.Names;

        // Act
        var recordings = names.Select(Fixtures.ByName).ToArray();

        // Assert
        Assert.HasCount(names.Count, recordings);
        Assert.AreEqual(TimeSpan.FromSeconds(600), Fixtures.ByName(nameof(Fixtures.SurfaceLimit600s)).Duration);
    }
}
