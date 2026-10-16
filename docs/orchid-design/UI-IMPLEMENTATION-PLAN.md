# Orchid UI: implementation plan

Status: phases 0-8 implemented on 2026-10-16; see "Implementation status" at the end. Phase 9 is not started. Requirements come from
[UI-SPEC](UI-SPEC.md) and [IMPLEMENTATION-DOD](IMPLEMENTATION-DOD.md); this document decides how the
UI code is split and in which order it is built. Item numbers (D7, D10 M5, ...) refer to the DoD.

## Goals and constraints

- **Windows first.** Every phase ends with a working Windows app. Other platforms are added after the
  desktop UI is accepted (DoD group E).
- **UI separate from the backend.** The UI library knows nothing about DryWetMidi, NAudio, `KeyListener`,
  `TimingStorage` or the session file format. It talks to the backend through a few small contracts it
  owns, and runs end to end on fakes.
- **UI in its own project.** Components, theme and layouts live in a class library. An OS head imports
  it, composes it with that OS's backend and overrides sizes; it does not copy views.
- **No abstraction for its own sake.** Plain MAUI controls stay where they render the same everywhere.
  Platform code is allowed in `Platforms/<OS>/` of the library when MAUI has no cross-platform API
  (mouse wheel, Escape key). Fonts, the renderer and breakpoints can be changed later without touching
  component code.

## Solution layout

```
Orchid.Core                 domain (unchanged)
Orchid.Application          use cases, backend contracts (unchanged)
Orchid.Infrastructure       timing adapters (unchanged)
Orchid.Platform.Windows     MIDI, audio, WindowsRecordingSession (unchanged)

Orchid.Ui                   NEW. MAUI class library. References no Orchid project.
  Contracts/                interfaces and records the UI consumes (the only door to a backend)
  Fakes/                    fake implementations and the acceptance fixtures
  State/                    WorkspaceState and its parts: everything the views show
  Timeline/                 camera, pitch map, grid model, note index, drawables, TimelineView
  Theme/                    tokens, metrics, styles, Palette for drawables, strings
  Controls/                 small reusable controls without application data
  Views/                    screen parts bound to State
  Layouts/                  AdaptiveHost, WideShell, CompactShell
  Platforms/Windows/        timeline wheel/pointer input, Escape and focus handling

Orchid.Windows              the Windows head (named Orchid.UiPreview until 2026-10-16)
  App, MauiProgram          composition and lifetime only
  Backend/                  adapters: Platform.Windows -> Orchid.Ui contracts
  Platforms/Windows/        WinUI host, CrashLog hooks

tests/Orchid.Ui.Tests       NEW. net10.0, everything pure in Orchid.Ui, fakes and fixtures
tests/Orchid.Windows.Tests  head-only tests (CrashLog, adapters)
```

Dependency rules:

```
Orchid.Windows (head) ──> Orchid.Ui
        │                     (MAUI only)
        └──> Orchid.Platform.Windows ──> Application ──> Core
```

- `Orchid.Ui` has no `ProjectReference`. Backend types never appear in its public API.
- `Orchid.Ui` targets `net10.0` and `net10.0-windows10.0.19041.0`; mobile TFMs are added in phase 9.
  The plain `net10.0` build compiles shared code without WinUI, so a `Microsoft.UI.*` or Windows-only
  call outside `Platforms/Windows/` breaks the build. It also lets `Orchid.Ui.Tests` run on Linux
  (`--artifacts-path /tmp/orchid-build`) and on Windows.
- Adapters live in the head, because only the head references both sides. `Orchid.Platform.Windows`
  never references MAUI.
- Composition stays explicit in `MauiProgram` (no DI container, AGENTS.md).

## Contracts (`Orchid.Ui/Contracts`)

Semantic minimum from DoD "Required data", shaped for display. Times are `TimeSpan` session offsets
(100 ns resolution, explicit unit); negative offsets are allowed for count-in notes (D2 open decision).

```csharp
public sealed record MidiInputInfo(string Id, string Name);

public interface IMidiInputCatalog
{
    Task<IReadOnlyList<MidiInputInfo>> GetInputsAsync(CancellationToken cancellationToken);
}

public sealed record SessionSettings(
    int Bpm, int PulseDenominator, int MeterNumerator, int MeterDenominator,
    IReadOnlyList<bool> StrongBeats, int CountInBars);

public interface IRecorder
{
    // Throws SessionStartException with a ProblemCode when the device or audio cannot start.
    Task<ILiveSession> StartAsync(MidiInputInfo input, SessionSettings settings, CancellationToken cancellationToken);
}

public interface ILiveSession : IAsyncDisposable
{
    LiveSnapshot Read();               // UI tick; thread-safe, no callbacks into UI code
    Task<RecordingData> StopAsync();   // idempotent; returns the closed, immutable recording
}

public enum LivePhase { CountIn, Recording, Ended }

public sealed record LiveSnapshot(
    LivePhase Phase, TimeSpan Elapsed, int Bar, int Beat,   // Bar < 1 during the count-in
    IReadOnlyList<HeldKey> Held, bool PedalDown, SessionProblem? Problem);

public sealed record HeldKey(int Midi, int Channel, int Velocity, TimeSpan Attack);

public enum ClosedBy { Release, SessionEnd, Disconnect, Recovery }

public sealed record NoteSpan(int Midi, int Channel, int Velocity, TimeSpan Attack, TimeSpan Release, ClosedBy ClosedBy);
public sealed record PedalSpan(int Channel, TimeSpan Down, TimeSpan Up, ClosedBy ClosedBy);

public sealed record RecordingData(
    SessionSettings Settings, TimeSpan Duration,
    IReadOnlyList<NoteSpan> Notes, IReadOnlyList<PedalSpan> Pedal);

public interface IRecordingStore
{
    Task<SaveResult> SaveAsync(RecordingData recording, CancellationToken cancellationToken);
    Task<LoadResult> LoadAsync(string path, CancellationToken cancellationToken);
    Task<RecordingData?> FindUnfinishedAsync(CancellationToken cancellationToken);
}

// SaveResult: Saved(path) | Failed(ProblemCode). LoadResult: Loaded(data, name) | Missing | Invalid | UnsupportedVersion.
public enum ProblemCode { MidiDisconnected, MidiUnavailable, AudioFailed, SaveFailed, DiskLow }
public sealed record SessionProblem(ProblemCode Code, string? Detail);

public sealed record OrchidServices(IMidiInputCatalog Inputs, IRecorder Recorder, IRecordingStore Store, TimeProvider Clock);
```

Decisions behind the shape:

- **Snapshot polling instead of events.** The UI reads `LiveSnapshot` once per frame. Backend threads
  never call into UI code, so late MIDI callbacks after Stop (D3 gap) cannot touch views, and the UI
  frame rate never affects capture timing (D2).
- **The backend pairs events into intervals** (D1). The UI receives `NoteSpan`s and only indexes them
  for drawing and `HeldAt` queries; both sides are checked against the shared acceptance fixture.
- **Bar and beat in the live view come from the backend**, which knows the scheduled click. Review
  computes them from `GridModel`.
- **Display text never crosses the contract.** Problems are codes; the UI maps them to its strings (D11).
- `TimeProvider` (BCL) is the clock. No custom clock interface.
- File picking uses MAUI's `FilePicker` in the UI; the store only receives a path.

When D1, D2, D5 and D15 land in Application, only the adapters in the head change.

## Fakes and fixtures (`Orchid.Ui/Fakes`)

| Type | Behavior |
| --- | --- |
| `FakeMidiInputCatalog` | Mutable list of inputs; Refresh tests add and remove devices |
| `ScriptedRecorder` | Plays a fixture as a live performance on the given `TimeProvider`: count-in, beats from the settings, held keys at the elapsed time. Options: fail on start with a code, disconnect at a time, stop returns the part played so far with `SessionEnd` closures |
| `InMemoryRecordingStore` | Keeps recordings in memory; switches for save failure, missing, invalid, unsupported and empty files, and an unfinished recording to recover |
| `Fixtures` | Deterministic (fixed seed) `RecordingData` builders: `Shared` (the 1000 ms table), `Empty`, `TwoEvents10MsApart`, `SamePitchTwoChannels`, `Narrow10s`, `WideA0ToC8` (5 min), `FiveMinutes` (≥ 2,000 notes, 3 octaves, overlaps), `OneHour` (≥ 20,000 notes, minute gaps), `SurfaceLimit600s`, `ManyHeld` (0/1/3/6/10 keys), `Pedal` |

The head chooses the backend at startup: real Windows services by default, fakes with `--fake`
(and `--fixture <name>` to open a review directly). Fixtures serve the Windows acceptance scenarios
of D7, D9, D10 and D12 as well as the unit tests. Tests use a small hand-written manual
`TimeProvider`; no testing package is added for it.

## State (`Orchid.Ui/State`)

Plain classes with `INotifyPropertyChanged` (one small base class, no MVVM framework). Views bind with
compiled bindings (`x:DataType`). Actions are methods and MAUI `Command`s. All UI state lives here,
not in views, so swapping layouts or rotating a device loses nothing (D16).

| Class | Owns |
| --- | --- |
| `WorkspaceState` | `Mode` (Ready, CountIn, Recording, Review), the parts below, `Tick()`, `IsTicking` |
| `SetupState` | Inputs, selected input, BPM (30..240), pulse (1, 2, 4, 8, 16), meter (4/4), strong beats (reset to default on pulse change), count-in bars, `CanStart`, locked while recording |
| `LiveState` | Latest `LiveSnapshot` fields as bindable properties, assigned only when they change (D9) |
| `ReviewState` | `RecordingData`, `NoteIndex`, position in `[0, duration]`, play/pause/rewind on one elapsed clock (moved from `PreviewSession`), selected note, `TimelineCamera`, fold toggle, grid settings |
| `StatusState` | File name, save state, banner problem, recovery offer |

`WorkspaceState.Tick()` runs only while recording, counting in or playing (`IsTicking`); the host
starts and stops the dispatcher timer from it, so Ready and paused review cost nothing (D9).

## Timeline core (`Orchid.Ui/Timeline`, pure)

No MAUI types; doubles and records, fully unit-tested.

| Type | Responsibility | DoD |
| --- | --- | --- |
| `NoteIndex` | Built once per recording: notes by attack, prefix maximum of releases, lowest/highest pitch, used pitches. `HeldAt(t)` (`attack <= t < release`) and `Visible(t0, t1, lanes)` without scanning all notes | D6, D9 |
| `PitchMap` | Pitch to lane and back. One table of 128 entries: linear (Fit, Zoomed) or folded (only used pitches). The single pitch-to-y source | D10 M4 |
| `TimelineCamera` | Time offset, pitch offset, px per second, px per lane, Locked/Free. `Fit`, `ZoomAt(anchor)`, `PanBy`, `Follow(position, anchor)`, clamping at the edges, limits (min readable 40 px/s and 4 px/lane, max 2,000 px/s and 64 px/lane, proposals) | D10 M1, M2, M4, M5 |
| `TimelineTransform` | Camera + pitch map + viewport: `TimeToX`, `XToTime`, `PitchToY`, `YToPitch`. Used by drawing, hit testing, labels, ruler, playhead and overview frame | D10 M5 |
| `GridModel` | Lines for a visible range from settings, pattern, subdivision and scale: time from the index (no drift), level (measure, strong, beat, subdivision), label, thinning below 12 px. Bar/beat at a position for the moment panel | D10 M6, D12 V2 |
| `OverviewColumns` | Lowest/highest sounding pitch per pixel column, computed on load and on strip resize | D10 M2 |
| `LayoutRules` | `For(width, height, thresholds)` returns Wide-side, Wide-stacked or Compact | Layouts |

The virtual extent is a property of the camera (doubles). No view is ever sized to the recording, so
the Win2D 16,384 px limit cannot be reached (D10 M5).

## Drawables and `TimelineView`

All drawing goes through `ICanvas` only, so the renderer can move from `GraphicsView` to SkiaSharp
(`Microsoft.Maui.Graphics.Skia`) without changing drawables if D9 measurements require it.

- `TimelineDrawable`: ruler band (time labels, bar plates), key rows or octave bands, grid lines,
  strong-beat columns, note blocks (min 2 px, label when wide enough), pedal tails and pedal strip
  (D15), playhead. Colors from `Palette`.
- `PitchAxisDrawable`: lane labels (all lanes or only C), held marks in `SelectedFill`, zoom strip and
  "notes above/below" counts.
- `OverviewDrawable`: cached columns, view frame, playhead.
- `TimelineView` (ContentView): pitch axis, timeline and overview `GraphicsView`s of viewport size,
  bound to `ReviewState`. Input:
  - shared, in the library: `TapGestureRecognizer` (select note, seek), `PanGestureRecognizer`
    (map-like drag, 4 px click threshold), `PinchGestureRecognizer` (time zoom);
  - `Platforms/Windows/TimelineInput.cs` (partial): wheel, Shift+wheel, Ctrl+wheel, Ctrl+Shift+wheel,
    precise mouse drag, grab cursor, Left/Right/Home/End. It replaces the WinUI pointer code now in
    `WorkspacePage.xaml.cs`.
  - Both call the same methods on `ReviewState`: `PanBy`, `ZoomAt`, `SelectAt`, `SeekTo`.

If the baseline (D9) shows full redraws per tick are too expensive, the playhead and held marks
move to a transparent overlay `GraphicsView`, so the notes layer redraws only on camera change.

## Theme, metrics and strings (`Orchid.Ui/Theme`)

- `Colors.xaml`: the UI-SPEC tokens as today (`<Token>Light` / `<Token>Dark`, `AppThemeBinding`).
  The `Key*` tokens are removed with the keyboard (D7).
- `Palette`: an immutable record of the drawing colors, rebuilt on theme change, read by drawables
  instead of looking tokens up by name every frame.
- `Metrics.xaml`: every size a head may change, with `OnIdiom` defaults (desktop / touch):

  | Key | Desktop | Phone, tablet |
  | --- | ---: | ---: |
  | `ControlHeight`, `IconButtonSize` | 36 | 44 |
  | `PagePadding` / `PanelPadding` | 24 / 16 | 16 / 12 |
  | `FontCaption`, `FontSecondary`, `FontBody`, `FontTitle`, `FontDisplay` | 11, 12, 14, 22, 28 | same |
  | `MomentPanelWidth` | 240 | 240 |
  | `PitchAxisWidth`, `RulerHeight`, `OverviewHeight`, `PedalStripHeight` | 56, 28, 48, 12 | 56, 32, 56, 16 |
  | `BeatBlockMinWidth`, `BeatBlockMaxWidth`, `BeatBlockHeight` | 28, 56, 28 | 28, 56, 44 |
  | `CompactMaxWidth`, `CompactMaxHeight`, `SidePanelMinWidth` | 600, 480, 960 | same |

  A head overrides a key by defining it in its own `Application.Resources`; local keys take precedence
  over merged dictionaries. Overrides are applied in the `App` constructor, before any view loads.
- `Styles.xaml`: implicit styles for `Label`, `Button`, `Border`, `Entry`, legend swatches, beat
  blocks; sizes only through `Metrics` keys.
- `Strings.resx`: every display string from the first view on, referenced with `x:Static`; English
  source now, Russian in D11. Keys are ids, not English text.
- Fonts: the platform font now, with numbers in a reserved width (no-jump rule). Lucide stays the icon
  font. Open: embedding one text font with tabular digits, if digits jitter on a platform whose system
  font has proportional digits. Changing it touches only `Styles.xaml` and font registration.

## Controls (`Orchid.Ui/Controls`)

Hold no application data; inputs are bindable properties, outputs are events (UI-SPEC "Shared components").

| Control | Inputs | Outputs | Notes |
| --- | --- | --- | --- |
| `ModalDialog` (exists) | `Title`, `Body`, `IsOpen` | `Closed` | Add bottom-sheet presentation on Compact, Escape and focus trap (Windows partial) |
| `BeatBars` (exists) | `Beats`, `IsReadOnly`, `CurrentBeat` | `BeatsChanged` | Add wrapping rows and the lit current beat for the live view |
| `SelectField` | `Title`, `Items`, `SelectedItem`, `ItemText` | `SelectionChanged` | Replaces `Picker`: a button opening a `ModalDialog` list, same look on every OS |
| `NumberStepper` | `Value`, `Minimum`, `Maximum`, `Step` | `ValueChanged` | BPM; hold repeats after 400 ms every 80 ms (as the remote); on desktop the value is also typed |
| `HeldSlot` | `Notes`, `ChordName`, `ShowDetails`, `SustainedNotes` | | Chips lowest first, MIDI/attack/duration in review, "No keys held", bounded height with inner scroll |
| `StatusBanner` | `Problem`, `ActionText` | `ActionInvoked` | MIDI disconnect, audio and save failures; icon and text, not magenta |

`Entry`, `Picker` and `Slider` are not used directly by views.

## Views (`Orchid.Ui/Views`)

`ContentView`s bound to a part of `WorkspaceState`. They do not know which layout hosts them.

| View | Content | Modes |
| --- | --- | --- |
| `AppHeader` | Brand, state, file name and save state; in live: elapsed, locked settings, Stop (Space) | all |
| `SetupPanel` | Input `SelectField` + refresh, BPM, pulse, 4/4, strong-beats summary + Edit, count-in, Start | Ready |
| `ReadyHome` | Last review card or empty state with Open log; recovery offer | Ready |
| `LiveView` | `BeatBars` (read-only, current beat), bar and beat or the big count, `HeldSlot`, pedal state, banner | CountIn, Recording |
| `ReviewToolbar` | Play/Pause, Rewind, time, note count, zoom out/in, Fit, Follow, Fold, grid settings; `IsCompact` moves the rare actions into an overflow modal | Review |
| `MomentPanel` | Bar and beat, `HeldSlot` with details; `IsCompact` gives a one-line peek | Review |
| `Legend` | Exactly the visible layers (D12 V6) | Review |
| `GridSettingsBody`, `StrongBeatsBody` | Modal bodies (D12 V7, D7) | |

## Layouts (`Orchid.Ui/Layouts`)

`AdaptiveHost` measures its own size, asks `LayoutRules`, and shows one of two shells. Both shells are
built once and cached; switching only toggles visibility. Thresholds come from `Metrics`.

- **Compact** when width < `CompactMaxWidth` or height < `CompactMaxHeight` (a landscape phone is
  wide but short).
- **Wide** otherwise. Inside it the moment panel is beside the timeline when width ≥
  `SidePanelMinWidth`, below it as a compact row otherwise (UI-SPEC "Adaptive behavior").

`WideShell` (PC and tablet):

```
Ready:      AppHeader / [SetupPanel] / [ReadyHome]
Live:       AppHeader(live) / StatusBanner / LiveView centered, bounded width
Review:     AppHeader / ReviewToolbar / [TimelineView | MomentPanel] / Legend
            (stacked: TimelineView / MomentPanel row / Legend)
```

`CompactShell` (phone), built in phase 9 after the D16 mockups:

```
Ready:      AppHeader(compact) / SetupPanel (single column, scrolls)
Live:       full screen LiveView, Stop always reachable
Review:     ReviewToolbar(compact) / TimelineView (fills) / MomentPanel peek as a bottom sheet;
            setup opens in a sheet
```

Rules for every layout: flat `Grid`s, fixed or bounded panel sizes, no size changes per tick, the
timeline is the only element that takes the remaining space, safe areas respected. Another head may
compose its own shell from the same views.

## Head (`Orchid.Windows`, Windows)

- `MauiProgram`: builds `OrchidServices` (real or fake), `WorkspaceState`, registers fonts.
- `App`: merges the library theme, applies head metric overrides, creates the window with
  `AdaptiveHost`.
- `Backend/WindowsMidiInputCatalog`, `Backend/WindowsRecorder`, `Backend/WindowsLiveSession`: wrap
  `WindowsRecordingSession`. Until D1, the adapter pairs `KeyPressTiming`s into `NoteSpan`s (the code
  now in `PreviewSession.LoadRecording`, with its known limits: 20 ms bins, MIDI rebuilt from `Tone`,
  no channel). After D1 it maps Application's intervals one to one.
- `InMemoryRecordingStore` is used until D5 delivers a file format; the header then says "Not saved"
  truthfully.
- `CrashLog` stays in the head.

## Phases

Each phase ends with `dotnet build Orchid.slnx`, `dotnet test --solution Orchid.slnx --no-build` on
Windows, and the Windows run named in the phase. Requires a .NET 10 SDK on `PATH`.

### 0. Spike: library mechanics (half a day)

- Create `Orchid.Ui` with `UseMaui`, TFMs `net10.0;net10.0-windows10.0.19041.0`; reference it from the
  head; build both TFMs.
- Verify: a `ContentView` with XAML and an implicit style from the library renders in the head; an
  embedded font in the library resolves (otherwise fonts stay registered in the head); a
  `Platforms/Windows/` partial compiles only for the Windows TFM; `PanGestureRecognizer` delivers
  mouse drags on Windows (decides how much of the pointer code stays Windows-only).
- Done when the four answers are written into this plan.
- Answers (2026-10-16, Windows SDK 10.0.102): a XAML `ContentView` and implicit styles from the library render in the head; the
  library's `MauiFont` (lucide.ttf) resolves in the head, which still registers it with `AddFont("lucide.ttf", "Lucide")`; files in
  `Platforms/Windows/` compile only for the Windows TFM; `PanGestureRecognizer` with a mouse on Windows was not verified, because
  the Windows partial of `TimelineView` takes all pointer input over and the shared gestures are attached only elsewhere.

### 1. Move existing UI into the library (no behavior change)

- Move `Theme.xaml`, `ThemeResources`, `ModalDialog`, `BeatBars`, the drawings and `PreviewSession`
  into `Orchid.Ui`; the head keeps `App`, `MauiProgram`, `WorkspacePage`, `CrashLog`.
- Create `tests/Orchid.Ui.Tests` (net10.0) and move the tests of the moved code.
- Check: same app on Windows as before; tests green on Windows and Linux.

### 2. Contracts, fakes, fixtures

- Add `Contracts/` and `Fakes/` as specified; fixtures as deterministic builders.
- Tests: the shared fixture table through `NoteIndex.HeldAt`, `ScriptedRecorder` phases and disconnect,
  store outcomes, fixture sizes (note counts, ranges) as stated in the DoD.

### 3. State

- `WorkspaceState`, `SetupState`, `LiveState`, `ReviewState`, `StatusState`; playback moves from
  `PreviewSession` (which is deleted), including seeking during playback and the end stop.
- The seeded eight-second demo disappears from production paths (D7); demos come only from `--fake`.
- Tests: mode transitions (Ready → CountIn → Recording → Review, Stop during count-in returns to Ready),
  locks while recording, pulse change resets the pattern, shared fixture through seek and playback,
  save failure keeps the recording, empty recording state.

### 4. Windows head on contracts

- Adapters, composition, `--fake` / `--fixture`. `WorkspacePage` binds to `WorkspaceState` and loses
  its own logic; no `Orchid.Platform.Windows` or `Microsoft.UI` usage remains in `Orchid.Ui` outside
  `Platforms/Windows/`.
- Check: real MIDI recording on Windows and the same flow on `--fake`.

### 5. Theme, metrics, strings, controls

- `Metrics.xaml`, `Palette`, `Strings.resx`; `SelectField`, `NumberStepper`, `HeldSlot`,
  `StatusBanner`; `BeatBars` wrapping and current beat; `ModalDialog` sheet, Escape, focus trap.
- Tests: pure parts (beat-bar row layout, stepper clamping and repeat on a manual `TimeProvider`).
- Check: Windows run in light and dark, 100 % and 200 % text scale.

### 6. Timeline core

- `NoteIndex`, `PitchMap`, `TimelineCamera`, `TimelineTransform`, `GridModel`, `OverviewColumns`,
  `LayoutRules`.
- Tests: the D10 M2, M4, M5, M6 lists (start/middle/end/empty camera, A0–C8 fits, fold mapping, zoom
  anchor and limits, grid counts for every pulse, no drift at 3,600 s, thinning), visible-note queries
  on `OneHour` without full scans.

### 7. Timeline view

- Drawables and `TimelineView` with shared gestures and the Windows input partial; the keyboard
  drawing and the `ScrollView` extent are removed.
- Check on Windows: `SurfaceLimit600s` at maximum zoom (no crash), notched wheel and precision
  touchpad (D10 M1), click vs drag, Locked/Free and Follow, overview seek and frame drag.

### 8. Views and the wide layout

- `AppHeader`, `SetupPanel`, `ReadyHome`, `LiveView`, `ReviewToolbar`, `MomentPanel`, `Legend`, modal
  bodies; `AdaptiveHost` with `WideShell`. `WorkspacePage` becomes a page hosting `AdaptiveHost`.
- Check on Windows: resize across `SidePanelMinWidth`, no-jump scenario with `ManyHeld` (bounds of
  timeline, overview and toolbar identical, D7), D9 baseline on `FiveMinutes` written to the head README,
  overlay split only if the baseline needs it.
- This phase closes the UI side of D6, D7, D10 and D12 for Windows; acceptance still needs D1–D5 for
  real data.

### 9. Compact layout and the next platform (after the desktop is accepted)

- D16 mockups agreed; `CompactShell`; add `net10.0-android` (or the agreed first mobile target) to
  `Orchid.Ui`; a mobile head with its own backend adapters (D8).
- Check on real devices only (D16).

Backend work (D1, D2, D3, D4, D5, D15) runs in parallel behind the contracts; each delivery changes
the Windows adapters, not the UI library.

## Open decisions

| Item | Proposal |
| --- | --- |
| Breakpoints | Compact below 600 wide or 480 high; side panel from 960 |
| Text font | Platform font now; embed one with tabular digits if jitter is visible |
| Renderer | `GraphicsView`; SkiaSharp only if the D9 baseline or rendering differences require it |
| Live polling rate | Display refresh (about 60 Hz) while live; capture is independent of it |
| One head per OS or one multi-target head | Decide at D8; the library supports both |
| Head name | Decided 2026-10-16: `Orchid.UiPreview` was renamed to `Orchid.Windows` |

## Implementation status (2026-10-16)

Implemented in `Orchid.Ui` and `tests/Orchid.Ui.Tests`: contracts, fakes and all fixtures, state, timeline core, drawables and
`TimelineView` with the Windows input partial, theme (`ColorTokens.xaml` instead of `Colors.xaml`, so the class does not clash with
`Microsoft.Maui.Graphics.Colors`; `Metrics.xaml` with touch values in code-behind instead of `OnIdiom`; `Styles.xaml`; `Palette`;
`Strings.resx` generated by MSBuild), the controls, views, `AdaptiveHost` and `WideShell`. The head composes fakes only and opens a
fixture with `--fixture <name>`; `--theme light|dark` forces a theme. `PreviewSession`, `WorkspacePage` and the drawings were deleted.

Deviations and decisions:

- Phase 4: `Backend/WindowsMidiInputCatalog`, `WindowsRecorder` and `WindowsLiveSession` wrap `WindowsRecordingSession`;
  `KeyPressPairing` pairs `KeyPressTiming`s into `NoteSpan`s. The real backend is the default, `--fake` selects the fakes. The
  backend has no count-in yet, so the adapter reports its first clicks as the count-in and shifts offsets by that span. Real
  recording was not verified: no MIDI input was connected to the machine during the check.
- The banner slot under the header holds the "PERFORMANCE" heading of the mockup in review; a problem banner covers it, so the
  slot never changes size. In Ready the banner sits at the top of the scrolling page.
- `AdaptiveHost` keeps one suspended modal: a `SelectField` list opened from Grid settings replaces that modal and returns to it.
- Compact uses the stacked wide arrangement until phase 9; the review toolbar then moves rare actions into an overflow modal.
- Minimum lane height 4 px instead of 8 px (see D10 M4).

Checked in the Windows window: Ready, count-in, recording with held keys, Stop into review and save, review of Narrow10s,
WideA0ToC8 (fits without vertical scrolling), Pedal (strip and tails), Empty, ManyHeld (10 keys fit the moment panel), OneHour
playback with Follow, light and dark themes, stacked layout below 960 px. Not checked: touch, pinch, pen, 200 % text scale,
`SurfaceLimit600s` at maximum zoom, the keyboard focus trap and Tab cycle, Space and Ctrl+0, the notched-wheel and touchpad
scenarios of D10 M1, and the D9 baseline. The working set with OneHour loaded was 320 MB during playback (D9 proposal: 300 MB).
