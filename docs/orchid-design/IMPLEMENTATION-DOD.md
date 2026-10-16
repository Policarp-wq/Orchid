# Orchid design: implementation gaps and Definitions of Done

Assessment date: 2026-10-15. This describes the inspected working tree, including
unfinished local edits. An unchecked item is a requirement, not implemented behavior.

Item states:

- `[x]` done and verified by the check named in the item.
- `[ ]` **Implemented, not accepted.** The behavior exists in code, but the acceptance
  named in the item (shared fixture, Windows scenario, hardware) has not been run.
- `[ ]` without that prefix: open.

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

`Orchid.Windows` (named `Orchid.UiPreview` until 2026-10-16) started as the independent
visual prototype (P0). It is the Windows head of the UI library `Orchid.Ui`, composes the
real MIDI input and metronome, and is the Windows workspace executable that D7 is accepted
against.

The selected-time panel supersedes the older closest-grid comparison panel in
`docs/ui-mvp-philosophy.md`. First delivery needs raw held-key state, time and
metronome position. Error scoring, reference comparison, chord recognition,
nearest-grid analysis and note editing are not prerequisites. Timeline zoom, the camera
and the grid element are specified in D10 and D12.

## Existing implementation and gaps

| Area | Present in code | Missing for the design |
| --- | --- | --- |
| Pitch | Musical definitions, signed intervals, MIDI-number conversion | Absolute key identity retained in recorded events and review results |
| Metronome | Absolute deadlines, in-order overdue beats, overload Critical event, strong/weak audio API, accent on the first of `pulseDenominator` clicks in 4/4 | Origin shared with capture, one BPM source, strong-beat pattern |
| Audio | Windows NAudio WASAPI click output, owned by `WindowsRecordingSession` | Audible check on hardware; other-platform backends |
| MIDI | DryWetMidi input, device enumeration/refresh/selection, NoteOn velocity 0 as Release, live note notifications | Channel/velocity, verified disconnect handling, callbacks outliving Stop, physical key-press check |
| Recording | `KeyInteraction`, `KeyPressTiming`, in-memory `TimingStorage` with 20 ms bins | Exact per-event timestamps, identity/channel/velocity, intervals with closure reason in Core/Application |
| Time | `StopwatchTimeProvider`; capture and the elapsed display share the session origin | The metronome measures from its own start, not that origin |
| Files | Requirements and example filename in the mockup | Write-ahead log while recording, own compact session format, recovery after a crash (D5) |
| Sustain pedal | Nothing; control changes are ignored as unrelated messages | CC64 capture, pedal intervals, sounding-versus-held display (D15) |
| UI/review | MAUI Windows workspace with real recording, review model in `PreviewSession`, timeline/keyboard drawings, strong-beats modal | See D6, D7, D10 and D12 |

### Limitations of the current code

- `TimingStorage` reads the current time but retains only the first timestamp of
  each 20 ms bin. Individual `KeyPressTiming` values have no timestamp. Two events
  10 ms apart therefore cannot be restored at their observed offsets. A bin's
  fixed duration is not a note's held duration. Keep exact events first; batching
  is acceptable only when it preserves every event's timestamp and order.
- Relative `Tone` values need the original `HomeSound` to recover an absolute
  pitch. The review rebuilds MIDI numbers as `60 + Tone * 2` from the fixed C4 home
  sound. The UI needs the physical MIDI key number; preserve that identity.
  Deriving relative intervals remains useful separately.
- `MusicPlayThrough` accents every `BeatsPerMeasure` clicks. `WindowsRecordingSession`
  sets `BeatsPerMeasure = pulseDenominator`, which gives the right counts for 4/4
  only and cannot express a strong-beat pattern. BPM is also passed both in the
  score and as a constructor argument, allowing contradictory settings.
- `Metronome` takes its start timestamp from its own `TimeProvider`, while capture
  uses the session's `StopwatchTimeProvider` origin. They start close together, but
  they are two origins.
- Press/Release pairing lives in `PreviewSession.LoadRecording` (UI layer). It pairs by
  MIDI number only, without channel, and does not record why an interval was closed.
- The current-moment panel shows the latest held note only, not every held key.
- The HTML mockup uses `notes.find(...)`, so it only displays one held note, and a
  note click seeks to attack plus 1 ms. Production acceptance follows the
  specification: all held keys and exact attack seeking.
- MIDI callbacks can overlap and outlive Stop (`DryWetMidiInput`, `WindowsRecordingSession`).

Build state on 2026-10-15: Windows SDK 10.0.102 builds `Orchid.slnx` with zero
warnings and errors. `dotnet test --solution Orchid.slnx --no-build`: 211 passed,
one expected non-Windows-only skip, no failures. `global.json` requires a .NET 10
SDK; an SDK 9 `dotnet` on `PATH` cannot build the solution.

## Required data

These are semantic requirements, not prescribed class names or a schema.
An append-only ordered event list and a session header are sufficient initially.

| Data | Minimum contents | Why it is needed |
| --- | --- | --- |
| Session configuration | BPM, pulse denominator, meter numerator/denominator | Reproduce click cadence, measure positions and grid after loading |
| Strong-beat pattern | One boolean per click of the measure; `true` plays the strong click. Stored with the session configuration | Reproduce which clicks were accented and mark strong beats when a loaded log is reviewed |
| Session boundaries | A shared monotonic origin while live; elapsed duration when stopped | Preserve initial/trailing silence, empty recordings and the full slider range |
| MIDI observation | Session-relative offset, note number, channel, Press/Release, velocity, stable event order | Recover exact attacks, releases, physical keys and overlapping observations |
| Pedal observation | Session-relative offset, channel, raw sustain value 0-127 (CC64), stable order with the notes | Recover pedal intervals and which notes sounded only because of the pedal |
| Pitch reference | `HomeSound` if relative tones are saved | Recover absolute pitches from interval-based records; unnecessary when only absolute MIDI pitches are stored |
| Derived note interval | Note identity, attack, release, press velocity, closure reason when synthesized at Stop/disconnect | Draw blocks, show duration and query held keys; derive from events rather than snapping timing |
| Log envelope | Format version, timestamp unit, configuration, duration, event data | Validate and read recordings without guessing units or semantics |
| Presentation state | Device availability, session state, playhead, viewport, review grid, selected note, file/save/error state | Drive controls and feedback consistently without treating UI selections as raw capture data |

Clicks per measure are `meterNumerator * pulseDenominator / meterDenominator`. In the
current 4/4 scope that is `pulseDenominator`. A meter and pulse for which this is not
a positive integer (3/4 with a whole-note pulse gives 0.75) is rejected.

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

Numbers are stable identifiers referenced from UI-SPEC, REMOTE-DOD and code; sections
are ordered by group, not by number.

| Group | Sections |
| --- | --- |
| Closed | P0, D0 |
| A. Capture and timing | D1, D2, D15 |
| B. Device and session | D3, D4 |
| C. Recording storage | D5 |
| D. Desktop review and workspace | D6, D17, D7, D10, D12 |
| E. Phones and tablets | D16, D8 |
| F. Technical quality | D9, D14, D11 |
| G. Follow-up | D13, D18, [Remote](REMOTE-DOD.md) |

## Closed

### P0. Review the UI independently of the recording backend (closed, superseded)

Delivered in `fb976b6` as an independent MAUI prototype driven by one demo session:
seek, exact note selection, Play/Pause/Rewind on one position, silent gaps, overlapping
notes, wide/narrow layouts and themes were checked in the native window. The project
has since been connected to real MIDI and the metronome and the demo session was
removed, so its prototype criteria no longer describe the code. The remaining
behavior is accepted under D6, D7 and D10.

### D0. Restore a coherent foundation (closed)

- [x] Listener, event records, storage and all consumers agree on one contract.
  Checked by the solution build below. The contract itself is lossy; that is D1.
- [x] Current Windows solution builds without errors; nullable checks stay enabled.
  Windows SDK 10.0.102, 2026-10-15, zero warnings and errors.
- [x] Relevant MSTest suites pass with explicit AAA blocks. Platform skips and
  hardware checks are reported separately. 211 passed, one non-Windows-only skip.

The 20 ms grouping regression check moved to D1.

## A. Capture and timing

What is recorded and when: raw observations, the shared clock, the metronome and the pedal.

### D1. Preserve raw observations and reconstruct held intervals

Owner: musical/session data in Core/Application; no UI or device-library types.
The pairing currently in `PreviewSession.LoadRecording` moves here.

- [ ] Every supported input observation retains the required identity, timing,
  interaction, channel, velocity and order.
- [ ] No input timing is lost through 20 ms grouping; a regression check stores
  events at 100 ms and 110 ms and restores both distinct offsets.
- [ ] Press/Release are paired by key and channel. The pairing policy for
  malformed/repeated events is explicit and tested.
- [ ] `HeldAt(t)` returns every interval satisfying `attack <= t < release`.
  Exact release, silent gaps, overlapping pitches and multiple channels pass.
- [ ] Stop closes remaining held keys at session end, preserving that the release
  was synthesized by session closure rather than received from the device.
- [ ] A new session has fresh capture data; previous recordings remain available
  as stable review snapshots rather than mutable live collections.

### D2. One session clock and correct pulse semantics

Owner: timing and pulse rules in Core/Application; the clock implementation in Infrastructure.

- [ ] Capture offsets, metronome scheduling and the live elapsed-time display
  share one origin. The first note never resets it.
- [ ] The count-in plays whole measures with the session's pattern before the origin, so
  bar 1 of the recording is bar 1 of the grid. Open decision: notes played during the
  count-in. Proposal: keep them at negative offsets so a pickup is not lost, and let the
  review range start at the earliest event.
- [ ] A stalled UI thread (test hook blocking it for 200 ms) does not change recorded
  offsets or the metronome schedule.
- [ ] BPM and pulse come from one session configuration. Review uses the recorded
  configuration, not settings changed for the next session.
- [ ] Metronome click duration, quarter-note duration and measure duration use:
  `pulseMs = 60000 / BPM`, `quarterMs = pulseMs * pulseDenominator / 4`,
  `measureMs = quarterMs * 4` for the initial 4/4 scope.
- [ ] **Implemented, not accepted.** The supported pulse options produce the expected
  counts below. Unless the user selects otherwise, strong clicks occur at measure
  starts, while quarter-beat labels and click marks are distinguished for non-quarter
  pulses. Counts and the first-click accent are covered by
  `WindowsRecordingSessionTests.Run_AccentsFirstPulseOfEachFourQuarterMeasure`.
- [ ] The strong-beat pattern has exactly one flag per click of the measure (see
  Required data), and repeats every measure. Any subset is valid: one beat, several
  beats, every beat or none. The default is beat 1 strong. A pattern of another
  length is rejected before any sound plays; it is never trimmed or padded.
- [ ] The metronome plays the strong click on flagged beats and the weak click on
  the others for every pulse option. A test with a recording sound player covers
  each pulse option with the default pattern and with non-default patterns
  (beat 3 only, every beat, no beat).
- [ ] The pattern belongs to the session configuration: fixed while recording,
  reset to the default when the pulse changes, and review uses the pattern the
  recording was made with, not the one selected for the next session.
- [ ] The measure start mark is independent of the flags: a weak first beat is
  still the start of the measure.
- [ ] Review subdivisions alter only display. They never change capture or tempo.
- [ ] The metronome plays without a time limit until Stop. Deadlines come from the beat
  index, so there is no drift, and nothing grows per beat: a test runs 100,000 beats on a
  fake clock and checks the deadlines and that allocations stay flat; a two-hour Windows
  run keeps clicks on schedule with the window minimized.
- [x] Beat processing over one interval writes Critical, and overdue beats are
  executed in order. Covered by `MetronomeTests`.
- [ ] Scheduled grid time and actual delayed click delivery are not presented as the
  same measurement.
- [ ] **Implemented, not accepted.** Starting another session resets time and accent
  position; two live starts cannot run concurrently. Each Start creates a new
  `WindowsRecordingSession`, which refuses a second run; the page refuses Start while
  a session is active.

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

### D15. Sustain pedal

Owner: capture in D1/D3, storage in D5, display in D6/D7. Physical key state and pedal-sustained
sound stay different concepts (UI-SPEC "Pitch axis and current-moment panel").

- [ ] Sustain (CC64) is captured like notes: session offset at the input boundary, channel,
  raw value 0-127 and order. The pedal is down at 64 and above, up below. Raw values are
  kept, so half-pedaling can be shown later. Sostenuto and soft pedals are out of scope.
- [ ] Pedal intervals are derived like note intervals; a pedal still down at Stop closes at
  the session end, marked as closed by the session.
- [ ] `HeldAt(t)` stays physical. A separate `SoundingAt(t)` adds the notes whose key was
  released while the pedal was down, until the pedal goes up or the same key is struck again.
- [ ] Review shows a pedal strip under the timeline (pedal-down spans aligned with time),
  and draws the pedal-sustained part of a note as a lighter tail after its key release.
  The Held slot lists held keys; sustained notes are shown separately and marked as such.
- [ ] The live view shows the pedal state next to the Held slot.
- [ ] Tests: pedal down before a press, release under the pedal, the same key struck again
  under the pedal, pedal up ending tails, several channels, pedal held at Stop, and a
  save/load round trip. A real Windows check with a pedal is recorded.

## B. Device and session

Real MIDI input and the session lifecycle around it.

### D3. Receive real Windows MIDI input

Owner: platform-neutral input operations in Application; device adapter in Infrastructure.

- [ ] **Implemented, not accepted.** Enumerate inputs, refresh the list, select a real
  available device and open it. Start is unavailable without a usable selection.
- [ ] Deliver Press and Release observations with timestamps from the session
  clock. Normalize equivalent MIDI release messages at the adapter boundary.
  Normalization of NoteOn with velocity 0 is implemented and tested; timestamps still
  pass through the 20 ms bins (D1).
- [ ] **Implemented, not accepted.** Unrelated MIDI messages do not create notes
  (`DryWetMidiInputTests.Run_IgnoresNonNoteEvents`). No chord grouping is required.
  The sustain controller (CC64) is no longer unrelated: it is captured (D15).
- [ ] **Implemented, not accepted.** Live key state and attack count come from actual
  input, not demo content.
- [ ] Stop unsubscribes/stops input; late callbacks cannot mutate a closed session.
  Unsubscribing is implemented; late callbacks are a known gap.
- [ ] Disconnect produces a visible result, preserves captured data and closes
  held intervals at a documented session-end timestamp.
- [ ] An adapter check with deterministic events and a real Windows device check
  verify press, release, overlap and disconnect. Hardware results are recorded.

MIDI note/channel/velocity fields follow the
[MIDI Association message definitions](https://midi.org/summary-of-midi-1-0-messages).

### D4. Session lifecycle and observable failures

Owner: lifecycle coordination in Application; device and audio lifetimes in Infrastructure.

- [ ] Ready, Recording, Review and Playing behavior is defined, with explicit
  device, save and error state; no state-machine framework is required.
- [ ] **Implemented, not accepted.** Start creates one session and coordinates
  capture/metronome lifetimes. A partial start failure releases acquired resources and
  reports the cause (`WindowsRecordingSessionTests`).
- [ ] Ready, Count-in, Recording and Review follow each other. Start runs the count-in
  set in Ready (default one bar, "off" allowed); Stop during the count-in returns to Ready
  without creating a session.
- [ ] While recording, configuration and conflicting load/playback actions are
  locked and elapsed time updates. The live view has no note counter; Review shows the
  note count. Locking and elapsed time are implemented.
- [ ] Stop ends capture/metronome, closes held notes, fixes final duration and
  publishes a review snapshot; repeating Stop is harmless.
- [ ] Empty recordings retain their duration and show an explicit empty state.
  Duration is retained; the empty state is missing.
- [ ] Device/audio/save failures are visible and never appear as successful
  recording or saving. Captured data remains available after a save failure.

## C. Recording storage

Nothing recorded is lost, however long the recording and whatever happens to the app.

### D5. Recording storage: write-ahead log, own format, crash safety

Owner: application save/load behavior; filesystem and serialization in Infrastructure.

Recording writes a write-ahead log (WAL) while playing; after Stop the WAL is compacted into
the Orchid session file, an own versioned format optimized for size and loading speed.

- [ ] Every observation (notes and pedal) is appended to the WAL at the input boundary,
  before any UI work. The WAL starts with a header: format version, timestamp unit,
  session configuration and strong-beat pattern. Records carry a checksum, so a torn last
  record is detected.
- [ ] An application crash or kill loses no recorded data: each event reaches the OS (a
  completed write) within a bounded delay (proposal: 100 ms batches). An OS crash or power
  loss loses at most the last flush interval (proposal: an fsync every 1 s). Both bounds
  are documented and tested by killing the process during a recording.
- [ ] On the next start an unfinished WAL is detected and the recording is offered for
  recovery: events, configuration and duration up to the last complete record, with held
  notes and pedal closed at the last recorded time and marked as closed by recovery.
- [ ] After Stop the WAL is compacted into the session file: written to a temporary file,
  read back and verified, then renamed into place; the WAL is deleted only after that.
  A failure keeps the WAL, shows the error and offers retry.
- [ ] The session file is versioned and documents its units. It stores the configuration,
  the strong-beat pattern, the duration and the ordered observations losslessly
  (proposal: delta-encoded timestamps, variable-length integers and block compression).
  A log without a strong-beat pattern loads with the default, and the version documents that rule.
- [ ] A recording of at least one hour is guaranteed on every target. Beyond that it may
  continue without a fixed limit, bounded only by free disk space and the documented format
  limits. Low free space is warned about before it ends the recording, and the recording
  then ends cleanly as a closed session, never as a corrupt file.
- [ ] Sizes are measured and documented: WAL and session file per hour of the one-hour
  fixture, with a stated budget (proposal: session file at most 5 MB per hour of dense playing).
- [ ] Save followed by Load preserves pitches, event times, interactions, velocities,
  channels, pedal values, duration and settings; no snapping or 20 ms merging.
- [ ] Filename/path and the saved status come from the actual result; a failure offers
  retry with the retained data.
- [ ] Missing, malformed, unsupported-version and valid-empty files produce distinct
  usable UI outcomes without replacing a good session with partial data.
- [ ] Note count means attacks/recorded intervals, not Press plus Release events.
- [ ] Initial/trailing silence survives a round trip. The last note's release is not used
  as an unconditional substitute for session duration.
- [ ] Legacy loading is only included once real example files and their format are
  available. Missing tempo/release information is shown as unavailable, never fabricated.
  Export to a standard MIDI file is a possible later addition, not this format.

## D. Desktop review and workspace

The first delivery's screens. D7 is the shell; the timeline is D10 (behavior) and D12 (look), accepted together.

### D6. Review and visual playback from one position

Owner: held-key queries come from D1; the review position and playback state are
presentation state (currently `PreviewSession`).

- [ ] **Implemented, not accepted.** One review position in `[0, sessionDuration]`
  drives the overview strip, playhead, time display, current-moment panel and
  held-pitch marks.
- [ ] **Implemented, not accepted.** Seek into a note, gap, exact attack/release and
  overlap updates all surfaces consistently. Clicking a note seeks to its exact
  attack, without an added 1 ms.
- [ ] **Implemented, not accepted.** Play advances from the current position; Pause
  freezes it; Rewind sets it to zero; reaching the end stops. Seeking during playback
  stops playback at the new position.
- [ ] **Implemented, not accepted.** Playback position is derived from an elapsed clock
  and its starting offset; UI tick delays do not accumulate into playback drift or
  mutate recorded timing (`PreviewSessionTests`).
- [ ] Current details list actual held notes and MIDI numbers, attack/duration,
  measure and beat position. Gaps clear stale selected-note fields. Only the latest
  held note is shown today. The held notes use the Held slot (UI-SPEC "Held slot"): a chord
  name when a recognizer (D13) supplies one, otherwise the notes alone; the slot scrolls
  inside its panel (D7).
- [ ] **Implemented, not accepted.** Review-grid selection redraws subdivisions
  without changing the recording.

Here Playback means visual review, as implemented by the mockup. Audible replay
of piano notes is an unresolved additional requirement. If included, it needs a
note output/synthesizer contract and note lifecycle handling; `IBeatSoundPlayer`
only plays metronome clicks and cannot provide that behavior. Audible note replay is D18.

### D17. Metronome during review playback

Requested in the window review of 2026-10-16. Playing back a recording can sound the metronome
of that recording, so the timing can be judged by ear as well as by eye.

- [ ] A toggle in the review toolbar turns the metronome on and off for playback; it is off by
  default and its state is visible. Changing it during playback takes effect at the next click.
- [ ] Clicks use the recording's own settings (BPM, pulse, meter, strong-beat pattern), not the
  ones selected for the next session, and the strong click plays on flagged beats (D2).
- [ ] Clicks are scheduled from the playback position on the session grid (D10 M6): a click at
  grid time `t` sounds when the position reaches `t`. Seeking, Pause, Rewind and the end of the
  recording stop pending clicks; Play from any position starts at the next grid click, without
  a click for the position itself unless it is on a beat.
- [ ] Deadlines come from the beat index and the playback clock, never from UI ticks, so a
  stalled UI thread neither delays nor bunches clicks (same rule as D2).
- [ ] The click output is the existing `IBeatSoundPlayer` behind a UI contract (Orchid.Ui stays
  backend-free); the fakes get a recording click player for tests.
- [ ] Tests: click times for every pulse option from position 0 and from a mid-measure
  position, strong clicks from a non-default pattern, no clicks after Pause, seek or end, and
  no drift over 3,600 s. A Windows check is recorded by ear on a real output device.

### D7. Desktop workspace shell

- [ ] A real MAUI Windows executable composes the existing layers, with explicit
  ownership of device/audio lifetimes. Musical calculations stay outside views.
  Open: the page opens `WindowsRecordingSession` itself, and measure/beat are
  computed in `WorkspacePage.UpdatePosition`.
- [ ] Setup, recording/review status, file controls and errors use real application
  data. No fake successful save, fixed eight-second range or seeded demo notes
  appears in production empty states. Open: `PreviewSession` still defaults to an
  8 s duration with seeded notes until the first `LoadRecording`.
- [ ] **Implemented, not accepted.** Timeline blocks use attack/release offsets and
  absolute MIDI pitches, including semitones outside the demo's eight natural-note
  lanes. Pitches are exact only once D1 keeps the MIDI number.
- [ ] **Implemented, not accepted.** The setup panel shows only a one-line summary of
  the strong beats and an Edit button. Edit opens a modal with the beat bars from
  UI-SPEC "Shared components": one column per beat of the measure, a tall bar is a
  strong beat, any beat may be strong, and the count follows the meter (3, 4, 6,
  8 ...). Edit is disabled while recording. Changing the pulse resets the pattern to
  the default and rebuilds the bars. The modal and the bars hold no session data and
  can be hosted unchanged on another screen or device. Review marks strong beats more
  heavily than other beats and the legend names them.
- [ ] The strong-beats modal traps focus and closes on Escape; the beat bars wrap
  onto a second row when a long measure does not fit.
- [ ] The bottom piano keyboard is removed from review and from the live view; the
  pitch label column of D10 M4 takes over naming and held-key highlighting.
- [ ] The interface never jumps. Only the window size, the wide/narrow reflow and an
  explicit user action (opening a panel or a modal) change geometry. Changing content
  never resizes or moves the timeline or a control: held notes from 0 to 10, chord names,
  times, bar and beat, file names and status messages. Panels have a fixed or bounded size
  and scroll inside, numbers use tabular figures in a reserved width, and long text is
  truncated with a tooltip. Check: a Windows scenario seeks the many-held-notes fixture
  through 0 to 10 held keys and in the live view, and the bounds of the timeline, the
  overview and the toolbar stay identical.
- [ ] Wide/narrow layouts, light/dark themes and OS text scaling keep controls,
  labels and seek access usable. Semantics do not depend on color alone; drawn
  controls have usable accessible descriptions and keyboard/touch interactions.
- [ ] MAUI SVG resources follow the
  [documented build conversion](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/images?view=net-maui-10.0),
  and the icon license is retained. Preview carousel/design-picker controls are
  not copied into product navigation.

### D10. Timeline mechanics (group 1 of 2: how it behaves)

The visual half is D12. Both come from one review of the timeline and are accepted
together. The pure functions below (camera, range, scale, grid) can be built and tested
now; the look waits for the mockup agreed in D12.

Words used here. **Camera**: the visible window of the timeline, a time span and a pitch
span. **Cursor**: the position control, meaning the overview strip and the playhead.
**Range**: the lowest and highest recorded pitch and the duration of a recording.
**Scale**: pixels per second and pixels per pitch lane.

#### M1. Panning

Reported: wheel input over the timeline is captured but does not move it.

Status in the app: reproduced and fixed for the time axis. A vertical wheel notch pans 80 px, a
mouse drag on the timeline pans 1:1 (a click without movement still selects a note), horizontal wheel and the
scroll bar still work, and the page keeps scrolling outside the timeline. The platform scroll viewers
swallow the vertical wheel and MAUI's pointer gestures do not deliver pointer moves, so the page
listens to the WinUI pointer events directly. Open: the items below that are not ticked.

- [ ] A vertical wheel over the timeline pans the time axis horizontally, a fixed
  distance per notch (proposal: 0.5 s). Shift+wheel pans the pitch lanes vertically
  when the lane range exceeds the viewport. Horizontal wheel and trackpad horizontal
  swipes pan time. Ctrl+wheel zooms (M5) and never scrolls the page.
- [ ] The wheel never merely locks: over the timeline, every wheel event either moves
  the timeline or is ignored at its limit. The page does not scroll under the pointer
  while it is over the timeline; outside the timeline the page scrolls normally.
  Open decision: whether scroll chains to the page at the limit. Proposal: no chaining.
- [ ] Press-and-drag pans in both axes like a map: the content follows the pointer 1:1,
  horizontally in time and vertically in pitch. Vertical movement is ignored while the
  whole pitch range fits. Works with the left mouse button, one finger on a touch screen
  and a pen. The cursor shows grab/grabbing, and the page never scrolls instead of the
  timeline. A dragged camera is Free (M2). Proposal: a touch flick continues with
  deceleration; the mouse stops on release.
- [ ] Wheel, drag, overview and scrollbar panning share one camera offset, so drawing,
  pitch labels and hit testing agree after any of them.
- [ ] Click selection and dragging do not interfere: a movement under 4 px is a click and
  selects the note, a longer one is a drag and never selects, also after a pan. Panning
  never changes the full-session playhead.
- [ ] Keyboard: with the timeline focused, Left/Right pan, Home/End jump to the start or end.
- [ ] Verified on Windows with a notched mouse and with a precision touchpad.

#### M2. The cursor moves the camera

Reported: the cursor (the position slider) is useless. It moves the playhead, but the camera only
moves when the playhead leaves the visible window, so scrubbing never shows what surrounds the position.

The position slider is replaced by an **overview strip** under the timeline: the whole recording
in miniature, the visible part as a frame, and the playhead.

- [ ] The overview shows, for every pixel column, the lowest and highest pitch sounding in
  that time span, so silences and register changes are visible. The columns are computed
  once when a recording is loaded and when the strip width changes (one pass over the
  notes); drawing costs one column per pixel, independent of the number of notes. Per
  tick only the frame and the playhead change; if the D9 baseline shows the columns are
  too costly, they move to a separate view that redraws only on load and resize.
- [ ] A click or drag outside the frame seeks there (Locked camera); dragging the frame
  pans the camera (Free). The strip is the session-position control for accessibility:
  it has an accessible name and value, and Left/Right/Home/End move the position.
- [ ] Dragging the playhead in the overview moves the camera continuously with it. The playhead
  stays at a fixed screen anchor (proposal: the middle of the viewport, to confirm)
  while the content moves under it. No jumps, and the camera is never more than one
  frame behind the slider.
- [ ] Two camera modes. **Locked**: the camera follows the cursor; it is the default and is
  the mode during scrubbing, playback, recording-to-review and after any seek.
  **Free**: the camera stays where the user panned it and the cursor may be off screen.
  A manual pan switches to Free; a visible Follow control, Play, Rewind, seeking and a click on
  a note switch back to Locked. The control shows which mode is active.
- [ ] Near the start and the end of the recording the camera stops at the edge, so the
  playhead leaves the anchor, and it returns to the anchor when the cursor moves away.
- [ ] The camera offset is a pure function of position, anchor, scale, range and viewport
  width, with tests for the start, the middle, the end and an empty recording.
- [ ] The overview and the camera never disagree about the position, and a click on the
  timeline ruler or on a note moves both.

#### M3. Recording and review are different modes

Reported: showing the timeline while recording makes no sense. Notes appear only after Stop.

- [ ] While recording, the timeline, the overview, Play, Rewind and the grid controls are
  not shown (hidden, not disabled). The live view replaces them (UI-SPEC "Live view"): the
  metronome is the dominant element (beat bars with the current beat lit, bar and beat
  number), held keys or the chord are the second, large element, elapsed time is small in
  the header next to the locked settings and Stop. No keyboard, no note counter and no
  input activity light; a MIDI disconnect appears as a banner.
- [ ] Nothing for the timeline is computed or drawn during recording.
- [ ] On Stop the review appears with the recording fitted (M4) and the cursor at the
  start. An empty recording shows the explicit empty state. Starting a new session
  replaces the review view with the live view again.
- [ ] Switching mode never touches captured data or timing.

#### M4. Range and scale come from the recording

- [ ] The time range is 0 to the duration. The pitch range is the lowest to the
  highest recorded pitch plus one lane of margin on each side (margin to confirm). The
  fixed minimum range of C4 to C5 and the hard-coded 160 px per second and 24 px per
  lane stop being the scale; they remain only as the fallback for an empty recording.
- [ ] One pure function turns notes, duration, viewport size and platform limits into
  the initial scale and ranges. If the whole recording fits at or above a minimum
  readable scale it is fitted; otherwise the view starts at that minimum scale at the
  beginning. If the pitch range does not fit at the minimum lane height, lanes are at
  that minimum and the pitch axis scrolls (M1, Shift+wheel). Thresholds to agree;
  proposal: 40 px per second and 4 px per lane (8 px did not fit A0 to C8 in the default
  1240 x 900 window; thin lanes draw octave bands).
- [ ] A Fit command (a button and Ctrl+0) restores this initial view at any time.
- [ ] The pitch axis has no keyboard. A label column at the left names the lanes: every
  lane when lanes are tall, only C otherwise, and pitches held at the position are marked
  there in the held color at any lane height.
- [ ] Three pitch-axis modes, all through the one pitch-to-y mapping of M5 that drawing,
  labels, hit testing and the overview share, tested over any range including A0 to C8:
  - **Fit** (default): the recorded range with octave bands when lanes are thin and
    black-key rows when they are tall (D12 V1).
  - **Zoomed**: after a pitch zoom, a narrow full-range strip next to the labels shows the
    visible window, and counts of notes above and below it ("▲ 12 notes above") appear.
  - **Fold empty pitches** (toolbar toggle): only pitches that occur in the recording get
    a lane, with a line at each octave change. Lane distance then no longer equals the
    interval, so the toggle stays visibly on while active.
- [ ] A recording spanning A0 to C8 opens fully visible without vertical scrolling; a
  one-octave recording uses tall lanes; the five-minute fixture stays readable and
  navigable. All are covered by the fixtures below.

#### M5. Zoom in and out

- [ ] Zoom works on time and on pitch independently. Ctrl+wheel zooms time,
  Ctrl+Shift+wheel zooms pitch, a pinch on a touchpad or touch screen zooms time, and
  toolbar buttons zoom in, zoom out and fit for each axis.
- [ ] Zoom keeps the point under the pointer fixed. Button and keyboard zoom keep the
  cursor fixed in Locked mode and the middle of the viewport in Free mode.
- [ ] Limits: the lower limit is the fit view but never below the minimum readable
  scale of M4, the upper limit is a stated maximum (proposal: 2,000 px per second, half a
  millisecond per pixel, and 64 px per lane). Going past a limit is ignored without an error.
- [ ] One transform object (time to x, pitch to y, scale, offset) is the only source for
  drawing, hit testing, pitch labels, the ruler, the playhead, the grid and the overview
  frame. Fold mode is another pitch-to-y function of the same transform. No code uses a
  hard-coded scale constant. The tests that pin 160 px per second and 24 px lanes become
  tests of that transform with explicit scales, plus scale-independence checks.
- [ ] The scroll extent is virtual: a logical range in doubles with its own scrollbar,
  not an element sized to the recording, so the length of a recording never enters the
  size of any view or drawing surface. Win2D refuses surfaces wider than 16384 px: a
  `BoxView` extent crashed the app after about 100 s of recording (now a `Grid`, which
  still grows with the duration). Zoom multiplies the extent: 600 s at 2,000 px per
  second is 1.2 million px. Verified in the real window with a 600 s fixture at the
  maximum zoom, not only in unit tests.
- [ ] Zoom never changes stored times, the cursor position or the selected note.

#### M6. The grid is its own element

The grid decides how timing is judged, so it is a component with its own model, tests and
settings, not a few lines inside the timeline drawing.

- [ ] The model is a pure component with no drawing or UI types. From the session
  configuration (BPM, pulse, meter, strong-beat pattern, review subdivision), a visible
  time range and the scale, it returns grid lines: time, level (measure, strong beat, beat,
  subdivision) and an optional label (measure number, beat number).
- [ ] A line's time is computed from its index (line n at n times the interval), never by
  adding up intervals, so a one-hour recording has no drift. The grid origin is the
  session origin; the first note does not reset it.
- [ ] Density follows the scale. A level is left out when its lines would be closer than a
  minimum spacing (proposal: 12 px). Zooming out leaves measures, zooming in adds beats,
  then subdivisions. Ruler labels thin out the same way.
- [ ] One source for everything that talks about beats: drawing, ruler labels, the
  measure and beat readout in "At this moment", and later the nearest-grid analysis.
  Counts match the table in D2 for every pulse option.
- [ ] Grid display settings (visible levels, review subdivision, strong-beat marks) are
  inputs of the model. They never change capture or stored data. Where they are edited
  is D12 V7.
- [ ] Unit tests: line counts for every pulse option, strong flags from the pattern,
  measure numbering, the thinning thresholds, no drift at 3,600 s, 1/4, 1/8 and 1/16
  subdivision alignment, and an empty visible range.

### D12. Timeline visuals (group 2 of 2: how it looks)

The behavior half is D10. Decided so far and recorded in UI-SPEC: the palette and contrast
rules, the live-view composition, the pitch axis without a keyboard, the overview strip and
the grid look A2 (bar line and plate, light beat lines, subdivision ticks, strong-beat column).
The rest is agreed with mockups for the three fixtures at the bottom of this section, recorded
in UI-SPEC, then implemented. D10 defines what
the drawing must be able to show. The discussion is on the critical path of the first delivery.

What the timeline draws today (read from the drawing code):

| Layer | Drawn as |
| --- | --- |
| Pitch lanes | tinted black-key rows and a line under each C |
| Review grid | dotted full-height lines and ruler ticks at the chosen subdivision (1/4, 1/8, 1/16) |
| Metronome pulses | a light vertical line for every click; strong beats as a tinted column |
| Measures | a graphite bar line at each measure start and the bar number on a plate |
| Labels | one row of seconds and one row of beat numbers |
| Notes and playhead | steel note blocks with names, magenta for held notes and the playhead |

The palette and contrast rules are agreed (UI-SPEC "Appearance"). The scale is still fixed:
every click and every subdivision is drawn at any zoom, which is what V2 and D10 M6 replace.

Reported: the timeline is overloaded with lines that are not needed, and the intended design
is different from this.

Questions to settle in the discussion:

- What is the timeline for first: reading timing against the beats, or reading pitch?
- Which layers are always visible, which appear on hover or selection, which sit behind a toggle?
- Does every semitone need a line, or only the natural notes, or alternating stripes instead of lines?
- Are the review grid and the metronome pulses two layers or one?
- How many line styles may be visible at once (proposal: three levels, measure, beat, subdivision)?
- Where do the thresholds go at which the display changes (see V1)?

Items, to be made concrete by the agreed mockups:

- [ ] **V1. Range-aware display.** The same data looks different at different ranges.
  Narrow pitch range (about two octaves or less): tall lanes with a label on every lane.
  Wide range (three octaves up to all 88 keys): thin lanes, labels only on C, no per-semitone
  lines, alternating key-colored stripes instead. Long time range: a note block is never
  thinner than 2 px, its label appears only when the block is wide enough, the ruler format
  follows the scale (seconds, m:ss, h:mm:ss, in the user's culture), and the overview strip
  shows the whole recording (D10 M2). Thresholds are agreed with the mockups.
- [ ] **V2. Grid look** (the visual side of D10 M6). At most three line styles are visible at any
  scale, with a stated budget of lines per 100 px. Levels appear and disappear with the scale,
  measure numbers sit on the ruler, strong beats are distinguishable from weak ones, and
  contrast holds in light and dark themes.
- [ ] **V3. Cursor look.** The playhead stays distinguishable above notes and grid lines,
  in the timeline and in the overview strip (D10 M2). Dragging shows the time at the cursor. The Locked or Free state of the camera is visible,
  and so is the Follow control.
- [ ] **V4. Zoom controls look.** A toolbar with zoom out, zoom in and fit for each axis and
  the current scale in words ("1 beat = 80 px"). Controls are disabled at the limits, have
  localized accessible names (D11) and are at least 44 px on touch.
- [ ] **V5. Live view look.** The recording screen of D10 M3, composition agreed (UI-SPEC
  "Live view"): beat bars as the dominant element with the current beat in the accent,
  bar and beat number under them, the Held slot large below, elapsed time small in the
  header. Open: whether lighting the current beat in magenta distracts while playing;
  the alternative is a mark under the beat.
- [ ] **V6. Legend and ruler.** The legend lists exactly the visible layers, and hidden layers can
  be turned on again. Ruler labels thin out with density and never overlap.
- [ ] **V7. Grid settings.** The grid settings of D10 M6 open in a modal (shared Modal
  component); the review subdivision moves there from the toolbar picker.
- [ ] The agreed layers, the line budget, the thresholds and the default visibility are
  written in UI-SPEC with the mockups, and the implemented timeline is compared with them.

Fixtures to mock up and to test the display on: a one-octave recording of 10 s, a recording
spanning A0 to C8 over 5 minutes, and a one-hour recording with long gaps.

## E. Phones and tablets

Designed after the desktop UI is finished; D8 is accepted per platform.

### D16. Phone and tablet layouts

The phone and tablet designs are reduced layouts of the same states, agreed with mockups and
recorded in UI-SPEC before implementation. D8 accepts the platform adapters; this section
accepts how the app looks and is operated on those screens.

- [ ] Mockups agreed for phone portrait and landscape and for tablet portrait and landscape:
  the live view and review as full-screen modes, setup in a sheet, and a written list of
  what is cut or moved on a phone (for example the moment panel as a bottom sheet and less
  frequent toolbar actions in a menu).
- [ ] The app displays correctly on the smallest supported phone (proposal: 360 x 640 dp),
  a large phone, an 8-inch and an 11-13-inch tablet, in both orientations, with notches,
  safe areas and gesture bars, light and dark themes, and system text scale up to 200 %.
- [ ] Touch targets are at least 44 pt (48 dp on Android). Drag, pinch and click follow
  D10; no function depends on hover, and page scrolling never takes a gesture meant for
  the timeline.
- [ ] The no-jump rule of D7 holds; rotating the device keeps the position, the camera and
  any open recording.
- [ ] Checked on real devices; emulator or desktop-narrow screenshots do not count.

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

## F. Technical quality

Budgets and diagnostics that hold on every platform.

### D9. Responsiveness and resource budget

Reported: the main panel lags. It has not been measured yet, so the first item is a baseline.
The shared-clock rule under a stalled UI thread is a D2 item.

- [ ] Baseline before any optimization: record UI-thread time per 20 ms refresh tick
  while recording and while playing back, plus page-scroll and timeline-pan
  smoothness, on the five-minute fixture below. Write the numbers and the machine
  into the Orchid.Windows README. Every later item is judged against them.
- [ ] Budget during recording and playback on the reference PC: UI-thread work of
  one tick is at most 4 ms median and 16 ms at the 99th percentile, and no tick
  is skipped or queued behind another.
- [ ] Work per tick is proportional to what changed. A label is assigned only when
  its text differs, a layout size is set only when it actually changes, and only
  the playhead strip and the held-key area redraw while the notes are unchanged.
- [ ] Drawing cost depends on the visible viewport and the lane count, not on the
  total number of notes. Values such as the lowest and highest pitch are computed
  when the notes change, not on every access, and drawing a note never scans all notes.
- [ ] The page scrolls, the window resizes and the timeline pans without visible
  stutter while five minutes of continuous notes are being recorded.
- [ ] Drawing the overview strip (D10 M2) costs the same for the five-minute and the
  one-hour fixture at the same strip width.
- [ ] CPU budget (numbers to confirm with the baseline): the process uses at most 10 % of
  one core on average over 10 s on the reference PC while recording (capture, metronome
  and live view) and during visual playback, and close to 0 % while idle in Ready or a
  paused review; no timer ticks when nothing on screen changes. Phone and tablet numbers
  are measured separately on reference devices (D16).
- [ ] Memory budget (numbers to confirm): the working set stays under a stated limit with
  the one-hour fixture loaded (proposal: 300 MB on Windows), recording costs a bounded
  amount per event, and nothing grows while the metronome runs: a two-hour run with no
  input and a one-hour recording show no growth beyond the recorded data.
- [ ] A draw-time check runs `TimelineDrawing.Draw` for the five-minute fixture against
  a recording canvas and fails above an agreed threshold. It is environment-sensitive,
  so the threshold is generous and the check is reported separately from the
  correctness suite.

Suspects found by reading the code, not by profiling, to be confirmed or dropped by
the baseline: `Lowest` and `Highest` in `TimelineDrawing` run LINQ over every note and
are read inside the per-lane and per-note loops; `UpdatePosition` resets the timeline
`WidthRequest` and `HeightRequest` every tick, which forces a layout pass; the timeline
and pitch labels are invalidated twice per tick; the tick runs 50 times a second even
when nothing visible changed.

### D14. Diagnostics and crash safety

The global crash log exists (`CrashLog`, hooks for AppDomain, unobserved tasks and WinUI)
and named the cause of the 16384 px crash. Recovering the recording after a crash is a D5 item.
What the log does not do yet:

- [ ] Every crash is written to the log with its full stack trace and inner exceptions:
  unhandled exceptions on any thread, unobserved tasks, the platform UI's unhandled
  exceptions (WinUI, Android, iOS/Mac Catalyst) and a caught `OutOfMemoryException`. A test
  forces each kind and checks the entry.
- [ ] A kill by the operating system (out of memory: Android low-memory killer, iOS jetsam,
  Windows commit limit) cannot be logged when it happens. While recording or playing the
  log writes a heartbeat every 10 s with the working set, managed heap and session state,
  so the last entry before a kill shows the memory. On the next start a log without
  `Shutdown` is reported with that last heartbeat and, where the platform supplies it,
  the system exit reason (Android `ApplicationExitInfo`, Windows Error Reporting).
- [ ] Every logged exception carries the most detail available. WinUI `UnhandledException`
  can arrive without a managed stack trace (the 16384 px entry contained only the message),
  so the stack is captured where it still exists, for example from
  `AppDomain.FirstChanceException`, and a test shows a stack for a deliberate UI-thread failure.
- [ ] The log says which build wrote it (version and commit) and what the app was doing:
  session state, BPM, pulse, duration and note count at the moment of the failure.
- [ ] On the next start, a log without the `Shutdown` entry is reported to the user as
  "the previous session ended abnormally" with the file path. A normal exit already
  writes `Shutdown`.
- [ ] Logs are rotated: at most the last N files or M MB are kept, instead of one file
  per launch forever.
- [ ] A failure in a non-critical view is shown to the user and the rest of the app keeps
  running where that is safe; fatal errors show a message with the log path before exit.

### D11. Localization

- [ ] Every user-visible string comes from a resource file keyed by a stable id:
  labels, buttons, tooltips, accessible names and descriptions, status and error
  messages, the legend, dialog titles and templates such as "Beat 3 of 8". No display
  literal remains in XAML or C#. English is the source language; keys are ids, not the English text.
- [ ] The mechanism is proven with two languages, English and Russian. The language
  follows the OS culture and can be overridden in settings. Applying it at startup is
  enough; switching without a restart is a later improvement.
- [ ] Numbers, durations and BPM use the current culture. Plurals come from the resource
  format ("1 note", "2 notes", Russian one/few/many); sentences are never built by
  concatenating fragments.
- [ ] Note naming is a setting separate from the UI language: letters (C D E), solfege
  (Do Re Mi, Russian До Ре Ми) and German (H for B). The timeline pitch labels
  and the Held slot follow it. Saved data stays language-neutral (MIDI numbers).
- [ ] Layout survives text at least 40 % longer than English at the default and narrow
  widths, with no truncated or overlapping text. A pseudo-locale build checks this.
- [ ] A check fails when a key is missing from a language or when XAML or C# contains
  display text instead of a resource. Right-to-left layout is not a goal now, but
  nothing may hard-code left and right where a start/end alignment would do.
- [ ] Logs, diagnostics, error codes and the log file stay language-neutral and in English.
- [ ] Other devices follow the same model: the remote page keeps display text in a message
  catalogue (REMOTE-DOD), and the shared components use the same key names on every client.

## G. Follow-up

### D13. Chord recognition (follow-up, separate system)

The bottom keyboard is removed (D7); held notes may instead be shown as a chord. Recognition
is its own component and not part of the first delivery, but the UI reserves its place now:
the Held slot in the live view and in review (UI-SPEC "Held slot") shows a chord name when
one is supplied and the notes alone otherwise.

- [ ] Contract: the input is the set of intervals held at a position, the output is an
  optional label (name, root, quality, bass note) and the notes it covers. No output means
  "show the notes". It lives in Core/Application; the UI only displays the result.
- [ ] The rules are defined before implementation: inversions and slash chords, sevenths
  and extensions, a bass note separate from the chord, doubled notes, arpeggios spread in
  time, and what is not a chord.
- [ ] Tests over a fixture of named chords; the live view and review show the same label
  for the same held set.
- [ ] Recognition never changes recorded data and never groups notes at capture (D3).

### D18. Audible MIDI replay (long-term wish)

Ideally playback also sounds the recorded notes. Not planned for a delivery; recorded here so
the design does not block it.

- [ ] A note-output contract (note on/off with pitch, velocity and channel, all notes off) with
  an implementation per platform: a software synthesizer or SoundFont player, or a MIDI output
  device. `IBeatSoundPlayer` only plays clicks and is not that contract.
- [ ] Notes sound from their recorded attacks and releases, including pedal tails (D15), with
  correct note lifecycle on seek, Pause and the end: no hanging notes.
- [ ] It uses the same playback clock as the playhead and the D17 metronome, so notes and clicks
  stay aligned. Velocity becomes meaningful only once capture keeps it (D1).

### Remote control

[REMOTE-DOD](REMOTE-DOD.md) is a follow-up, not part of the first Windows acceptance.
`Orchid.Remote` currently holds only a static mock page. Its PC-side items depend on the
strong-beat pattern (D2) and the session lifecycle (D4).

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

Five-minute fixture (D9, D10): a continuous recording of 300 s at 120 BPM with
at least 2,000 notes spread over at least three octaves, including chords that overlap.

Wide-range fixture (D10, D12): 5 minutes of notes from A0 (MIDI 21) to C8 (MIDI 108).
One-hour fixture (D9, D10, D12): 3,600 s at 120 BPM, at least 20,000 notes, with silent gaps
of a minute or more. Narrow fixture (D10, D12): 10 s inside one octave.
Surface-limit fixture (D10 M5): 600 s of recording.
Many-held-notes fixture (D7): positions with 0, 1, 3, 6 and 10 keys held at once, and a chord label.
Pedal fixture (D15): the shared fixture with the pedal down from 350 ms to 800 ms: C4 sounds until
800 ms after its release at 400 ms, E4 until 800 ms; held keys stay as in the table above.
Crash fixture (D5, D14): a one-hour recording whose process is killed at random moments; every
event written before the kill is recovered.

## Delivery order and completion

P0 and D0 are closed. For integration, preserve events/intervals (D1), then establish the
shared clock/pulse rules (D2) and the pedal (D15). The storage format (D5) follows D1's event
model. A fixture can drive review/UI work (D6, D7, D10, D12) while real MIDI/lifecycle work
(D3, D4) is completed independently. D10 can start with its pure functions now; D12 needs its
remaining mockups, and the two are accepted together.

Final Windows acceptance requires groups A to D and F together: D1-D7, D9-D12, D14 and D15.
Phones and tablets (E) start after the desktop UI is finished: D16 first as design, then D8
per platform. Chord recognition (D13) and the remote control are follow-ups.

Each delivered behavior has a meaningful automated check in the existing AAA
MSTest style where possible. Rendering/interactions, actual MIDI and audio also
require the relevant Windows scenario checks. Build success alone does not mark
a recording/review scenario Done. Changes to the data model must verify all callers.

Before implementing legacy compatibility or audible note replay, define their
missing inputs/behavior. The alternative Practice layout and closest-grid
analysis do not block the Workspace first delivery.
