# Metronome audio

`Orchid.Application.IBeatSoundPlayer` queues either a strong or a weak click.
It has no time signature, BPM, audio-file, or NAudio dependency. Completion
means queued playback; it does not wait for the 40 ms click to finish.
Cancellation prevents queuing a new click; it does not retract audio already
buffered by the device.

`MusicBeatNotifier` chooses the first beat of any positive-length cycle as
strong. `MusicPlayThrough` supplies that cycle from the score and exposes
`Start(CancellationToken)`. BPM counts the score's beat unit. The old sixteenth
subdivision TODO is outside this change: the sound API plays individual clicks.

`Orchid.Infrastructure.Audio.NAudioBeatSoundPlayer` is the Windows implementation.
It uses [NAudio.Wasapi 3.1.0](https://www.nuget.org/packages/NAudio.Wasapi/3.1.0)
and a continuously running shared-mode mixer. Short fading sine tones distinguish
strong (1600 Hz) and weak (1000 Hz) clicks. WASAPI uses the device's sample rate
and channel count and requests low latency with a 20 ms fallback setting.
Actual latency depends on the Windows driver; this is not sample-accurate scheduling.
The backend follows the device selected at construction. If output stops,
playback requests fail instead of silently queuing inaudible clicks.

Create the playback graph in the presentation composition root and keep the
audio backend alive until the session stops:

```csharp
using Orchid.Application;
using Orchid.Infrastructure.Audio;
using Orchid.Infrastructure.Timeline;

using var sound = new NAudioBeatSoundPlayer();
var session = new MusicPlayThrough(new TaskDelayProvider(), score, score.Bpm, sound);
await session.Start(cancellationToken);
```

A macOS backend implements the same Application interface in Infrastructure.
The composition root selects that implementation; neither the metronome nor
the beat subscriber needs platform checks. Pass the chosen backend through the
existing `IBeatSoundPlayer` constructor parameter.

`Metronome` uses monotonic absolute deadlines with fractional BPM intervals.
It emits the first beat immediately, compensates for subscriber execution
time, and emits every beat in order. Overdue beats execute immediately until the
metronome catches up with its schedule; no beat or accent is skipped.
Use one `Start` call per instance at a time.

When the combined subscriber processing time exceeds one beat interval, the
metronome writes `Beat processing exceeded the beat interval.` at `Critical`
level through `System.Diagnostics.TraceSource`. The optional constructor
parameter accepts the caller's trace source. The manual playback tool directs
these events to stderr. Exactly one interval is allowed; no detailed timing or
subscriber profiling is logged yet.

## Checks

Automated checks use MSTest and normal project references. Every test has
separate Arrange, Act and Assert blocks:

```sh
dotnet test --project tests/Orchid.Core.Tests/Orchid.Core.Tests.csproj
dotnet test --project tests/Orchid.Infrastructure.Tests/Orchid.Infrastructure.Tests.csproj
```

They check arbitrary accent cycles, immediate startup, interval waiting,
fractional BPM drift, overdue beats, the processing budget and Critical logging, distinct finite audio samples,
cancellation, stopped output, disposal and device reuse, without audio hardware.

To additionally hear a four-second 3/4 session at 120 BPM on Windows:

```sh
dotnet run --project tests/Orchid.AudioChecks -- --play
```

`Orchid.AudioChecks` is only a manual Windows playback tool; it does not run
automated assertions.
