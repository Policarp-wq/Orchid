using Orchid.Core;
using System.Diagnostics;

namespace Orchid.Application;

public class MusicPlayThrough
{
    private readonly Metronome _metronome;
    public MusicScoreDefinition MusicScoreDefinition { get; private set; }

    // BPM counts the time signature's beat unit. Subdivisions can use separate subscribers.
    public MusicPlayThrough(IDelayProvider delayProvider, MusicScoreDefinition musicScoreDefinition, int bpm,
        IBeatSoundPlayer soundPlayer, TraceSource? traceSource = null)
    {
        MusicScoreDefinition = musicScoreDefinition;
        _metronome = new Metronome(
            [new MusicBeatNotifier(musicScoreDefinition.TimeSignature.BeatsPerMeasure, soundPlayer)],
            delayProvider, bpm, traceSource: traceSource);
    }

    public Task Start(CancellationToken cancellationToken) => _metronome.Start(cancellationToken);
}
