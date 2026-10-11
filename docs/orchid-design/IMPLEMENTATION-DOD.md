# Orchid design: implementation gaps and Definitions of Done

Assessment date: 2026-10-06. This describes the inspected working tree, including
unfinished local edits. An unchecked item is a requirement, not implemented behavior.

## Scope and feasibility

The [UI specification](UI-SPEC.md) is implementable. Use Workspace as the primary
reference; Practice is an alternative arrangement of the same features. The HTML
is a preview, not the production application or an authoritative session engine.

The first delivery targets Windows, as agreed in the development conversation.
The handoff selects shared .NET MAUI C#/XAML UI and names macOS, Android and iOS
as additional targets. Those require separate platform adapters and acceptance
checks; a shared view is not proof that MIDI and audio work on another platform.
MAUI supports these targets, with macOS using Mac Catalyst. See the
[official platform documentation](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0).
The timeline and keyboard can use the supported
[GraphicsView/IDrawable canvas](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/graphics/?view=net-maui-10.0).

The selected-time panel supersedes the older closest-grid comparison panel in
`docs/ui-mvp-philosophy.md`. First delivery needs raw held-key state, time and
metronome position. Error scoring, reference comparison, chord recognition,
nearest-grid analysis, note editing and zoom are not prerequisites.

## Existing implementation and gaps

| Area | Present in code | Missing for the design |
| --- | --- | --- |
| Pitch | Musical definitions, signed intervals, MIDI-number conversion | Absolute key identity retained in recorded events and review results |
| Metronome | Absolute deadlines, sequential beats, overload Critical event, strong/weak audio API | Shared session origin, separately configured pulse unit, measure-correct accents for each pulse |
| Audio | Reusable Windows NAudio WASAPI click output | Production UI lifecycle/error handling; other-platform backends |
| MIDI | Key value and converter/provider | Device enumeration, selection, connection, live Note On/Off delivery and disconnect handling |
| Recording | `KeyInteraction`, `KeyPressTiming`, new in-memory `TimingStorage` draft | Exact per-event timestamps, identity/channel/velocity, complete press/release intervals and session boundaries |
| Time | Core uses `System.TimeProvider`; Application has `ITimeProvider` | One session origin and a connected implementation; review position independent of live wall-clock time |
| Files | Requirements and example filename in the mockup | Versioned format, writer, reader, save/load results and legacy-format fixtures |
| UI/review | Interactive HTML and images | MAUI executable, real session states, interval queries, playback cursor and drawing/hit testing |

### Concrete blockers in the inspected draft

- `TimingStorage` reads the current time but retains only the first timestamp of
  each 20 ms bin. Individual `KeyPressTiming` values have no timestamp. Two events
  10 ms apart therefore cannot be restored at their observed offsets. A bin's
  fixed duration is not a note's held duration. Keep exact events first; batching
  is acceptable only when it preserves every event's timestamp and order.
- Relative `Tone` values need the original `HomeSound` to recover an absolute
  pitch. The UI also needs the physical MIDI key number. Preserve that identity;
  deriving relative intervals remains useful separately.
- `MusicPlayThrough` always accents every `BeatsPerMeasure` clicks. That works
  for the matching beat-unit example, but not for a separate eighth-note pulse
  in 4/4, which needs eight clicks per measure. BPM is also duplicated between
  the score and a constructor argument, allowing contradictory settings.
- The preview uses `notes.find(...)`, so it only displays one held note. Its
  beat marks use quarter-note spacing even when pulse changes. A note click
  seeks to attack plus 1 ms. Production acceptance follows the specification:
  all held keys, actual configured click marks and exact attack seeking.
- The old note about no storage implementation is outdated: a draft now exists,
  but it does not yet supply the recording model required here.

Windows SDK 10.0.102 was used for a current solution build during this assessment.
It failed with CS1503 in `KeyListener.cs`: `Tone` is passed where `KeyPressTiming`
is required; and CS0266 in `TimingStorage.cs`: an enumeration of `TimeBin` is
returned as `IEnumerable<Tone>`. MIDI tests also still use the previous listener
and storage contracts. No production code was changed or tests claimed green
as part of this review.

## Required data

These are semantic requirements, not prescribed class names or a schema.
An append-only ordered event list and a session header are sufficient initially.

| Data | Minimum contents | Why it is needed |
| --- | --- | --- |
| Session configuration | BPM, pulse denominator, meter numerator/denominator | Reproduce click cadence, measure positions and grid after loading |
| Session boundaries | A shared monotonic origin while live; elapsed duration when stopped | Preserve initial/trailing silence, empty recordings and the full slider range |
| MIDI observation | Session-relative offset, note number, channel, Press/Release, velocity, stable event order | Recover exact attacks, releases, physical keys and overlapping observations |
| Pitch reference | `HomeSound` if relative tones are saved | Recover absolute pitches from interval-based records; unnecessary when only absolute MIDI pitches are stored |
| Derived note interval | Note identity, attack, release, press velocity, closure reason when synthesized at Stop/disconnect | Draw blocks, show duration and query held keys; derive from events rather than snapping timing |
| Log envelope | Format version, timestamp unit, configuration, duration, event data | Validate and read recordings without guessing units or semantics |
| Presentation state | Device availability, session state, playhead, viewport, review grid, selected note, file/save/error state | Drive controls and feedback consistently without treating UI selections as raw capture data |

Store elapsed offsets in an explicitly documented unit and preserve the chosen
clock's resolution. Timestamp observations at the input boundary, before queued
storage or UI work. UI refresh rate must not determine recording accuracy.
The observed input timestamp is not a measurement of audio-output latency.

Names, note count, duration of each note, measure/beat at the playhead, key
highlights and drawing coordinates are derived. Do not create independent saved
copies of these values that can disagree with the event data. Total session
duration is stored because it includes silence after the last event.

If two channels hold the same physical pitch, releasing one channel must not
clear the key while the other is still held. Define and test repeated Press,
unmatched Release and equal-timestamp ordering. Do not silently infer chords or
make up release observations. Sustain sound and physical key state remain
different concepts.

## Definitions of Done

### P0. Review the UI independently of the recording backend

This stage can start immediately. D0-D6 are integration requirements, not blockers
for a visual prototype. Use one clearly identified sample session and simple
presentation-owned data; do not bind the prototype to unfinished capture contracts
or introduce fake production MIDI/storage services.

- [x] The prototype launches independently of the current Core/Application build.
- [x] A sample session drives timeline blocks, elapsed/total time and the keyboard.
  The UI identifies this as demo data, without claiming real device or save success.
- [x] Seek, note selection, Play/Pause and Rewind use one position. Silent gaps,
  exact releases and overlapping notes agree across the timeline and keyboard.
- [x] The wide/narrow layouts can be inspected against the handoff. Demo setup
  controls affect only the sample preview, with hardware/file actions clearly unavailable.
- [ ] The completed prototype is reviewed separately from connecting MIDI,
  session lifecycle and persistence. Adapter work happens when those contracts stabilize.

Implemented in [Orchid.UiPreview](../../Orchid.UiPreview/README.md). Windows SDK
10.0.102 built the independent project; 16 AAA MSTest cases passed. Native-window
checks covered exact attack selection, silence, F-sharp, overlapping B4/C5,
Play/Pause/Rewind, theme switching and narrow reflow. Startup/crash diagnostics
persist outside the temporary launcher folder. This verifies the prototype;
integration acceptance and user review remain separate.

### D0. Restore a coherent foundation

- [ ] Listener, event records, storage and all consumers agree on one contract.
- [ ] Current Windows solution builds without errors; nullable checks stay enabled.
- [ ] Relevant MSTest suites pass with explicit AAA blocks. Platform skips and
  hardware checks are reported separately.
- [ ] No input timing is lost through 20 ms grouping; a regression check stores
  events at 100 ms and 110 ms and restores both distinct offsets.

### D1. Preserve raw observations and reconstruct held intervals

Owner: musical/session data in Core/Application; no UI or device-library types.

- [ ] Every supported input observation retains the required identity, timing,
  interaction, channel, velocity and order.
- [ ] Press/Release are paired by key and channel. The pairing policy for
  malformed/repeated events is explicit and tested.
- [ ] `HeldAt(t)` returns every interval satisfying `attack <= t < release`.
  Exact release, silent gaps, overlapping pitches and multiple channels pass.
- [ ] Stop closes remaining held keys at session end, preserving that the release
  was synthesized by session closure rather than received from the device.
- [ ] A new session has fresh capture data; previous recordings remain available
  as stable review snapshots rather than mutable live collections.

### D2. One session clock and correct pulse semantics

- [ ] Capture offsets, metronome scheduling and the live elapsed-time display
  share one origin. The first note never resets it.
- [ ] BPM and pulse come from one session configuration. Review uses the recorded
  configuration, not settings changed for the next session.
- [ ] Metronome click duration, quarter-note duration and measure duration use:
  `pulseMs = 60000 / BPM`, `quarterMs = pulseMs * pulseDenominator / 4`,
  `measureMs = quarterMs * 4` for the initial 4/4 scope.
- [ ] The supported pulse options produce the expected counts below. Strong
  clicks occur at measure starts, while quarter-beat labels and click marks are
  distinguished for non-quarter pulses.
- [ ] Review subdivisions alter only display. They never change capture or tempo.
- [ ] Beat processing over one interval writes Critical, and overdue beats are
  executed in order as currently requested. Scheduled grid time and actual
  delayed click delivery are not falsely presented as the same measurement.
- [ ] Starting another session resets time and accent position; two live starts
  cannot run concurrently.

At 120 BPM in 4/4:

| Pulse | Click interval | Quarter duration | Measure duration | Clicks per measure |
| --- | ---: | ---: | ---: | ---: |
| 1/1 | 500 ms | 125 ms | 500 ms | 1 |
| 1/2 | 500 ms | 250 ms | 1000 ms | 2 |
| 1/4 | 500 ms | 500 ms | 2000 ms | 4 |
| 1/8 | 500 ms | 1000 ms | 4000 ms | 8 |
| 1/16 | 500 ms | 2000 ms | 8000 ms | 16 |

The click interval stays constant when BPM is constant: BPM counts the selected
pulse. Changing pulse changes its relation to a quarter note, not clicks/minute.

### D3. Receive real Windows MIDI input

Owner: platform-neutral input operations in Application; device adapter in Infrastructure.

- [ ] Enumerate inputs, refresh the list, select a real available device and open
  it. Start is unavailable without a usable selection.
- [ ] Deliver Press and Release observations with timestamps from the session
  clock. Normalize equivalent MIDI release messages at the adapter boundary.
- [ ] Unrelated MIDI messages do not create notes. No chord grouping is required.
- [ ] Live key state and attack count come from actual input, not demo content.
- [ ] Stop unsubscribes/stops input; late callbacks cannot mutate a closed session.
- [ ] Disconnect produces a visible result, preserves captured data and closes
  held intervals at a documented session-end timestamp.
- [ ] An adapter check with deterministic events and a real Windows device check
  verify press, release, overlap and disconnect. Hardware results are recorded.

MIDI note/channel/velocity fields follow the
[MIDI Association message definitions](https://midi.org/summary-of-midi-1-0-messages).

### D4. Session lifecycle and observable failures

- [ ] Ready, Recording, Review and Playing behavior is defined, with explicit
  device, save and error state; no state-machine framework is required.
- [ ] Start creates one session and coordinates capture/metronome lifetimes.
  A partial start failure releases acquired resources and reports the cause.
- [ ] While recording, configuration and conflicting load/playback actions are
  locked; elapsed time and received attack count update.
- [ ] Stop ends capture/metronome, closes held notes, fixes final duration and
  publishes a review snapshot; repeating Stop is harmless.
- [ ] Empty recordings retain their duration and show an explicit empty state.
- [ ] Device/audio/save failures are visible and never appear as successful
  recording or saving. Captured data remains available after a save failure.

### D5. Save and load a real Orchid log

Owner: application save/load behavior; filesystem/serialization in Infrastructure.

- [ ] The format is versioned and documents timestamp and MIDI channel units.
  It stores the required configuration, duration and ordered observations.
- [ ] Save followed by Load preserves pitches, event times, interactions,
  velocities, channels, duration and settings; no snapping or 20 ms merging.
- [ ] Stop attempts the specified automatic save. Filename/path and successful
  save status come from its actual result; failure offers retry with retained data.
- [ ] Missing, malformed, unsupported-version and valid-empty files produce
  distinct usable UI outcomes without replacing a good session with partial data.
- [ ] Note count means attacks/recorded intervals, not Press plus Release events.
- [ ] Initial/trailing silence survives a round trip. The last note's release is
  not used as an unconditional substitute for session duration.
- [ ] Legacy loading is only included once real example files and their format
  are available. Missing tempo/release information is shown as unavailable, never
  fabricated. The inspected tree contains no legacy reader or format fixtures.

A local versioned JSON file is a possible first implementation; choosing its
exact schema is a later implementation decision, not an existing format contract.

### D6. Review and visual playback from one position

- [ ] One review position in `[0, sessionDuration]` drives the slider, playhead,
  time display, current-moment panel and keyboard.
- [ ] Seek into a note, gap, exact attack/release and overlap updates all surfaces
  consistently. Clicking a note seeks to its exact attack, without an added 1 ms.
- [ ] Play advances from the current position; Pause freezes it; Rewind sets it
  to zero; reaching the end stops. Seeking during playback has an explicit policy.
- [ ] Playback position is derived from an elapsed clock and its starting offset;
  UI tick delays do not accumulate into playback drift or mutate recorded timing.
- [ ] Current details list actual held notes and MIDI numbers, attack/duration,
  measure and beat position. Gaps clear stale selected-note fields.
- [ ] Review-grid selection redraws subdivisions without changing the recording.

Here Playback means visual review, as implemented by the mockup. Audible replay
of piano notes is an unresolved additional requirement. If included, it needs a
note output/synthesizer contract and note lifecycle handling; `IBeatSoundPlayer`
only plays metronome clicks and cannot provide that behavior.

### D7. Implement the Windows MAUI workspace

- [ ] A real MAUI Windows executable composes the existing layers, with explicit
  ownership of device/audio lifetimes. Musical calculations stay outside views.
- [ ] Setup, recording/review status, file controls and errors use real application
  data. No fake successful save, fixed eight-second range or seeded demo notes
  appears in production empty states.
- [ ] Timeline blocks use attack/release offsets and absolute MIDI pitches,
  including semitones outside the demo's eight natural-note lanes.
- [ ] Time-to-coordinate mapping agrees with hit testing and viewport scrolling;
  playhead remains distinguishable above blocks and grid lines.
- [ ] Long recordings retain a readable scale and scroll. A five-minute fixture
  remains navigable; work is bounded by the visible viewport rather than creating
  every hidden note/grid element on each redraw.
- [ ] Click selection and timeline dragging do not interfere. Viewport navigation
  does not silently change the full-session playhead.
- [ ] Piano geometry has correct black-key groups and C octave labels; actual
  held keys are highlighted. The range adapts to data or provides navigation.
- [ ] Wide/narrow layouts, light/dark themes and OS text scaling keep controls,
  labels and seek access usable. Semantics do not depend on color alone; drawn
  controls have usable accessible descriptions and keyboard/touch interactions.
- [ ] MAUI SVG resources follow the
  [documented build conversion](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/images?view=net-maui-10.0),
  and the icon license is retained. Preview carousel/design-picker controls are
  not copied into product navigation.

### D8. Accept each additional platform separately

- [ ] The same session/review behavior and data format pass on the target platform.
- [ ] Its MIDI input and audio adapters, connection failure paths and device
  lifetimes work on real hardware; Core/Application stay platform-neutral.
- [ ] File selection/storage, permissions, packaging and suspend/resume behavior
  are verified on the platform, with recording stopped or continued explicitly.
- [ ] Narrow/touch/safe-area behavior is checked on actual target sizes. A desktop
  narrow screenshot does not count as mobile acceptance.
- [ ] Build/deployment prerequisites for that target are available; Windows-only
  NAudio output is not invoked on macOS/iOS/Android.

## Shared acceptance fixture

One session: 120 BPM, quarter pulse, 4/4, duration 1000 ms, channel 0.
Press C4/MIDI 60 at 100 ms, Press E4/64 at 300 ms, Release C4 at 400 ms,
Release E4 at 600 ms, Press G4/67 at 900 ms, then Stop at 1000 ms.
Retain an explicit synthesized closure for G4 at Stop.

| Position | Held physical keys |
| --- | --- |
| 0 ms | None |
| 100 ms | C4 |
| 300 ms | C4 and E4 |
| 400 ms | E4 |
| 600 ms | None |
| 900 ms | G4 |
| 1000 ms | None |

Run the same assertions before and after save/load, through direct seeking and
visual playback. Three note blocks remain at their original offsets, while
the timeline/slider include silence before the first attack and after releases.
Add a separate empty session, same-pitch multi-channel case and two events 10 ms
apart; the screenshot demo alone cannot verify these requirements.

## Delivery order and completion

P0 can deliver the visual prototype immediately, independently of D0-D6.
For integration, restore coherence (D0), preserve events/intervals (D1), then
establish the shared clock/pulse rules (D2). A fixture can drive review/UI work
(D5-D7) while real MIDI/lifecycle work (D3-D4) is implemented independently.
Prototype acceptance P0 does not mean recording is implemented. Final Windows
acceptance requires D0-D7 together; D8 is an explicit per-platform follow-up.

Each delivered behavior has a meaningful automated check in the existing AAA
MSTest style where possible. Rendering/interactions, actual MIDI and audio also
require the relevant Windows scenario checks. Build success alone does not mark
a recording/review scenario Done. Changes to the data model must verify all callers.

Before implementing legacy compatibility or audible note replay, define their
missing inputs/behavior. The alternative Practice layout and closest-grid
analysis do not block the Workspace first delivery.
