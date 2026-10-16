using Orchid.Core;

namespace Orchid.Application;

public class MusicBeatNotifier : IBeatSubscriber
{
    private readonly IBeatSoundPlayer _soundPlayer;
    public int MaxIteration { get; private set; }
    public int CurrentIteration { get; private set; }

    public MusicBeatNotifier(int maxIteration, IBeatSoundPlayer soundPlayer)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIteration);
        MaxIteration = maxIteration;
        _soundPlayer = soundPlayer;
    }

    public async Task OnBeat(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CurrentIteration == 0)
        {
            await _soundPlayer.PlayStrongBeat(cancellationToken);
        }
        else
        {
            await _soundPlayer.PlayWeakBeat(cancellationToken);
        }
        CurrentIteration = (CurrentIteration + 1) % MaxIteration;
    }
}
