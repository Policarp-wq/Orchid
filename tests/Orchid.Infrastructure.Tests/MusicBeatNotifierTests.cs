using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Application;

namespace Orchid.Infrastructure.Tests;

[TestClass]
public class MusicBeatNotifierTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(7)]
    public async Task OnBeat_AccentsFirstBeatOfEveryCycle(int cycleLength)
    {
        // Arrange
        var sound = new RecordingSound();
        var notifier = new MusicBeatNotifier(cycleLength, sound);
        var expected = Enumerable.Range(0, cycleLength * 2).Select(i => i % cycleLength == 0).ToArray();

        // Act
        for (var i = 0; i < expected.Length; i++) await notifier.OnBeat(default);

        // Assert
        CollectionAssert.AreEqual(expected, sound.Beats.ToArray());
        Assert.AreEqual(cycleLength, notifier.MaxIteration);
        Assert.AreEqual(0, notifier.CurrentIteration);
    }

    [TestMethod]
    public void Constructor_RejectsEmptyCycle()
    {
        // Arrange
        var sound = new RecordingSound();

        // Act
        Action act = () => new MusicBeatNotifier(0, sound);

        // Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(act);
    }

    private sealed class RecordingSound : IBeatSoundPlayer
    {
        public List<bool> Beats { get; } = [];
        public Task PlayStrongBeat(CancellationToken cancellationToken)
        {
            Beats.Add(true);
            return Task.CompletedTask;
        }
        public Task PlayWeakBeat(CancellationToken cancellationToken)
        {
            Beats.Add(false);
            return Task.CompletedTask;
        }
    }
}
