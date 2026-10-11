using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Orchid.Core.Tests;

[TestClass]
public class MusicalNoteTests
{
    public static IEnumerable<object[]> Differences()
    {
        int[] positions = [0, 2, 4, 5, 7, 9, 11];
        for (var left = 0; left < positions.Length; left++)
            for (var right = 0; right < positions.Length; right++)
                yield return [(MusicalNote)left, (MusicalNote)right, (positions[left] - positions[right]) / 2f];
    }

    public static IEnumerable<object[]> ToneValues() =>
        Enumerable.Range(-12, 25).Select(step => new object[] { step / 2f });

    [TestMethod]
    [DynamicData(nameof(Differences))]
    public void Difference_MatchesKeyboardPositions(MusicalNote left, MusicalNote right, float expected)
    {
        // Arrange
        var first = left;
        var second = right;

        // Act
        var actual = first.Difference(second).ToNum();

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DynamicData(nameof(ToneValues))]
    public void Tone_PreservesSignedHalfSteps(float value)
    {
        // Arrange
        var expected = value;

        // Act
        var actual = Tone.FromNum(value).ToNum();

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(7)]
    public void Difference_RejectsInvalidLeftNote(int value)
    {
        // Arrange
        var invalidNote = (MusicalNote)value;

        // Act
        Action act = () => invalidNote.Difference(MusicalNote.C);

        // Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(act);
    }

    [TestMethod]
    public void Difference_RejectsInvalidRightNote()
    {
        // Arrange
        var invalidNote = (MusicalNote)7;

        // Act
        Action act = () => MusicalNote.C.Difference(invalidNote);

        // Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(act);
    }

    [TestMethod]
    public void Tone_RejectsNonHalfStepFraction()
    {
        // Arrange
        var value = 0.55f;

        // Act
        Action act = () => Tone.FromNum(value);

        // Assert
        Assert.ThrowsExactly<InvalidOperationException>(act);
    }
}
