using Orchid.Core;

namespace Orchid.Infrastructure;

public class TaskDelayProvider : IDelayProvider
{
    public Task Delay(long milliseconds, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
}
