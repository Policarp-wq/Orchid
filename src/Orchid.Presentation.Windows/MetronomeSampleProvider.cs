using NAudio.Wave;
using Orchid.Core;

namespace Orchid.Presentation.Windows;

internal sealed class MetronomeSampleProvider : ISampleProvider
{
    private const double RegularFrequency = 1100;
    private const double AccentFrequency = 1650;
    private const double ClickDurationSeconds = 0.035;
    private readonly double framesPerBeat;
    private readonly int beatsPerMeasure;
    private readonly int clickFrameCount;
    private long framePosition;

    public MetronomeSampleProvider(Tempo tempo, int sampleRate, int channels)
    {
        ArgumentNullException.ThrowIfNull(tempo);

        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        framesPerBeat = sampleRate * tempo.GetDuration(tempo.BeatUnit).TotalSeconds;
        beatsPerMeasure = (int)tempo.BeatUnit;
        clickFrameCount = (int)Math.Round(sampleRate * ClickDurationSeconds);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        var channels = WaveFormat.Channels;
        var frameCount = count / channels;

        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var sample = GetClickSample(framePosition + frameIndex);

            for (var channel = 0; channel < channels; channel++)
            {
                buffer[offset + (frameIndex * channels) + channel] = sample;
            }
        }

        framePosition += frameCount;
        return frameCount * channels;
    }

    private float GetClickSample(long currentFrame)
    {
        var beatIndex = Math.Max(0L, (long)Math.Floor(currentFrame / framesPerBeat));
        var beatStartFrame = (long)Math.Round(beatIndex * framesPerBeat);

        if (beatStartFrame > currentFrame && beatIndex > 0)
        {
            beatIndex--;
            beatStartFrame = (long)Math.Round(beatIndex * framesPerBeat);
        }

        var clickFrame = currentFrame - beatStartFrame;

        if (clickFrame < 0 || clickFrame >= clickFrameCount)
        {
            return 0;
        }

        var isMeasureStart = beatIndex % beatsPerMeasure == 0;
        var frequency = isMeasureStart ? AccentFrequency : RegularFrequency;
        var amplitude = isMeasureStart ? 0.65 : 0.45;
        var seconds = clickFrame / (double)WaveFormat.SampleRate;
        var envelope = 1 - (clickFrame / (double)clickFrameCount);

        return (float)(Math.Sin(2 * Math.PI * frequency * seconds) * amplitude * envelope);
    }
}
