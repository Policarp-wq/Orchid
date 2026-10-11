using Orchid.Core;

namespace Orchid.Application;

public class KeyListener
{
    private readonly IMidiKeyConverterProvider _midiKeyConverterProvider;
    private readonly ITimingStorage _timingStorage;
    public MusicSound HomeSound { get; private set; }
    public KeyListener(MusicSound homeSound, IMidiKeyConverterProvider midiKeyConverterProvider,
        ITimingStorage timingStorage)
    {
        HomeSound = homeSound;
        _midiKeyConverterProvider = midiKeyConverterProvider;
        _timingStorage = timingStorage;
    }

    public Task RememberKeyPress(MidiKey midiKey, KeyInteraction keyInteraction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sound = _midiKeyConverterProvider.GetConverter(midiKey).ConvertKeyPressToMidiKey(midiKey);

        return _timingStorage.SaveTimingAsync(new(GetTone(sound), keyInteraction), cancellationToken);
    }

    private Tone GetTone(MusicSound sound) => sound - HomeSound;
}
