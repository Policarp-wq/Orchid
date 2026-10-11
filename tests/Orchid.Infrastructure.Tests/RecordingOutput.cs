using NAudio.Wave;

namespace Orchid.Infrastructure.Tests;

sealed class RecordingOutput : IWavePlayer
{
    private IWaveProvider _source = new SilenceProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
    public event EventHandler<StoppedEventArgs>? PlaybackStopped;
    public PlaybackState PlaybackState { get; private set; }
    public float Volume { get; set; }
    public WaveFormat OutputWaveFormat => _source.WaveFormat;
    public int InitCount { get; private set; }
    public int PlayCount { get; private set; }
    public bool Disposed { get; private set; }

    public void Init(IWaveProvider source)
    {
        _source = source;
        InitCount++;
    }

    public void Play()
    {
        PlaybackState = PlaybackState.Playing;
        PlayCount++;
    }

    public void Pause() => PlaybackState = PlaybackState.Paused;

    public void Stop()
    {
        PlaybackState = PlaybackState.Stopped;
        PlaybackStopped?.Invoke(this, new StoppedEventArgs());
    }

    public void Dispose()
    {
        Stop();
        Disposed = true;
    }

    public float[] ReadSamples()
    {
        var bytes = new byte[4800 * sizeof(float)];
        _source.Read(bytes);
        var samples = new float[4800];
        Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
        return samples;
    }
}
