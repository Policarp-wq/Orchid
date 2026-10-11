# Live MIDI input

`Orchid.Infrastructure.Midi.DryWetMidiInput` uses the previously selected
[DryWetMidi 8.0.3](https://www.nuget.org/packages/Melanchall.DryWetMidi/8.0.3).
Its library types stay in Infrastructure. The constructor takes a library
`IInputDevice` and the existing Application `KeyListener`.

```csharp
using var device = InputDevice.GetByIndex(0);
var storage = new TimingStorage(new StopwatchTimeProvider());
var listener = new KeyListener(new MusicSound(MusicalNote.C, 4),
    new MidiKeyConverterProvider(new MidiNoteKeyConverter()), storage);
var input = new DryWetMidiInput(device, listener);
await input.RunAsync(cancellationToken);
```

`RunAsync` subscribes, starts the supplied device and waits until cancellation.
Its `finally` detaches the handler and stops listening. The caller owns and
must dispose the supplied device after the run. Cancellation is propagated as
`OperationCanceledException`. Use one run per device at a time.

NoteOn with positive velocity becomes Press; NoteOff and zero-velocity NoteOn
become Release. Other messages are ignored by the adapter. MIDI number is wrapped
in the existing `MidiNoteKey`, then the converter/listener/store path is used.
Device enumeration stays in the caller.
[Library input/lifetime documentation](https://melanchall.github.io/drywetmidi/articles/devices/Input-device.html).

## Try it on Windows

From the repository root, using the user's Windows .NET SDK:

```powershell
$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $dotnet run --project tests/Orchid.AudioChecks -- --midi-list
& $dotnet run --project tests/Orchid.AudioChecks -- --midi 0
```

Choose the index printed by the first command. Play some notes, then press Ctrl+C.
The tool prints raw MIDI messages live and the stored events after stopping:

In Rider, reload `Orchid.slnx` and select an `Orchid.AudioChecks` launch profile:
`Orchid MIDI devices`, `Orchid MIDI recording` or `Orchid metronome`. These profiles
are declared in `Properties/launchSettings.json`; recording defaults to device 0.
Change its argument in the run configuration to use another index. If Rider does
not create the configurations automatically, add a `.NET Launch Settings Profile`
configuration for `Orchid.AudioChecks` and choose the matching profile. The IDE's
Stop action can terminate the process without printing the final stored events;
use Ctrl+C in a terminal for the documented recording-stop path.

```text
RAW 12:00:00.100 Note On [...]
Stored events (intervals in whole tones relative to C4):
     100 ms | Press   |     6 tones
     500 ms | Release |     6 tones
```

The values above are illustrative. The home sound is C4. Six tones represent
C5 relative to C4. Stored timestamps are exactly what the current `TimingStorage`
returns, including its 20 ms grouping. To retain the console output:

```powershell
& $dotnet run --project tests/Orchid.AudioChecks -- --midi 0 |
    Tee-Object -FilePath .\midi-session.log
```

This is a diagnostic text log, not the production recording file format. The
independent MAUI preview still uses its sample session.

## Deliberately unchanged behavior

At the user's request, this change does not repair application/storage logic or
concurrency. MIDI event callbacks call the listener directly with `async void`;
there is no queue, serialization, locking or wait for callback completion at
Stop. Exceptions from these callbacks follow async-void exception handling rather
than faulting `RunAsync`. The manual tool prints the library's `ErrorOccurred`
events to stderr. Raw callback threads are not UI threads.

The store's grouping and current event contracts are preserved. Channels and
velocity are not added to `KeyPress`; velocity is used only to classify a silent
NoteOn. Inspect raw messages versus stored events to investigate those choices.

## Acceptance

- NoteOn/NoteOff reach the existing listener with the correct interaction.
- Zero-velocity NoteOn is Release; non-note messages do not enter the store.
- Cancellation stops listening and unsubscribes, without disposing caller-owned
  device resources; failed start propagates and removes the handler.
- The manual tool lists real Windows inputs and can print raw/stored data.
- Application storage/parallelism behavior remains unchanged.

AAA tests use the library's existing `IInputDevice` contract and the real
`KeyListener`/converter. They validate forwarding and listening lifetime, not
asynchronous callback ordering or storage correctness.

Windows verification: solution and manual tool builds passed with zero
warnings/errors. All 174 applicable MSTest cases passed, with one expected
non-Windows-only skip. Device enumeration loaded the native backend successfully
and reported no MIDI inputs. Real keyboard recording and live-device Ctrl+C
have not been verified in this environment.
