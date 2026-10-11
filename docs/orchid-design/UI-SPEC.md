# Orchid: rhythm-session UI specification

Design handoff recorded on 2026-10-06. This specification captures the mockup discussed with the user and the latest product decisions in that conversation.

## Decisions and scope

- Product: Orchid, a MIDI piano practice application.
- Implementation direction: .NET MAUI, with shared C#/XAML views, styles, and interaction logic for Windows, macOS, Android, and iOS.
- Linux is not a first-version target. Keep musical/session logic independent of the UI framework so a Linux presentation layer can be added later.
- First version: inspect a recorded performance against time and metronome beats, with an arbitrary-position playhead and a piano keyboard reflecting that exact moment.
- The user's longer-term objective is to reveal mistakes and compare performances with a reference. Reference comparison, correctness scores, and error classifications are later work. Do not infer wrong notes from the demo colors.
- The user liked the overall visual direction. The particular palette and the two layout alternatives remain design choices, not a finalized brand standard.
- Use **Workspace** as the primary visual reference. **Practice** is an alternative composition, not a required extra application screen. No explicit final choice between the two was made.

The user's latest first-version requirements take precedence over older proposed mechanisms in `docs/ui-mvp-philosophy.md`: a timeline with a time slider, visible elapsed time and metronome beat marks, and a piano below with octave labels on C keys. The selected-time panel shows actual held keys and metronome position. Closest-grid calculations from the older document are not a required panel in this first mockup.

## Visual references

![Primary desktop layout](workspace-light.png)

![Dark theme](workspace-dark.png)

![Narrow layout](workspace-narrow.png)

![Alternative desktop composition](practice-light.png)

For interaction, open `mockup.html`. The preview carousel selects the two variants; its controls must not become product navigation.

## Screen composition

### Workspace: primary desktop reference

Read the screen from top to bottom:

1. Small app title bar: Orchid icon, Orchid name, and secondary “Rhythm practice” label. Let the native host manage actual window controls and drag regions.
2. Session setup strip: MIDI input and refresh action, tempo in BPM, metronome pulse unit, fixed 4/4 meter, Start/Stop, and session status.
3. Performance header: session title, log filename, note count and duration, and Open log.
4. Main inspection area: the timeline takes most of the width; a narrow panel on the right shows the moment under the playhead.
5. Piano keyboard below the inspection area, spanning the available content width.
6. Small footer: recording/save/device feedback and tempo/pulse context.

The timeline panel includes playback and return-to-start actions, current and total time, an optional review-subdivision picker, the note/time view, the session-position slider, and a compact legend.

### Practice: alternative desktop reference

Session setup is a narrow column on the left. The remaining area holds the performance header, timeline, a compact current-moment detail strip, and the keyboard. This gives the timeline more vertical prominence. The alternative uses the same data and behavior as Workspace.

## Timeline and seeking

- Horizontal position represents elapsed time from the session start. Vertical lanes represent pitch, with note names at the left.
- Each note block begins at its actual attack time and extends for its actual held duration. Keep the stored timing; never snap recorded events to the grid for display or saving.
- Draw elapsed-time labels, measure boundaries, and metronome beat marks. In the initial 4/4, quarter-note-pulse example, beat labels repeat **1, 2, 3, 4** once per measure. Measure start has a stronger gold mark; optional subdivisions remain lighter.
- Make the violet vertical playhead visible above notes and grid lines. The **Session position** slider covers the full recorded duration. Show the selected time immediately beside the slider and in the playback toolbar.
- Moving the slider updates the playhead, held-key highlights, and current-moment details together, including silent gaps.
- Clicking a note seeks to its attack. Its details and physical piano key then agree with the playhead.
- Playback advances this same session position. Pause holds it. Return to start sets it to zero. Playback and live capture must not create separate inconsistent clocks.
- Horizontal timeline dragging is navigation. Distinguish a short selection click from a drag so panning does not accidentally select notes. In production, viewport navigation is separate from the full-session position slider.
- For long recordings, retain a readable time scale and scroll the timeline. Do not compress an entire multi-minute recording into a few pixels per note. Timeline zoom is outside the current scope.

The demo is deliberately tiny: 16 notes over eight seconds at 120 BPM with a quarter-note pulse. Its fixed range and limited panning are not production limits.

### Timing and beat semantics

Use the same session origin for MIDI timestamps, metronome scheduling, the timeline, and the playhead. The first note does not reset the grid.

BPM counts the configured pulse unit. With pulse denominator `d`, one pulse lasts `60 / BPM` seconds, one quarter note lasts `(60 / BPM) * d / 4`, and a 4/4 measure lasts four quarter notes. At 120 BPM with a 1/4 pulse, beats are 0.5 seconds apart and a measure is two seconds. Review subdivisions do not change the metronome or recorded data.

If other pulse units are enabled, make actual metronome-click marks agree with that configuration; do not draw four click marks regardless of the pulse. The initial visual reference uses 1/4 and four beats per 4/4 measure.

## Piano and current-moment panel

- Draw a recognizable piano keyboard with correct groups of two and three black keys, using scalable geometry.
- Put octave labels on **C keys**, such as C3, C4, and C5. The desktop demo shows C3 through B5; that range is sample content, not a hard limit.
- At position `t`, a key is held when `attack <= t < release`. At the exact release time the highlight is gone. In a gap, clear all key highlights and show “No keys held.”
- During live capture, highlight physical key state. During review and playback, derive highlights from the recorded intervals at the playhead.
- Display all physically held keys if intervals overlap. This is raw MIDI state, not chord detection or harmonic interpretation. The first-version exercise scope remains primarily monophonic.
- The side panel shows current note name and MIDI number, attack time, recorded duration, measure number, and beat position. For a gap, show empty note fields instead of the previously selected note. If multiple keys are held, list the actual keys rather than inventing a chord name.
- Close notes still held when Stop is pressed at the session-end time. Sustain-pedal semantics are not defined by this mockup; do not silently equate pedal-sustained sound with physical key presses.

## Session and file states

Existing product notes propose the following supporting controls around the core review surface:

- Before recording: select a MIDI input and configure BPM/pulse. Start requires an available selected input.
- During recording: show elapsed time and received attacks; lock configuration and conflicting load/playback actions. Start becomes Stop.
- After Stop: stop capture and metronome, close held notes, present the recorded timeline, and save the session as an Orchid log.
- Loading: show the loaded filename and duration; provide visible feedback for missing, invalid, or empty files.
- Empty recording: use an explicit empty state in the timeline and an unhighlighted keyboard, without fabricated demo notes.
- MIDI disconnect, audio failure, and save failure must be visible. Do not display successful saving just because a button was pressed.

These are production behaviors to implement against actual services. The HTML does not implement them beyond simulated UI state changes.

## Appearance

The visual goal is a calm practice workspace: quiet neutral surfaces, compact controls, small secondary labels, readable note marks, and restrained rounding. Give the timeline most of the visual attention.

The green/orange/gold/blue meanings came from the project's existing UI philosophy. The orchid-purple accent and the exact shades were proposed in this design conversation. Orange indicates inspection selection or the key under review, not an error. Use explicit labels and a legend rather than color alone.

| Token / purpose | Light | Dark |
| --- | --- | --- |
| App background | `#F6F5F8` | `#17171D` |
| Panel / field surface | `#FFFFFF` | `#202027` |
| Subtle surface | `#F0EFF4` | `#292932` |
| Main text | `#292735` | `#E9E7EF` |
| Secondary text | `#706B7D` | `#AAA5B8` |
| Border | `#E4E1EB` | `#383640` |
| Orchid accent / playhead | `#755B9B` | `#C1A8E4` |
| Text on accent | `#FFFFFF` | `#21142E` |
| Recorded-note fill | `#DCEBE4` | `#29473C` |
| Recorded-note text | `#2B5844` | `#C3E3D0` |
| Selected-note/key fill | `#F7DDBA` | `#715133` |
| Selected-note text | `#754A17` | `#FFE4BC` |
| Measure line | `#B59A56` | `#BBAA71` |
| Subdivision line | `#CBD8E7` | `#394454` |

Keep these as reusable theme resources. Tune contrast for production before shipping, especially small grid labels and notes. Reserve a distinct future error treatment; avoid conflating errors, selection, and live MIDI status.

Desktop reference sizes: about 22 logical pixels of content padding, 14-pixel body text, 11–12-pixel secondary labels, 22-pixel section titles, 6–8-pixel control/panel corner radii, and an 80-pixel keyboard. These are starting proportions, not fixed constraints. Use each platform's available system font rather than requiring Segoe UI everywhere. Support OS text scaling.

Use one consistent SVG icon family for flower/brand, refresh, record/stop, play/pause, rewind, open file, and information. The mockup uses Lucide. In MAUI, SVG files can be source assets and are converted to PNG resources during the normal build. Do not assume browser SVG rendering behavior applies to MAUI. The handoff's Lucide license is in `THIRD-PARTY-NOTICES.txt`.

## Adaptive behavior

- Wide desktop: timeline and current-moment panel sit beside each other.
- Medium window: move the moment panel below the timeline as a compact row; keep the timeline dominant.
- Narrow window / phone: stack setup controls, performance header, timeline, details, and keyboard. Keep Start/Stop and time seeking reachable. Increase touch targets instead of reducing type to fit.
- Use the actual content width. The demo's transitions around 760 and 580 CSS pixels illustrate reflow; they are not mandatory MAUI breakpoints.
- Keep note names, time, beat information, and octave labels readable. Reduce secondary tick-label density when needed, preserving actual beat lines and the time mapping.
- If the required keyboard range cannot fit with readable keys, provide horizontal navigation or a smaller visible range. Do not squeeze 88 keys into a phone-width strip.
- Geometry should respond to measured canvas size, OS scale, and safe areas. Avoid layout coordinates copied from one screenshot.

The narrow screenshot is a reflow reference. It is not an approved final phone screen or proof that all touch interactions are ready.

## MAUI implementation notes

- Build regular panels, fields, buttons, and the session slider with normal MAUI/XAML controls and shared resources.
- Implement the timeline and piano as reusable drawing controls, for example with `GraphicsView`/`IDrawable`. Centralize time-to-position and note-to-key mapping so rendering and hit testing agree.
- Keep musical data and session state in the existing Core/Application responsibilities; keep presentation drawing and platform-specific input/audio outside them. Do not introduce extra architecture without a concrete need.
- All four targets can share views and themes. MIDI devices, audio output, permissions, deployment, and packaging still require appropriate platform implementations and verification.
- The inspected repository has Core/Application/Infrastructure projects but no production presentation executable. The current `Timing` record stores an interval and one timestamp; it does not itself represent full note attack/release observations. `ITimingStorage` has no production implementation in that snapshot, and the NAudio output implementation is Windows-specific. Treat the screenshots as intended UI, not proof the capture/log/playback backend is complete.
- Use the repository's current `AGENTS.md` when implementing. In the inspected copy this includes English project-facing text, keeping platform code in the correct project, and working on a dedicated branch rather than committing directly to main.

Relevant documentation:

- [MAUI supported platforms](https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms?view=net-maui-10.0)
- [MAUI graphics canvas](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/graphics/?view=net-maui-10.0)
- [MAUI image and SVG resources](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/images?view=net-maui-10.0)

## Acceptance examples for the first implementation

1. Seek into a note: the timeline playhead, time readout, current-note fields, and corresponding piano key agree.
2. Seek into a silent gap: no piano key remains highlighted and the panel says no keys are held.
3. Seek to an exact release: that key is no longer held. Overlapping recorded intervals light their actual keys without chord classification.
4. At 120 BPM, 1/4 pulse, and 4/4, show four evenly spaced metronome beats per two-second measure; playback and recorded attacks share time zero.
5. Resize the window: controls remain accessible, details reflow, and note timing does not change.
6. Play, pause, and rewind: one shared playhead controls all review surfaces; no stale selection masquerades as current MIDI state.
7. Stop with a key held: its recorded interval ends at the session end. Save/load preserves timings.
8. Use an empty or invalid log / unavailable device: show an explicit state rather than invented data or a silent failure.

Reference-performance overlays, error scoring, chord recognition, note editing, and practice statistics are outside this first implementation.
