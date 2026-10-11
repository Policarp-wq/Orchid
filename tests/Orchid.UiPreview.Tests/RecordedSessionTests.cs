using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Application;
using Orchid.Core;
using Orchid.UiPreview;

namespace Orchid.UiPreview.Tests;

[TestClass]
public class RecordedSessionTests
{
    [TestMethod]
    public void LoadRecording_PairsOverlapsAndClosesHeldKeyAtStop()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        KeyPressTiming[] timings =
        [
            Event(0, KeyInteraction.Press, 100),
            Event(2, KeyInteraction.Press, 300),
            Event(0, KeyInteraction.Release, 400),
            Event(2, KeyInteraction.Release, 600),
            Event(3.5f, KeyInteraction.Press, 900)
        ];

        // Act
        session.LoadRecording(timings, 1);
        session.Seek(.3);

        // Assert
        CollectionAssert.AreEqual(new[]
        {
            new PreviewNote(60, .1, .4),
            new PreviewNote(64, .3, .6),
            new PreviewNote(67, .9, 1)
        }, session.Notes.ToArray());
        CollectionAssert.AreEqual(new[] { 60, 64 }, session.HeldNotes.Select(note => note.Midi).ToArray());
        Assert.AreEqual(1, session.Duration);
    }

    [TestMethod]
    public void LoadRecording_EmptySessionClearsDemoAndRetainsSilence()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        session.Play();

        // Act
        session.LoadRecording([], 12.5);

        // Assert
        Assert.AreEqual(0, session.Notes.Count);
        Assert.AreEqual(12.5, session.Duration);
        Assert.AreEqual(0, session.Position);
        Assert.IsFalse(session.IsPlaying);
    }

    [TestMethod]
    public void LoadRecording_RepeatedPressClosesPreviousAttackAndIgnoresUnmatchedRelease()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        KeyPressTiming[] timings =
        [
            Event(1, KeyInteraction.Release, 50),
            Event(-.5f, KeyInteraction.Press, 100),
            Event(-.5f, KeyInteraction.Press, 200),
            Event(-.5f, KeyInteraction.Release, 400)
        ];

        // Act
        session.LoadRecording(timings, .5);

        // Assert
        CollectionAssert.AreEqual(new[]
        {
            new PreviewNote(59, .1, .2),
            new PreviewNote(59, .2, .4)
        }, session.Notes.ToArray());
    }

    private static KeyPressTiming Event(float tone, KeyInteraction interaction, long milliseconds) =>
        new(new KeyPress(Tone.FromNum(tone), interaction), milliseconds);
}
