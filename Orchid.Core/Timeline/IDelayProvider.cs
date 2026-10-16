namespace Orchid.Core;

public interface IDelayProvider
{
    public Task Delay(long milliseconds, CancellationToken cancellationToken);
}
