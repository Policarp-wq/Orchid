# Orchid UI Philosophy for the Rhythm Session MVP

## Purpose

The MVP UI helps a pianist observe a monophonic performance against a stable rhythmic grid. It presents recorded facts clearly and leaves musical judgment to the player.

The MVP is intended for scales and chromatic exercises. Chord interpretation is outside its scope.

## Product principles

1. **Show facts before judgments.** Display when a note was pressed, how long it was held, and how far its attack is from useful grid divisions. Do not label a performance as good, bad, correct, or incorrect.
2. **Keep time visually central.** The timeline is the primary review surface. The piano keyboard supports note location but does not replace the timeline.
3. **Use one clock.** The metronome, MIDI recording, grid, and note positions share the session start as time zero.
4. **Make every visual mark explainable.** Colors, lines, blocks, and states must have a visible label or legend.
5. **Keep configuration small.** The player chooses a MIDI input, BPM, and pulse unit. The MVP time signature is fixed at 4/4.
6. **Preserve raw observations.** A stopped session is saved as an Orchid log and can be loaded again without changing its recorded timing.

## Screen structure

The window has four functional areas.

### 1. Session controls

Show:

- MIDI input device;
- refresh-device action;
- BPM;
- pulse unit: 1/1, 1/2, 1/4, 1/8, or 1/16;
- fixed time signature: 4/4;
- Start and Stop actions;
- recording duration;
- number of received note attacks.

Configuration is editable before a session and locked while recording.

### 2. Performance controls

Show:

- Orchid log path;
- Browse and Load actions;
- selected review grid;
- playback controls;
- the loaded or saved file name;
- the number of notes in the performance;
- selected-note details.

Loading must produce visible feedback. An empty, invalid, or missing file must not fail silently.

### 3. Timeline

The timeline is a horizontally scrollable representation of the complete session.

It must show:

- elapsed time in seconds;
- measure starts;
- divisions of the currently selected review grid;
- every recorded note attack;
- each note name;
- note duration when a matching release is available;
- a legend explaining all colors;
- an explicit empty state when no notes were recorded.

Current color meanings:

| Visual | Meaning |
| --- | --- |
| Gold line | Start of a 4/4 measure |
| Blue line | Division of the selected review grid |
| Green note block | Recorded note |
| Orange note block | Selected recorded note |

Color must not be the only explanation. The legend and note labels remain required.

### 4. Piano keyboard

The keyboard shows the physical location of notes.

- During recording, pressed MIDI notes are highlighted live.
- During playback, notes are highlighted for their recorded duration.
- Clicking a timeline note highlights that note on the keyboard.

## Session behavior

### Before Start

The player selects a MIDI device, BPM, and pulse unit. Start is unavailable as a meaningful action until a MIDI device is selected.

### After Start

- Session time starts immediately at zero.
- The metronome starts on the configured pulse unit.
- The first pulse of each 4/4 measure has a distinct accent.
- MIDI attacks and releases are timestamped against the same session origin.
- The first played note does not move or redefine the rhythmic grid.
- The UI shows elapsed recording time and the number of received attacks.

### After Stop

- MIDI capture and metronome playback stop.
- Any notes still held are closed at the session end.
- The complete session appears on the timeline.
- The session is saved automatically as an Orchid log.
- Review and playback controls become available.

## Timeline interactions

### Hover

Hovering over a note block shows:

- note name;
- MIDI note number;
- attack offset from session start;
- held duration;
- the two closest rhythmic divisions;
- deviation from each division in milliseconds;
- whether the attack was early or late.

The comparison candidates run from 1/1 through the currently selected review grid. For example, a selected 1/8 grid compares 1/1, 1/2, 1/4, and 1/8. It does not compare 1/16 until the player selects the 1/16 grid.

Deviation is informational. The MVP does not apply a tolerance or produce a pass/fail result.

### Click

Clicking a note block:

- keeps the note selected in orange;
- highlights its piano key;
- shows its timing and closest-grid information in the performance controls.

### Drag

Holding the left mouse button and dragging moves the timeline horizontally. A short click selects a note; movement is treated as navigation and must not accidentally select it.

### Grid selection

Changing the review grid redraws divisions and recalculates the two closest values. It does not modify the recorded performance.

## Timing semantics

BPM counts the selected pulse unit per minute. One BPM value defines every rhythmic duration.

For 40 BPM with a quarter-note pulse:

| Value | Duration |
| --- | ---: |
| 1/1 | 6000 ms |
| 1/2 | 3000 ms |
| 1/4 | 1500 ms |
| 1/8 | 750 ms |
| 1/16 | 375 ms |

Grid points are calculated from absolute multiples of their duration since session start. They are not produced by repeatedly adding timer delays, so timing error must not accumulate across the session.

A positive deviation means the note was late relative to the nearest grid point. A negative deviation means it was early.

## Log behavior

New rhythm-session logs preserve:

- BPM;
- pulse unit;
- 4/4 time signature;
- total session duration;
- MIDI note number;
- MIDI channel;
- velocity;
- attack offset;
- held duration.

Legacy Orchid piano logs remain readable. Because they do not contain tempo settings, rhythmic deviations are unavailable for them and the UI must say so explicitly.

## Deliberately outside the MVP

The MVP does not include:

- correctness scoring;
- configurable timing tolerance;
- automatic good/bad coloring;
- chord detection or chord-level timing;
- interpretation of overlapping notes as harmony;
- time signatures other than 4/4;
- tempo changes inside a session;
- note editing or deletion;
- timeline zoom;
- server synchronization;
- aggregated practice statistics.

These capabilities may be added later without changing the MVP rule that recorded facts and derived judgments must remain distinguishable.

## Review checklist

A UI change belongs in the MVP when all applicable answers are yes:

- Does it help configure, record, inspect, replay, or load one rhythm session?
- Is every new visual mark explained in the UI?
- Does it preserve the shared session timeline?
- Does it show measured data without inventing a quality judgment?
- Does it keep note selection, hover, playback, and timeline navigation unambiguous?
- Does an error or empty result produce visible feedback?
- Does it avoid introducing chord semantics or another out-of-scope capability?
