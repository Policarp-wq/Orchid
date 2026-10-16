using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.UiPreview;

namespace Orchid.UiPreview.Tests;

[TestClass]
public class StrongBeatsTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    public void DefaultStrongBeats_OnlyFirstBeatOfTheMeasureIsStrong(int pulses)
    {
        // Arrange
        var expected = Enumerable.Range(0, pulses).Select(pulse => pulse == 0).ToArray();

        // Act
        var actual = PreviewSession.DefaultStrongBeats(pulses);

        // Assert
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void NewSession_HasFourPulseDefaultPattern()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System);

        // Act
        var strongBeats = session.StrongBeats;

        // Assert
        CollectionAssert.AreEqual(new[] { true, false, false, false }, strongBeats.ToArray());
        Assert.AreEqual(session.PulseDenominator, strongBeats.Count);
    }

    [TestMethod]
    public void WithBeat_ChangesOnlyThatBeatAndLeavesTheSourceUntouched()
    {
        // Arrange
        var source = new[] { true, false, false, false, false, false };

        // Act
        var changed = PreviewSession.WithBeat(source, 3, true);

        // Assert
        CollectionAssert.AreEqual(new[] { true, false, false, true, false, false }, changed);
        CollectionAssert.AreEqual(new[] { true, false, false, false, false, false }, source);
    }

    [TestMethod]
    [DataRow("....", "No strong beats")]
    [DataRow("S...", "Beat 1 of 4")]
    [DataRow("S.S.", "Beats 1, 3 of 4")]
    [DataRow("SSSS", "Every beat is strong (4)")]
    [DataRow("..S.....", "Beat 3 of 8")]
    [DataRow("S..S..", "Beats 1, 4 of 6")]
    [DataRow("S..", "Beat 1 of 3")]
    public void DescribeStrongBeats_NamesTheStrongBeatsOfAnyMeter(string pattern, string expected)
    {
        // Arrange
        var beats = pattern.Select(flag => flag == 'S').ToArray();

        // Act
        var actual = PreviewSession.DescribeStrongBeats(beats);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void LoadRecording_KeepsTheConfiguredPattern()
    {
        // Arrange
        var session = new PreviewSession(TimeProvider.System) { PulseDenominator = 4, StrongBeats = new[] { true, false, true, false } };

        // Act
        session.LoadRecording([], 8);

        // Assert
        CollectionAssert.AreEqual(new[] { true, false, true, false }, session.StrongBeats.ToArray());
    }
}
