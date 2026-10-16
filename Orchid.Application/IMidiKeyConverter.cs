using Orchid.Core;

namespace Orchid.Application;

public interface IMidiKeyConverter<in T> where T : MidiKey
{
    public MusicSound ConvertKeyPressToMidiKey(T keyPress);
}
