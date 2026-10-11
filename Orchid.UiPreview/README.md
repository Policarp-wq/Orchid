# Orchid MAUI UI preview

Windows C#/XAML workspace connected to the existing MIDI and metronome adapters.
Select an input, BPM and pulse, then use Start session / Stop. Recording runs through
DryWetMidiInput, the existing MIDI converter, KeyListener and TimingStorage while
MusicPlayThrough sends clicks to NAudioBeatSoundPlayer.

Included: MIDI input selection/refresh, recording with metronome, timeline with chromatic pitch lanes, configurable BPM/pulse/review grid,
exact stored attack selection, seeking, visual Play/Pause/Rewind, simultaneous held-key
highlights during capture/review, the latest held note, moment details, light/dark
themes and a narrow-window layout. Colors, type and spacing scales and button/label
styles live in `Theme.xaml` (tokens from `docs/orchid-design/UI-SPEC.md`, Light/Dark pairs);
the timeline and keyboard drawings read the same tokens through `PreviewColors`, so
piano keys and notes follow the theme. Icons are Lucide font glyphs
(`Resources/Fonts/lucide.ttf`, ISC license in `docs/orchid-design/THIRD-PARTY-NOTICES.txt`).
The timeline keeps 160 pixels per second and 24 pixels per pitch row, with pinned
pitch labels and horizontal scrolling following the playhead.

## Run on Windows

Install the `maui-windows` workload in the .NET 10 SDK used for this project:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" workload install maui-windows
```

From the Orchid repository, use Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Orchid.UiPreview\run-preview.ps1
```

The launcher prefers the SDK in `$env:USERPROFILE\.dotnet`, matching the current
Rider setup. Pass `-Dotnet 'C:\path\to\dotnet.exe'` to choose another installation.
It sets the apphost's runtime location and builds only this independent project.
For a WSL source checkout, it runs a temporary local Windows copy of the binaries:
WinUI COM activation failed when launched directly from the UNC share during
verification. Default `bin`/`obj` paths are preserved; the temporary copy is removed
after closing the preview.

In Rider, add a local external tool under Settings > Tools > External Tools:

| Field | Value |
| --- | --- |
| Name | `Orchid UI preview` |
| Program | `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` |
| Arguments | `-NoProfile -ExecutionPolicy Bypass -File "$SolutionDir$Orchid.UiPreview\run-preview.ps1"` |
| Working directory | `$SolutionDir$` |

Run it from Tools > External Tools > Orchid UI preview. This uses the same verified
launcher and WSL/local-copy behavior. It is a normal launch; IDE breakpoints require
attaching the debugger to the started `Orchid.UiPreview` process. The actual Rider
action has not been exercised during Windows CLI validation.
[Rider external tools](https://www.jetbrains.com/help/rider/Configuring_Third-Party_Tools.html).

During recording, setup and review controls are locked; elapsed time, the latest
held note and keyboard highlights refresh every 20 ms. The interval is copied from
TimingStorage for now, while MIDI notifications supply the live display state.
After Stop, recorded Press/Release events are paired for the timeline. Held keys
are closed visually at Stop; an unmatched release is ignored and a repeated press
closes its previous interval. Review notes use MIDI pitches reconstructed from
stored intervals relative to the recording's fixed home sound C4.

No demo notes appear in the window. Empty recordings retain their duration.
Recorded note intervals appear after Stop; file saving/loading and chord naming
are not implemented. Open log stays disabled. Recordings
remain in memory, and Start replaces the previous review. For narrower windows,
vertical scrolling keeps all sections reachable.

The user's Application/storage code and DryWetMidiInput callback behavior were
preserved. Storage still groups timestamps into 20 ms bins and does not retain
channel/velocity. Concurrent callbacks and callbacks outliving Stop remain known
limitations. Capture and elapsed time share a session origin; metronome startup
follows MIDI listening startup, without hardware latency calibration.

## Checks

```powershell
dotnet test --project tests/Orchid.UiPreview.Tests/Orchid.UiPreview.Tests.csproj
```

The AAA MSTest checks cover held-key state, exact releases, pause/resume,
late refresh at the end, pulse durations and reconstruction of recordings.
Infrastructure session tests cover actual converter/listener/storage forwarding,
cancellation, failures and pulse accents. Actual MAUI rendering and controls also
require a window check; physical key presses and audible click quality require hardware.

## Crash diagnostics

The Windows entry point installs logging before WinUI starts. It writes startup,
normal shutdown, exceptions reaching `Main`, WinUI unhandled exceptions,
AppDomain unhandled exceptions and unobserved task exceptions to:

```text
%LOCALAPPDATA%\Orchid\UiPreview\Logs\preview-YYYY-MM-DD-PID.log
```

Records include UTC time, process/thread IDs, the interception point and exception
details with available stack traces and inner exceptions. Files survive launcher
cleanup. A logging I/O failure does not replace the original exception. WinUI
exceptions remain unhandled; logging does not attempt to resume a damaged UI.

These are last-chance diagnostics. A native fail-fast, stack overflow, forced
termination or failure before managed `Main` is not guaranteed to reach them.
WinUI may also expose less exception detail than the original managed failure.
For those cases use Windows Error Reporting dumps or a debugger.
[WinUI exception behavior](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception).

Historical standalone-preview verification: 16 MSTest cases passed; timeline selection, held keys,
Play/Pause/Rewind, both themes and narrow layout were checked in the native
window. A temporary deliberate exception in `Main` exited with code 1 and left
its stack trace and inner exception in the persistent log. The probe was removed.

2026-10-08 integration verification: Windows solution build passed with zero
warnings/errors. UI tests: 25 passed. Infrastructure tests: 52 passed and one
expected non-Windows-only skip. Three existing MusicalNote invalid-enum tests
still fail, as they did before this integration. Native UI Start/Stop with
5704PIA-DK completed, locked/unlocked controls and retained a 10.23-second empty
recording. Physical key presses, disconnect and audible metronome quality were
not verified. See [integration checkpoint](../docs/ui-recording-integration.md).
