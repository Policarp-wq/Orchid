using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Core;
using Orchid.UiPreview;

namespace Orchid.UiPreview.Tests;

[TestClass]
public class LiveNoteTests
{
    [TestMethod]
    public void ReceiveLiveNote_PressShowsPitchAndReleaseClearsIt()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        session.BeginRecording();

        // Act
        session.ReceiveLiveNote(66, KeyInteraction.Press, .1);
        var held = session.HeldNotes.Select(note => note.Name).ToArray();
        session.ReceiveLiveNote(66, KeyInteraction.Release, .4);

        // Assert
        CollectionAssert.AreEqual(new[] { "F♯4" }, held);
        Assert.AreEqual(0, session.HeldNotes.Count());
    }

    [TestMethod]
    public void ReceiveLiveNote_ReleasingAnotherPitchKeepsHeldPitch()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        session.BeginRecording();
        session.ReceiveLiveNote(60, KeyInteraction.Press, .1);
        session.ReceiveLiveNote(64, KeyInteraction.Press, .2);

        // Act
        session.ReceiveLiveNote(60, KeyInteraction.Release, .3);

        // Assert
        CollectionAssert.AreEqual(new[] { 64 }, session.HeldNotes.Select(note => note.Midi).ToArray());
    }

    [TestMethod]
    public void LoadRecording_DropsLiveStateAndIgnoresLateNotification()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);
        session.BeginRecording();
        session.ReceiveLiveNote(60, KeyInteraction.Press, .1);

        // Act
        session.LoadRecording([], 1);
        session.ReceiveLiveNote(64, KeyInteraction.Press, 1.1);

        // Assert
        Assert.AreEqual(0, session.HeldNotes.Count());
        Assert.AreEqual(1, session.Duration);
        Assert.AreEqual(0, session.Position);
    }
}
