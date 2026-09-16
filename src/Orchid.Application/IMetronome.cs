using Orchid.Core;

namespace Orchid.Application;

public interface IMetronome : IDisposable
{
    void Start(Tempo tempo);

    void Stop();
}
