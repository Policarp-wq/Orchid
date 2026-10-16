using Orchid.Core;

namespace Orchid.Application;

public interface ITimingStorage
{
    Task SaveTimingAsync(KeyPress timing, CancellationToken cancellationToken);
}
