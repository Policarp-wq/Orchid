namespace Orchid.Application;

public interface IMidiKeyConverterProvider
{
    IMidiKeyConverter<MidiKey> GetConverter(MidiKey midiKey);
}
