using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.UiPreview;

namespace Orchid.UiPreview.Tests;

[TestClass]
public class PreviewSessionTests
{
    [TestMethod]
    [DataRow(0.0, new int[] { })]
    [DataRow(0.012, new int[] { 60 })]
    [DataRow(0.4, new int[] { })]
    [DataRow(1.6, new int[] { 66 })]
    [DataRow(3.5, new int[] { 71, 72 })]
    [DataRow(8.0, new int[] { })]
    public void Seek_ReflectsHeldKeys(double position, int[] expected)
    {
        // Arrange
        var session = new PreviewSession(new ManualClock());

        // Act
        session.Seek(position);

        // Assert
        CollectionAssert.AreEqual(expected, session.HeldNotes.Select(note => note.Midi).ToArray());
    }

    [TestMethod]
    public void Seek_ExactReleaseClearsKey()
    {
        // Arrange
        var session = new PreviewSession(new ManualClock());
        var note = session.Notes[0];

        // Act
        session.Seek(note.Release);

        // Assert
        Assert.IsFalse(session.HeldNotes.Contains(note));
    }

    [TestMethod]
    public void Play_ResumesWithoutCountingPausedTime()
    {
        // Arrange
        var clock = new ManualClock();
        var session = new PreviewSession(clock);
        session.Seek(1);
        session.Play();
        clock.Milliseconds = 750;
        session.Pause();
        clock.Milliseconds += 5000;

        // Act
        session.Play();
        clock.Milliseconds += 250;
        session.Tick();

        // Assert
        Assert.AreEqual(2.0, session.Position);
    }

    [TestMethod]
    public void Tick_StopsAtEndAfterDelayedRefresh()
    {
        // Arrange
        var clock = new ManualClock();
        var session = new PreviewSession(clock);
        session.Seek(7);
        session.Play();

        // Act
        clock.Milliseconds = 3000;
        session.Tick();

        // Assert
        Assert.AreEqual(8.0, session.Position);
        Assert.IsFalse(session.IsPlaying);
    }

    [TestMethod]
    [DataRow(1, .125)]
    [DataRow(2, .25)]
    [DataRow(4, .5)]
    [DataRow(8, 1.0)]
    [DataRow(16, 2.0)]
    public void Pulse_DefinesQuarterDuration(int denominator, double expectedSeconds)
    {
        // Arrange
        var session = new PreviewSession(new ManualClock()) { Bpm = 120, PulseDenominator = denominator };

        // Act
        var actualSeconds = session.QuarterSeconds;

        // Assert
        Assert.AreEqual(expectedSeconds, actualSeconds);
    }

    private sealed class ManualClock : TimeProvider
    {
        public long Milliseconds { get; set; }
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Milliseconds;
    }
}
