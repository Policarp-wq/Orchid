using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Application;
using Orchid.Core;
using static Orchid.Core.AlterationSymbol;
using static Orchid.Core.MusicalNote;

namespace Orchid.Infrastructure.Tests;

[TestClass]
public class MidiNoteKeyConverterTests
{
    [TestMethod]
    [DataRow(60, C, 4, Natural)]
    [DataRow(61, C, 4, Sharp)]
    [DataRow(62, D, 4, Natural)]
    [DataRow(63, D, 4, Sharp)]
    [DataRow(64, E, 4, Natural)]
    [DataRow(65, F, 4, Natural)]
    [DataRow(66, F, 4, Sharp)]
    [DataRow(67, G, 4, Natural)]
    [DataRow(68, G, 4, Sharp)]
    [DataRow(69, A, 4, Natural)]
    [DataRow(70, A, 4, Sharp)]
    [DataRow(71, B, 4, Natural)]
    [DataRow(0, C, -1, Natural)]
    [DataRow(11, B, -1, Natural)]
    [DataRow(12, C, 0, Natural)]
    [DataRow(21, A, 0, Natural)]
    [DataRow(108, C, 8, Natural)]
    [DataRow(127, G, 9, Natural)]
    public void Convert_ReturnsPitchAndScientificOctave(int number, MusicalNote note, int octave,
        AlterationSymbol alteration)
    {
        // Arrange
        var converter = new MidiNoteKeyConverter();
        var key = new MidiNoteKey(number);
        var expected = new MusicSound(note, octave, alteration);

        // Act
        var actual = converter.ConvertKeyPressToMidiKey(key);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(128)]
    public void Key_RejectsInvalidNoteNumber(int number)
    {
        // Arrange
        var invalidNumber = number;

        // Act
        Action act = () => new MidiNoteKey(invalidNumber);

        // Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(act);
    }

    [TestMethod]
    public void Provider_SelectsConverterForBaseTypedKey()
    {
        // Arrange
        var converter = new MidiNoteKeyConverter();
        var provider = new MidiKeyConverterProvider(converter);
        MidiKey key = new MidiNoteKey(60);

        // Act
        var actual = provider.GetConverter(key);

        // Assert
        Assert.AreSame(converter, actual);
    }

    [TestMethod]
    public void Provider_RejectsUnsupportedKey()
    {
        // Arrange
        var converter = new MidiNoteKeyConverter();
        var provider = new MidiKeyConverterProvider(converter);
        var key = new UnsupportedKey();

        // Act
        Action act = () => provider.GetConverter(key);

        // Assert
        Assert.ThrowsExactly<NotSupportedException>(act);
    }

    [TestMethod]
    public void Converter_RejectsUnsupportedKey()
    {
        // Arrange
        var converter = new MidiNoteKeyConverter();
        var key = new UnsupportedKey();

        // Act
        Action act = () => converter.ConvertKeyPressToMidiKey(key);

        // Assert
        Assert.ThrowsExactly<ArgumentException>(act);
    }

    [TestMethod]
    public async Task Listener_SavesConvertedPitchRelativeToHomeSound()
    {
        // Arrange
        var storage = new RecordingTimingStorage();
        var provider = new MidiKeyConverterProvider(new MidiNoteKeyConverter());
        var listener = new KeyListener(new MusicSound(C, 4), provider, storage);
        var key = new MidiNoteKey(72);
        var expected = new KeyPress(new Tone(6), KeyInteraction.Press);

        // Act
        await listener.RememberKeyPress(key, KeyInteraction.Press, default);

        // Assert
        Assert.AreEqual(expected, storage.Saved);
    }

    private sealed class UnsupportedKey : MidiKey;

    private sealed class RecordingTimingStorage : ITimingStorage
    {
        public KeyPress? Saved { get; private set; }
        public Task SaveTimingAsync(KeyPress timing, CancellationToken cancellationToken)
        {
            Saved = timing;
            return Task.CompletedTask;
        }
    }
}
