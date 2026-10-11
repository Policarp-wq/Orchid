using System.Diagnostics;
using Orchid.Application;

namespace Orchid.Infrastructure;

public sealed class StopwatchTimeProvider : ITimeProvider
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    public long GetCurrentTimeMilliseconds() => _clock.ElapsedMilliseconds;
}
