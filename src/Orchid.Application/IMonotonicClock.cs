namespace Orchid.Application;

public interface IMonotonicClock
{
    TimeSpan Elapsed { get; }

    void Restart();

    void Stop();
}
