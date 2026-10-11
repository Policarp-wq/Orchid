namespace Orchid.Core;

public interface IBeatSubscriber
{
    public Task OnBeat(CancellationToken cancellationToken);
}
