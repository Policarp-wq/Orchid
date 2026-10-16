using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Application;
using Orchid.Core;
using Orchid.Ui.Contracts;
using Orchid.Windows.Backend;

namespace Orchid.Windows.Tests;

[TestClass]
public class KeyPressPairingTests
{
    [TestMethod]
    public void ToNotes_PairsOverlapsAndClosesHeldKeyAtStop()
    {
        // Arrange
        KeyPressTiming[] timings =
        [
            Event(0, KeyInteraction.Press, 100),
            Event(2, KeyInteraction.Press, 300),
            Event(0, KeyInteraction.Release, 400),
            Event(2, KeyInteraction.Release, 600),
            Event(3.5f, KeyInteraction.Press, 900),
        ];

        // Act
        var notes = KeyPressPairing.ToNotes(timings, TimeSpan.Zero, Ms(1000));

        // Assert
        CollectionAssert.AreEqual(new[]
        {
            Note(60, 100, 400, ClosedBy.Release),
            Note(64, 300, 600, ClosedBy.Release),
            Note(67, 900, 1000, ClosedBy.SessionEnd),
        }, notes);
    }

    [TestMethod]
    public void ToNotes_RepeatedPressClosesPreviousAttackAndIgnoresUnmatchedRelease()
    {
        // Arrange
        KeyPressTiming[] timings =
        [
            Event(1, KeyInteraction.Release, 50),
            Event(-.5f, KeyInteraction.Press, 100),
            Event(-.5f, KeyInteraction.Press, 200),
            Event(-.5f, KeyInteraction.Release, 400),
        ];

        // Act
        var notes = KeyPressPairing.ToNotes(timings, TimeSpan.Zero, Ms(500));

        // Assert
        CollectionAssert.AreEqual(new[] { Note(59, 100, 200, ClosedBy.Release), Note(59, 200, 400, ClosedBy.Release) }, notes);
    }

    [TestMethod]
    public void ToNotes_CountInShiftsOffsetsAndKeepsPickupNotesNegative()
    {
        // Arrange
        KeyPressTiming[] timings = [Event(0, KeyInteraction.Press, 1800), Event(0, KeyInteraction.Release, 2300)];

        // Act
        var notes = KeyPressPairing.ToNotes(timings, origin: Ms(2000), duration: Ms(1000));

        // Assert
        CollectionAssert.AreEqual(new[] { Note(60, -200, 300, ClosedBy.Release) }, notes);
    }

    [TestMethod]
    public void ToNotes_EmptySessionHasNoNotes()
    {
        // Arrange

        // Act
        var notes = KeyPressPairing.ToNotes([], TimeSpan.Zero, Ms(12_500));

        // Assert
        Assert.HasCount(0, notes);
    }

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private static NoteSpan Note(int midi, int attack, int release, ClosedBy closedBy) => new(midi, 0, 0, Ms(attack), Ms(release), closedBy);

    private static KeyPressTiming Event(float tone, KeyInteraction interaction, long milliseconds) =>
        new(new KeyPress(Tone.FromNum(tone), interaction), milliseconds);
}
