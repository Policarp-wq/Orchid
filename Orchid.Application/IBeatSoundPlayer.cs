namespace Orchid.Application;

public interface IBeatSoundPlayer
{
    Task PlayStrongBeat(CancellationToken cancellationToken);
    Task PlayWeakBeat(CancellationToken cancellationToken);
}
