# Windows UI recording integration

Date: 2026-10-08. Branch: refactor/aaa-tests-nullability. Changes are uncommitted;
the user's pre-existing staged diff was preserved byte-for-byte.

## Scope and checks

- [x] Enumerate/select real MIDI inputs; disable Start without an input.
- [x] Start MIDI capture and metronome together, and cancel both through Stop.
- [x] Use the existing DryWetMidiInput, converter, KeyListener, TimingStorage and
  MusicPlayThrough. Application/Core and the existing input adapter are unchanged.
- [x] Stop, device startup failures and audio task failures are covered by tests.
- [x] Map stored Press/Release events into the existing review model; test overlap,
  silence, repeated presses, unmatched releases and held-key closure at Stop.
- [x] Preserve recorded BPM/pulse when setup changes for the next recording.
- [x] Windows build and affected test suites pass. The three pre-existing Core
  invalid-enum test failures remain outside this change.
- [x] Native window starts with 5704PIA-DK available. Real Start/Stop was checked
  on a 10.23-second empty recording; review and setup controls became available.
- [ ] Physical key press/release recording and listening to metronome clicks.
- [ ] Physical device disconnect and stopping while MIDI callbacks are active.

## Start regression fixed

The first fixed-scale timeline revision awaited ScrollToAsync(0, 0) before
starting capture. A no-op MAUI scroll remained pending in the native window:
the elapsed timer ran, but MIDI/metronome never started and Stop stayed on
Stopping. Startup now requests scrolling without awaiting its completion.
The Windows window completed an 87.75-second empty recording through Stop
immediately after this change, then another Start/Stop completed at 16.93 seconds.
Build and existing test suites retain the results
below; physical note display still needs a hardware check.

## Composition and deliberate limits

WindowsRecordingSession in Infrastructure owns device/audio lifetimes, creates
the existing application objects and cancels the sibling operation on failure.
The view owns one CancellationTokenSource and runs the session off the UI thread.
No new MIDI listener, converter, queue, storage fix or DI container was added.

Capture and displayed elapsed time use one relative origin. The existing
metronome begins immediately after listening starts, with its own scheduling
origin. No hardware latency measurement or calibration is claimed. The selected
pulse controls clicks per 4/4 measure through the existing beat notifier.

During recording MIDI notifications update held keys and the latest held note.
The view refreshes every 20 ms, copying TimingStorage's current buffer interval
as requested; the display does not read the storage's mutable collections live.
Storage is read after the input task stops; the existing adapter still does not drain callbacks. Its concurrency
limitations and the storage's 20 ms timestamp bins are intentionally retained at
the user's request. This is not completion of every item in the broader design DoD.

Review reconstructs MIDI pitches from C4-relative stored intervals. Channel and
velocity are unavailable in this storage contract. A release without an attack
is ignored, another press closes the previous interval, and a held key is closed
visually at the session end. The raw storage is not rewritten by these policies.

The review timeline uses 160 pixels per second and 24 pixels per pitch row,
with pinned pitch labels and horizontal scrolling following the playhead.
Only the visible timeline is drawn. Piano key colors stay the same in both themes.

File saving/loading and chord naming are deferred.
Recordings stay in memory; starting another session replaces the current review.

## Validation

Use C:\Users\Policarp\.dotnet\dotnet.exe (SDK 10.0.102) with DOTNET_ROOT and
DOTNET_ROOT_X64 set to that installation. Build/test commands require the approved
execution path in the current Codex sandbox; ordinary sandbox execution previously
failed for Git diff and .NET even though the same commands succeed outside it.

- dotnet build Orchid.slnx: zero warnings/errors.
- dotnet test --solution Orchid.slnx --no-build: three existing failures in
  MusicalNoteTests; UI and Infrastructure suites pass.
- UI: 25 passed, including live note/release state and fixed timeline geometry.
- Infrastructure: 52 passed, one expected platform skip; live notifications detach at Stop.
- Physical input: enumerated 0: 5704PIA-DK; no physical notes supplied during check.
- Native window: Start, elapsed time, Stop and review state checked with Computer Use.

Next manual check: Start session, play/release C4 and F-sharp, hold overlapping
keys, Stop, then seek through the resulting notes and an exact release. Confirm
click tempo/accent and that a second Start obtains fresh MIDI/audio resources.
