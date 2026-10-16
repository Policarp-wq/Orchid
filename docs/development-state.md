# Development state

## Current iteration

- Goal: connect the Windows MAUI window to the existing MIDI recording and metronome code. Preserve the user's Application/storage logic and existing callback concurrency behavior. See [UI integration checkpoint](ui-recording-integration.md).
- Branch: `refactor/aaa-tests-nullability`, created from the local `feat/midi-key-converter` working tree at `b6e447a`. Earlier changes remain uncommitted in the same working tree.
- The user corrected the subtraction's second operand, then requested additional tests and the discussed code cleanup. This iteration replaces the old accidental-normalization helper and fixes the octave-boundary errors exposed by the new tests.
- The input working tree already contained staged and unstaged Core/Application drafts and untracked project files. They remain in place. The user has since completed the in-memory storage draft. The design DoD and preview files were staged separately; no commit or reset was performed.
- No PR or commit was created. A standalone commit would include the user's uncommitted project foundation. Publishing was not requested. The GitHub CLI read also failed because the sandbox could not connect to its proxy; remote PR state is unverified.

## Architecture

- Core owns time generation (`Metronome`, `IBeatSubscriber`, `IDelayProvider`) and score definitions; no audio library or platform code.
- Application references Core, owns `IBeatSoundPlayer` and chooses accents with `MusicBeatNotifier`; `MusicPlayThrough` connects the score to a metronome.
- Infrastructure references Application (and transitively Core), implements `TaskDelayProvider`, `StopwatchTimeProvider`, Windows `NAudioBeatSoundPlayer` and `DryWetMidiInput`. NAudio.Wasapi 3.1.0 and Melanchall.DryWetMidi 8.0.3 stay in Infrastructure and its consumers.
- The Windows MAUI executable now references Infrastructure. WindowsRecordingSession composes the existing listener/storage/metronome and owns device/audio lifetimes; the view coordinates Start/Stop. The manual playback tool remains a separate hardware check.
- The user explicitly requested the Application abstraction for future macOS support. No musical size or NAudio types cross that contract.
- [API usage, backend research and check commands](metronome-audio.md).
- `IMidiKeyConverterProvider` selects by runtime key type and returns `IMidiKeyConverter<MidiKey>`. Infrastructure owns `MidiNoteKey`, `MidiNoteKeyConverter` and `MidiKeyConverterProvider`. The input adapter supplies the MIDI note number; no MIDI capture library is needed for conversion. [Usage](midi-key-conversion.md).

## Current local review description

`DryWetMidiInput` accepts the library's `IInputDevice` and the existing
`KeyListener`. It starts listening until cancellation, removes its subscription
and stops listening in `finally`. The caller disposes the device. NoteOn/NoteOff
are forwarded directly; zero-velocity NoteOn becomes Release. At the user's
explicit request, callback concurrency, draining and application/storage logic
were not repaired. The async-void callback can overlap other callbacks and can
finish after Stop. No queue, lock, new application abstraction or DI was added.

The existing manual hardware tool now supports `--midi-list` and `--midi <index>`.
It prints raw messages live and the user's stored events after Ctrl+C, relative
to C4. `StopwatchTimeProvider` supplies elapsed milliseconds. The old converter
test fixture was updated only to the current KeyPress/KeyInteraction signatures.
The tool is now included in `Orchid.slnx`, with launch profiles for listing MIDI
inputs, recording device 0 and metronome playback. Rider can create run
configurations from these profiles. For the native preview on the WSL share,
the preview README documents an external tool using the existing local-copy
launcher. Actual Rider UI execution has not been validated.
[Usage and intentional limitations](midi-input.md).

`Orchid.UiPreview` is a Windows MAUI executable connected to real MIDI input and
metronome audio through the existing Infrastructure/Application code. Start/Stop
records through KeyListener and TimingStorage; elapsed time, the latest held note
and key highlights are displayed during recording, with review after Stop.
Display refresh copies the storage's 20 ms interval without changing storage.
The timeline keeps fixed time/pitch scales, pinned labels and horizontal scrolling;
piano key colors are independent of theme. File actions remain unavailable.
Sample-based model tests are retained, but no demo notes
appear in the window. Window size changes move the detail panel below the
timeline; theme styles also cover native input text. See the
[integration checkpoint](ui-recording-integration.md) for checks and known limits.

The Windows entry point installs AppDomain/task diagnostics before WinUI starts,
logs exceptions reaching `Main` and subscribes to WinUI's unhandled-exception
event. Logs persist under `%LOCALAPPDATA%\Orchid\UiPreview\Logs`, with timestamps,
PID/thread, source and full available exception details. Native termination before
the handlers run requires Windows crash diagnostics. No logging package or
container was added. [Launch instructions and limitations](../Orchid.UiPreview/README.md).

`Metronome` advances one beat at a time and keeps absolute deadlines. Overdue
beats execute immediately instead of being dropped. Waiting is separated into
`WaitUntil`, with explicit elapsed and remaining milliseconds. Combined subscriber
processing that exceeds one interval emits a plain Critical event through the
built-in `TraceSource`; the manual Windows playback root routes it to stderr.
No logging package, DI container or subscriber profiler was added. AAA checks
cover catch-up, combined processing over budget and the exact interval boundary.

`AGENTS.md` now reflects constructor injection with explicit composition, the
current Core/Application/Infrastructure boundaries, Windows as the application
target, default Windows output paths and separate Linux artifacts. It removes
the obsolete MIDI-domain prohibition, assumed DryWetMidi implementation and
nonexistent `bin/Windows` / `obj/Windows` rules. Existing AAA, nullable and English
project-text preferences remain. The test command uses the current MTP CLI.

`MusicSound` subtraction now combines octave distance, natural-note distance and
a signed accidental offset. The tuple, separate sign flag, out parameter and
copied sounds are removed. Accidentals no longer modify octave numbers; this
corrects B-sharp and C-flat at octave boundaries. Operand names and formatting
make the three components of the calculation explicit.

Eighteen additional AAA interval cases cover sharps/flats in both subtraction
directions, enharmonic notes, mixed accidentals and negative-octave boundaries.
The original ten examples remain. Existing behavior first passed all 94 Core
cases; the expanded suite then exposed 12 boundary failures before production
was changed. The final MusicSound data-driven test has 28 cases.

All automated checks now run in the existing MSTest projects with explicit
Arrange, Act and Assert blocks. The former linked-source console assertions
were migrated to normal project references; `Orchid.AudioChecks` now only
performs manual Windows playback. Independent MIDI rejection checks are split.

Redundant `ThrowIfNull` calls and their runtime-null tests were removed.
`Directory.Build.props` treats the nullable warning group as errors without
changing output paths. Numeric ranges and unsupported key types are still
validated at runtime. The repository rules record these user preferences.

The DI container, registration extensions, their three tests and all four DI
package references were removed at the user's request. Constructors and existing
Application interfaces remain. The manual playback tool's `Program` now creates
the score, delay, audio backend and session directly; it disposes audio after
playback stops. The graph does not justify a container yet.

The user's current `TimingStorage` implements `ITimingStorage` in memory and
groups events in 20 ms bins. It is used as-is by the manual MIDI tool; no dummy
storage or semantic/concurrency corrections were introduced. The user's own
`mvp-development` skill now distinguishes product goals from proposed mechanisms,
requires concrete objections before implementing unnecessary complexity, and
considers the cost of adding a mechanism later. The ponytail skill was not changed.

## Previous MIDI review description

Removed the uninferrable generic type parameter from `GetConverter(MidiKey)`.
The existing converter interface is preserved. Infrastructure maps MIDI note
numbers 0..127 to natural notes/sharps and scientific octaves (60 = C4).
`KeyListener` now receives initialized provider/storage dependencies and stores
the signed interval relative to its home sound, using the existing Core operator.

Review `IMidiKeyConverterProvider`, the three Infrastructure MIDI classes,
`KeyListener`, then `MidiNoteKeyConverterTests`. The new listener constructor
requires provider and storage; there were no existing implemented call sites.
No MIDI-device capture or discovery was added.

## Previous MusicSound test review description

Added `tests/Orchid.Core.Tests/MusicSoundTests.cs` and an MSTest.Sdk 4.4.1
project referencing the actual Core project, registered under `/tests/` in the
solution. One data-driven test covers the ten requested examples.
Production definitions were not edited in this iteration.
`global.json` selects Microsoft.Testing.Platform for the .NET 10 test CLI.
The user subsequently added a `Microsoft.NET.Test.Sdk` package reference;
that edit was preserved.

The requested expectation for C5 minus C#5 was corrected from +0.5 to -0.5
to follow the signed subtraction agreed in the previous iteration. All other
expectations match piano pitch distances. A whole tone is two adjacent-key
steps; an octave is six tones. [Interval reference](https://open-musictheory.github.io/docs/fundamentals/scales/).

## Previous note review description

`MusicalNote.F.Difference(MusicalNote.E)` returns a `Tone` of 0.5;
the reverse is -0.5, and C minus B in the same octave is -5.5.
The extension corrects the enum-index distance when the notes lie on opposite
sides of E/F. No enum replacement or circular interval semantics were introduced.
`Tone.FromNum` now uses a floor-based integer part, preserving negative half
tones and rejecting fractions other than 0 or 0.5.

Review `MusicalNoteExtensions.cs`, `Tone.cs`, then `MusicalNoteChecks.cs`.
The focused executable verifies all 49 note pairs against chromatic positions
and round-trips half-tone values from -6 through 6.

## Previous local review description

Metronome playback previously lacked an audio backend; its stopwatch loop stopped
waiting after the first interval, and the beat notifier modified the cycle length
instead of advancing its position. This iteration introduces an Application
playback port for strong and weak clicks, implements a reusable Windows WASAPI
mixer in Infrastructure, and fixes absolute-deadline timing and cyclic accents.

Review in order: `IBeatSoundPlayer`, `MusicBeatNotifier`, `Metronome`,
`NAudioBeatSoundPlayer`, `MusicPlayThrough`, then `Orchid.AudioChecks`.
The backend is Windows-only; other platforms can implement the same interface.
Sixteenth-note subdivision scheduling remains outside this audio API change.

## Validation and next step

- Rider launch setup: the manual tool is included in the solution and all three launch profiles parse correctly. Full Windows build passes with zero warnings/errors. The Windows CLI launch using `--launch-profile "Orchid MIDI devices"` reads the new settings and now detects `0: 5704PIA-DK`. Physical note input and the actual Rider run actions remain unverified.
- Latest MIDI iteration: Windows SDK 10.0.102 builds the full solution and manual hardware tool with zero warnings/errors. `dotnet test --solution Orchid.slnx --no-build` passes 174/175 cases with one expected non-Windows-only skip (Core 115, Infrastructure 44, preview 16). Before implementing input, five forwarding/lifetime cases failed. `--midi-list` successfully calls the Windows backend but returns no input devices in this environment. Physical MIDI recording and Ctrl+C on a live device remain unverified. Application source was not changed in this iteration.
- Latest preview iteration: Windows SDK 10.0.102 builds the app and MSTest project with zero warnings/errors. All 16 cases pass, including persistent exception/inner-exception/stack details and an unusable log destination. The native window passed seeking, exact note attack selection, silent gaps, F-sharp, overlapping B4/C5, Play/Pause/Rewind, both themes and narrow layout. A temporary deliberate startup exception exited with code 1 and persisted its details; that probe was removed. The local Windows launcher supplies the custom runtime and avoids UNC WinUI activation. Production MIDI/audio/storage were not connected or edited.
- Previous preview assessment: the full Windows solution failed with CS1503 in `KeyListener` and CS0266 in `TimingStorage`. The user has since updated those contracts and completed the storage draft; those compilation errors are resolved. The stale converter test fixture was updated during the MIDI iteration.
- Latest metronome iteration: Windows SDK 10.0.102 builds Core.Tests and all 115 cases pass. Before the fix, two new cases failed: skipped overdue beats and absent Critical logging. Output is in `/tmp/orchid-metronome-red.log` and `/tmp/orchid-metronome-green.log`. The manual audio project, including Core/Application/Infrastructure, builds on Windows with zero warnings/errors. Actual audio hardware was not exercised.
- The latest full solution build is blocked by concurrent MIDI API edits: `RecordingTimingStorage` in `MidiNoteKeyConverterTests` still implements the old signature instead of `ITimingStorage.SaveTimingAsync(Tone, CancellationToken)` (CS0535). Those edits were preserved; the Infrastructure suite could not be rebuilt and its earlier results do not validate this revision.
- Latest arithmetic iteration: Windows SDK 10.0.102 builds the full solution with zero warnings/errors. Core passes 112/112; Infrastructure passes 37/38 with one deliberately skipped non-Windows-only test. There are no test failures. Direct Windows test output is in `/tmp/orchid-alteration-green.log` and `/tmp/orchid-alteration-infra.log`; the pre-fix regression output is in `/tmp/orchid-alteration-red.log`.
- After container removal, the Linux solution and manual playback executable build with zero warnings/errors. No-argument playback startup succeeds. Before the user's subsequent arithmetic edit, the remaining 132 tests had the same nine known failures; output is in `/tmp/orchid-core-without-container.log` and `/tmp/orchid-infra-without-container.log`.
- The updated `mvp-development` skill passes `skill-creator/scripts/quick_validate.py`. No ponytail files were changed.
- The user's Windows Rider log shows only `Orchid.Core.Tests` being compiled while both the Core implementation and reference DLL are missing. Windows SDK 10.0.102 subsequently built the full solution through WSL interop with zero warnings/errors, creating `Orchid.Core/bin/Debug/net10.0/Orchid.Core.dll` and `Orchid.Core/obj/Debug/net10.0/ref/Orchid.Core.dll` at the expected Windows paths. No output paths were changed. Rider's skipped dependency is consistent with its ReSharper incremental build; a solution rebuild or disabling ReSharper Build bypasses that skip.
- Both test applications started successfully through Windows .NET 10.0.2. Those runs used binaries built before the user's latest subtraction edit: Core 86/94, Infrastructure 36/38 with one known failure and one intentionally skipped non-Windows case. These results do not validate the user's newly reported green revision.
- Automated checks are now MSTest tests in `Orchid.Core.Tests` and `Orchid.Infrastructure.Tests`; all have Arrange, Act and Assert blocks. The console project is a manual playback tool.
- The user completed `MusicSound.operator -` before the MSTest iteration. Core builds successfully. This MIDI iteration fixes the Application build errors in `KeyListener` by connecting the provider, storage and existing interval operator.
- The MSTest project built with zero warnings/errors after restoring its SDK to `/tmp/orchid-nuget` with `NUGET_PACKAGES` set. A full solution build also hit an SDK-resolution sandbox error when that environment variable was absent.
- `dotnet test` was blocked by sandbox restrictions on named pipes. The subsequent escalated invocation was aborted by the user.
- Running the already-built test application directly with `dotnet /tmp/orchid-build/bin/Orchid.Core.Tests/debug/Orchid.Core.Tests.dll --results-directory /tmp/orchid-test-results --no-ansi` executed all ten cases: 2 passed, 8 failed. Every nonzero expectation returned 0. The passing cases were C5 minus C5 and C#5 minus C#5. Production code was not changed; the cause of the observed zero results has not been investigated.
- `Orchid.Infrastructure.Tests` builds with warnings treated as errors and zero warnings. Running its MSTest DLL directly executed 23 cases: 22 passed, one failed. Conversion, range validation and provider selection pass; `Listener_SavesConvertedPitchRelativeToHomeSound` observes the same existing Core issue (C5 - C4 returns 0 rather than 6). The Core DLLs in both test projects and the production build are identical by SHA-256. The failing assertion remains and Core was not edited.
- Full solution verification: `NUGET_PACKAGES=/tmp/orchid-nuget dotnet build Orchid.slnx --no-restore --nologo -m:1 -p:UseSharedCompilation=false --artifacts-path /tmp/orchid-build -v:minimal` succeeds with zero warnings and errors, including Core, Application, Infrastructure and both MSTest projects.
- After the AAA migration, direct runs of both MSTest DLLs executed 132 cases: 123 passed and the same 9 known interval failures remain (8 in MusicSoundTests, 1 listener integration). New/migrated note, clock, accent, audio and converter checks pass. Output is saved in `/tmp/orchid-core-aaa-output.log` and `/tmp/orchid-infra-aaa-output.log`.
- A temporary compilation probe importing the repository's properties rejects `new MidiKeyConverterProvider(null)` with error CS8625. This intentional failed build verifies the non-null contract; `null!` and disabled diagnostics can still bypass it.
- AAA phase order was verified across all 25 test methods. The manual playback executable also builds with zero warnings/errors and its no-argument startup was run successfully; actual Windows audio remains unverified in WSL.
- Real WASAPI playback is unverified in Linux/WSL. Run the documented `--play` check on Windows.
- Next: connect a Windows MIDI input and run the documented console check to inspect raw versus stored events. The user wants to investigate storage/parallelism behavior personally. The MAUI window now records real sessions (see [UI integration checkpoint](ui-recording-integration.md)); exact capture, persistence and the timeline work are tracked in the [implementation DoD](orchid-design/IMPLEMENTATION-DOD.md).
