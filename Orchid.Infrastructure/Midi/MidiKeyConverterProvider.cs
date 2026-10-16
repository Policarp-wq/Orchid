using Orchid.Application;

namespace Orchid.Infrastructure;

public sealed class MidiKeyConverterProvider : IMidiKeyConverterProvider
{
    private readonly MidiNoteKeyConverter _converter;

    public MidiKeyConverterProvider(MidiNoteKeyConverter converter)
    {
        _converter = converter;
    }

    public IMidiKeyConverter<MidiKey> GetConverter(MidiKey midiKey)
    {
        if (midiKey is MidiNoteKey) return _converter;
        throw new NotSupportedException($"No converter is available for {midiKey.GetType().Name}.");
    }
}
