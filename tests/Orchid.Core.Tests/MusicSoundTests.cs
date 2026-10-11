using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Orchid.Core.AlterationSymbol;
using static Orchid.Core.MusicalNote;

namespace Orchid.Core.Tests;

[TestClass]
public class MusicSoundTests
{
    [TestMethod]
    [DataRow(C, 5, Natural, C, 5, Natural, 0f, DisplayName = "C5 - C5 = 0")]
    [DataRow(C, 5, Natural, C, 4, Natural, 6f, DisplayName = "C5 - C4 = 6")]
    [DataRow(C, 4, Natural, C, 5, Natural, -6f, DisplayName = "C4 - C5 = -6")]
    [DataRow(C, 5, Sharp, C, 5, Sharp, 0f, DisplayName = "C#5 - C#5 = 0")]
    [DataRow(C, 5, Sharp, C, 5, Natural, 0.5f, DisplayName = "C#5 - C5 = 0.5")]
    [DataRow(C, 5, Natural, C, 5, Sharp, -0.5f, DisplayName = "C5 - C#5 = -0.5")]
    [DataRow(F, 5, Natural, E, 5, Natural, 0.5f, DisplayName = "F5 - E5 = 0.5")]
    [DataRow(C, 5, Natural, B, 4, Natural, 0.5f, DisplayName = "C5 - B4 = 0.5")]
    [DataRow(B, 5, Natural, C, 5, Natural, 5.5f, DisplayName = "B5 - C5 = 5.5")]
    [DataRow(A, 5, Natural, F, 4, Natural, 8f, DisplayName = "A5 - F4 = 8")]
    [DataRow(C, 5, Flat, C, 5, Natural, -0.5f, DisplayName = "Cb5 - C5 = -0.5")]
    [DataRow(C, 5, Natural, C, 5, Flat, 0.5f, DisplayName = "C5 - Cb5 = 0.5")]
    [DataRow(B, 4, Sharp, C, 5, Natural, 0f, DisplayName = "B#4 - C5 = 0")]
    [DataRow(C, 5, Natural, B, 4, Sharp, 0f, DisplayName = "C5 - B#4 = 0")]
    [DataRow(C, 5, Flat, B, 4, Natural, 0f, DisplayName = "Cb5 - B4 = 0")]
    [DataRow(B, 4, Natural, C, 5, Flat, 0f, DisplayName = "B4 - Cb5 = 0")]
    [DataRow(E, 5, Sharp, F, 5, Natural, 0f, DisplayName = "E#5 - F5 = 0")]
    [DataRow(F, 5, Natural, E, 5, Sharp, 0f, DisplayName = "F5 - E#5 = 0")]
    [DataRow(F, 5, Flat, E, 5, Natural, 0f, DisplayName = "Fb5 - E5 = 0")]
    [DataRow(E, 5, Natural, F, 5, Flat, 0f, DisplayName = "E5 - Fb5 = 0")]
    [DataRow(B, 4, Sharp, C, 5, Flat, 0.5f, DisplayName = "B#4 - Cb5 = 0.5")]
    [DataRow(C, 5, Flat, B, 4, Sharp, -0.5f, DisplayName = "Cb5 - B#4 = -0.5")]
    [DataRow(C, 5, Sharp, C, 5, Flat, 1f, DisplayName = "C#5 - Cb5 = 1")]
    [DataRow(C, 5, Flat, C, 5, Sharp, -1f, DisplayName = "Cb5 - C#5 = -1")]
    [DataRow(B, 5, Flat, A, 5, Natural, 0.5f, DisplayName = "Bb5 - A5 = 0.5")]
    [DataRow(A, 5, Natural, B, 5, Flat, -0.5f, DisplayName = "A5 - Bb5 = -0.5")]
    [DataRow(B, -1, Sharp, C, 0, Natural, 0f, DisplayName = "B#-1 - C0 = 0")]
    [DataRow(C, 0, Flat, B, -1, Natural, 0f, DisplayName = "Cb0 - B-1 = 0")]
    public void Subtraction_ReturnsSignedDifferenceInWholeTones(
        MusicalNote leftNote, int leftOctave, AlterationSymbol leftAlteration,
        MusicalNote rightNote, int rightOctave, AlterationSymbol rightAlteration,
        float expected)
    {
        // Arrange
        var left = new MusicSound(leftNote, leftOctave, leftAlteration);
        var right = new MusicSound(rightNote, rightOctave, rightAlteration);

        // Act
        var actual = (left - right).ToNum();

        // Assert
        Assert.AreEqual(expected, actual);
    }
}
