using System.Diagnostics;
using Orchid.Application;

namespace Orchid.Presentation.Windows;

internal sealed class StopwatchClock : IMonotonicClock
{
    private readonly Stopwatch stopwatch = new();

    public TimeSpan Elapsed => stopwatch.Elapsed;

    public void Restart()
    {
        stopwatch.Restart();
    }

    public void Stop()
    {
        stopwatch.Stop();
    }
}
