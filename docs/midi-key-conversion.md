# MIDI key conversion

`IMidiKeyConverterProvider.GetConverter(MidiKey)` selects a converter using
the key's runtime type. The returned `IMidiKeyConverter<MidiKey>` accepts
the base-typed key already used by `KeyListener`; callers do not supply a
generic type argument.

The Infrastructure implementation uses `MidiNoteKey` for a MIDI note number
from 0 through 127. `MidiNoteKeyConverter` produces a `MusicSound` with a
natural note or a sharp. It uses scientific octave numbering: 60 is C4,
69 is A4, 0 is C-1 and 127 is G9. Octave names are a project convention;
MIDI specifies the key number. [MIDI key-number reference](https://cmtext.indiana.edu/MIDI/chapter3_channel_voice_messages.php).

An input adapter passes its library's note number into `MidiNoteKey`.
The live [DryWetMidi adapter and console check](midi-input.md) now do this for
incoming NoteOn/NoteOff messages.
Conversion does not require an audio device or another MIDI package.
Device capture and MIDI message parsing remain separate from this converter.

```csharp
IMidiKeyConverterProvider provider =
    new MidiKeyConverterProvider(new MidiNoteKeyConverter());
MidiKey key = new MidiNoteKey(61);
MusicSound sound = provider.GetConverter(key).ConvertKeyPressToMidiKey(key);
// sound is C#4.

var listener = new KeyListener(homeSound, provider, timingStorage);
await listener.RememberKeyPress(key, elapsedMilliseconds, cancellationToken);
```

`KeyListener` receives the provider and storage through its constructor and
stores the converted sound's signed interval relative to `HomeSound`.
The storage owns persistence. `MusicSound` owns interval arithmetic.

## Composition

The executable creates the converter, provider and listener as shown above.
Dependencies still enter through constructors; no DI container or registration
package is needed for this small graph. The composition root owns the lifetime
of disposable dependencies.

`timingStorage` must implement `ITimingStorage`; the repository currently defines
only its contract. No placeholder persistence is added. Tests supply a recording
storage implementation. A different audio backend implements `IBeatSoundPlayer`
and is selected by the executable without changing Application.

## Non-null contracts and validation

Non-null internal parameters rely on nullable reference analysis rather than
`ThrowIfNull`. `Directory.Build.props` treats nullable warnings as errors.
Passing a literal `null` fails compilation; `null!` or disabled diagnostics can
still bypass analysis. This policy does not add runtime null guarantees.

The provider rejects unknown key types with `NotSupportedException`.
The converter rejects a mismatched key with `ArgumentException`, and
invalid MIDI note numbers are rejected when constructing the key.

Run the conversion and integration tests with .NET 10:

```sh
dotnet test --project tests/Orchid.Infrastructure.Tests/Orchid.Infrastructure.Tests.csproj
```
