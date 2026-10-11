using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orchid.Platform.Windows;

namespace Orchid.Infrastructure.Tests;

[TestClass]
public class NAudioBeatSoundPlayerTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Click_ProducesFortyMillisecondsOfAudio(bool strong)
    {
        // Arrange
        using var output = new RecordingOutput();
        using var player = new NAudioBeatSoundPlayer(output);
        Func<CancellationToken, Task> play = strong ? player.PlayStrongBeat : player.PlayWeakBeat;

        // Act
        await play(default);
        var samples = output.ReadSamples();

        // Assert
        Assert.IsTrue(samples.Take(3840).Any(sample => sample != 0));
        Assert.IsTrue(samples.Skip(3840).All(sample => sample == 0));
    }

    [TestMethod]
    public async Task StrongAndWeakClicks_ProduceDistinctAudio()
    {
        // Arrange
        using var output = new RecordingOutput();
        using var player = new NAudioBeatSoundPlayer(output);

        // Act
        await player.PlayStrongBeat(default);
        var strong = output.ReadSamples();
        await player.PlayWeakBeat(default);
        var weak = output.ReadSamples();

        // Assert
        Assert.IsFalse(strong.SequenceEqual(weak));
    }

    [TestMethod]
    public async Task Clicks_ReuseOutputDevice()
    {
        // Arrange
        using var output = new RecordingOutput();
        using var player = new NAudioBeatSoundPlayer(output);

        // Act
        await player.PlayStrongBeat(default);
        await player.PlayWeakBeat(default);

        // Assert
        Assert.AreEqual(1, output.InitCount);
        Assert.AreEqual(1, output.PlayCount);
    }

    [TestMethod]
    public void CancelledClick_IsNotQueued()
    {
        // Arrange
        using var output = new RecordingOutput();
        using var player = new NAudioBeatSoundPlayer(output);
        using var stop = new CancellationTokenSource();
        stop.Cancel();

        // Act
        Action act = () => player.PlayStrongBeat(stop.Token);

        // Assert
        Assert.ThrowsExactly<OperationCanceledException>(act);
        Assert.IsTrue(output.ReadSamples().All(sample => sample == 0));
    }

    [TestMethod]
    public void StoppedOutput_RejectsClick()
    {
        // Arrange
        using var output = new RecordingOutput();
        using var player = new NAudioBeatSoundPlayer(output);
        output.Stop();

        // Act
        Action act = () => player.PlayWeakBeat(default);

        // Assert
        Assert.ThrowsExactly<InvalidOperationException>(act);
    }

    [TestMethod]
    public void Dispose_ReleasesOutput()
    {
        // Arrange
        using var output = new RecordingOutput();
        using var player = new NAudioBeatSoundPlayer(output);

        // Act
        player.Dispose();

        // Assert
        Assert.IsTrue(output.Disposed);
    }

    [TestMethod]
    public void DisposedPlayer_RejectsClick()
    {
        // Arrange
        using var output = new RecordingOutput();
        using var player = new NAudioBeatSoundPlayer(output);
        player.Dispose();

        // Act
        Action act = () => player.PlayStrongBeat(default);

        // Assert
        Assert.ThrowsExactly<ObjectDisposedException>(act);
    }

    [TestMethod]
    public void Constructor_RejectsNonWindowsPlatform()
    {
        // Arrange
        if (OperatingSystem.IsWindows()) Assert.Inconclusive("This case requires a non-Windows platform.");

        // Act
        Action act = () => new NAudioBeatSoundPlayer();

        // Assert
        Assert.ThrowsExactly<PlatformNotSupportedException>(act);
    }
}
