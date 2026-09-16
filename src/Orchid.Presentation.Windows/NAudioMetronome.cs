using NAudio.Wave;
using Orchid.Application;
using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed class NAudioMetronome : IMetronome
{
    private WaveOutEvent? output;
    private bool isDisposed;

    public void Start(Tempo tempo)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        ArgumentNullException.ThrowIfNull(tempo);

        Stop();

        output = new WaveOutEvent
        {
            DesiredLatency = 50,
            NumberOfBuffers = 2
        };

        output.Init(new MetronomeSampleProvider(tempo, sampleRate: 48_000, channels: 2));
        output.Play();
    }

    public void Stop()
    {
        if (output is null)
        {
            return;
        }

        output.Stop();
        output.Dispose();
        output = null;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        Stop();
        isDisposed = true;
    }
}
