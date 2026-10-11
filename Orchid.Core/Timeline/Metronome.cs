using System.Diagnostics;

namespace Orchid.Core;

public class Metronome
{
    private const int MillisecondsInMinute = 60000;
    private readonly IReadOnlyCollection<IBeatSubscriber> _subscribers;
    private readonly IDelayProvider _delayProvider;
    private readonly TimeProvider _timeProvider;
    private readonly TraceSource _traceSource;
    public int Bpm { get; private set; }

    public Metronome(
        IReadOnlyCollection<IBeatSubscriber> subscribers,
        IDelayProvider delayProvider,
        int bpm,
        TimeProvider? timeProvider = null,
        TraceSource? traceSource = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bpm);
        _subscribers = subscribers;
        _delayProvider = delayProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _traceSource = traceSource ?? new TraceSource("Orchid.Metronome", SourceLevels.Critical);
        Bpm = bpm;
    }
    /// <summary>
    /// Beats with provided BPM and notifies all subscribers on each beat, starting without delay
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task Start(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var intervalMilliseconds = (double)MillisecondsInMinute / Bpm;
        var startedAt = _timeProvider.GetTimestamp();
        long beat = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var processingStartedAt = _timeProvider.GetTimestamp();
            foreach (var beatSubscriber in _subscribers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await beatSubscriber.OnBeat(cancellationToken);
            }

            var processingMilliseconds = _timeProvider.GetElapsedTime(processingStartedAt).TotalMilliseconds;
            if (processingMilliseconds > intervalMilliseconds)
            {
                _traceSource.TraceEvent(TraceEventType.Critical, 0,
                    "Beat processing exceeded the beat interval.");
            }

            beat++;
            await WaitUntil(startedAt, beat * intervalMilliseconds, cancellationToken);
        }
    }

    private async Task WaitUntil(long startedAt, double deadlineMilliseconds, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsedMilliseconds = _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
            var remainingMilliseconds = deadlineMilliseconds - elapsedMilliseconds;
            if (remainingMilliseconds <= 0) return;

            await _delayProvider.Delay((long)Math.Ceiling(remainingMilliseconds), cancellationToken);
        }
    }
}
