using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Orchid.Application;

namespace Orchid.Platform.Windows;

public class NAudioBeatSoundPlayer : IBeatSoundPlayer, IDisposable
{
    private readonly IWavePlayer _output;
    private readonly MixingSampleProvider _mixer;
    private readonly object _sync = new();
    private bool _disposed;

    public NAudioBeatSoundPlayer() : this(CreateOutput()) { }

    internal NAudioBeatSoundPlayer(IWavePlayer output)
    {
        _output = output;
        try
        {
            var format = OperatingSystem.IsWindows() && output is WasapiPlayer wasapi ? wasapi.DeviceMixFormat :
                WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
            _mixer = new MixingSampleProvider(
                WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels)) { ReadFully = true };
            _output.Init(_mixer);
            _output.Play();
        }
        catch
        {
            _output.Dispose();
            throw;
        }
    }

    private static IWavePlayer CreateOutput()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Metronome audio playback requires Windows.");

        return new WasapiPlayerBuilder().WithLatency(20).WithLowLatency().Build();
    }

    public Task PlayStrongBeat(CancellationToken cancellationToken) => PlayClick(1600, 0.3, cancellationToken);
    public Task PlayWeakBeat(CancellationToken cancellationToken) => PlayClick(1000, 0.2, cancellationToken);

    private Task PlayClick(double frequency, double gain, CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (_output.PlaybackState != PlaybackState.Playing)
                throw new InvalidOperationException("The audio output has stopped.");

            var tone = new SignalGenerator(_mixer.WaveFormat.SampleRate, _mixer.WaveFormat.Channels)
            {
                Type = SignalGeneratorType.Sin, Frequency = frequency, Gain = gain
            };
            var click = new FadeInOutSampleProvider(tone);
            click.BeginFadeOut(40);
            _mixer.AddMixerInput(click.Take(TimeSpan.FromMilliseconds(40)));
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _output.Dispose();
        }
    }
}
