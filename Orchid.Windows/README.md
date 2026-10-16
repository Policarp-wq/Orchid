# Orchid for Windows

The Windows head of the UI library `Orchid.Ui`. By default it records from a real MIDI input through
`Orchid.Platform.Windows` (DryWetMidi input, KeyListener, TimingStorage, NAudio metronome); `--fake` replaces the input
and recorder with two fake inputs and a `ScriptedRecorder` that plays the five-minute fixture. Recordings stay in an
in-memory store in both modes until a file format exists (IMPLEMENTATION-DOD D5). Views, theme, controls, the timeline
and the state live in `Orchid.Ui`; this project holds only `App`, `MauiProgram`, the adapters in `Backend/`, `CrashLog`
and the WinUI entry point.

Limits of the real backend, all in the adapters until Application delivers D1/D2/D15: timestamps are grouped into
20 ms bins, channel and velocity are not kept, MIDI pitches are rebuilt from tones relative to C4, the metronome accents
only beat 1 whatever the strong-beat pattern, the count-in is the first bars of the backend's own clicks (notes played
in it keep negative offsets), and the sustain pedal is not captured. A device or audio failure during a session ends it
with a banner and keeps what was captured.

Arguments: `--fake` uses the fakes, `--fixture <name>` opens a fixture from `Orchid.Ui/Fakes/Fixtures.cs` in review (Shared, Empty,
TwoEvents10MsApart, SamePitchTwoChannels, Narrow10s, WideA0ToC8, FiveMinutes, OneHour, SurfaceLimit600s, ManyHeld,
Pedal); `--theme light|dark` forces a theme. Without a fixture the window starts in Ready. Open log asks the in-memory
store, so a file from disk always reports "not found". Icons are Lucide font glyphs (`Orchid.Ui/Resources/Fonts/lucide.ttf`,
ISC license in `docs/orchid-design/THIRD-PARTY-NOTICES.txt`).

## Run on Windows

Install the `maui-windows` workload in the .NET 10 SDK used for this project:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" workload install maui-windows
```

From the Orchid repository, use Windows PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Orchid.Windows\run.ps1 --fixture FiveMinutes
```

Arguments after the script options are passed to the executable.

The launcher prefers the SDK in `$env:USERPROFILE\.dotnet`, matching the current
Rider setup. Pass `-Dotnet 'C:\path\to\dotnet.exe'` to choose another installation.
It sets the apphost's runtime location and builds only this independent project.
For a WSL source checkout, it runs a temporary local Windows copy of the binaries:
WinUI COM activation failed when launched directly from the UNC share during
verification. Default `bin`/`obj` paths are preserved; the temporary copy is removed
after closing the app.

In Rider, from a Windows checkout, use the launch profiles of `Properties/launchSettings.json`: they appear as run
configurations `Orchid.Windows: Orchid (MIDI)`, `Orchid (fake input)`, `Orchid (review FiveMinutes)` and
`Orchid (review OneHour, dark)` and run with the debugger attached. Do not use a `UWP` run configuration: the head is
unpackaged (`WindowsPackageType=None`). Rider builds with the .NET SDK it is configured with (Settings > Build,
Execution, Deployment > Toolset and Build); that SDK needs the `maui-windows` workload, otherwise the build fails with
NETSDK1147 naming some `maui-*` workload such as `maui-tizen`. The started apphost finds the .NET 10 runtime in
`Program Files\dotnet`; with .NET 10 installed only under `%USERPROFILE%\.dotnet`, set `DOTNET_ROOT` in the run
configuration.

For a WSL checkout, add a local external tool under Settings > Tools > External Tools:

| Field | Value |
| --- | --- |
| Name | `Orchid` |
| Program | `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` |
| Arguments | `-NoProfile -ExecutionPolicy Bypass -File "$SolutionDir$Orchid.Windows\run.ps1"` |
| Working directory | `$SolutionDir$` |

Run it from Tools > External Tools > Orchid. This uses the same verified
launcher and WSL/local-copy behavior. It is a normal launch; IDE breakpoints require
attaching the debugger to the started `Orchid.Windows` process. The actual Rider
action has not been exercised during Windows CLI validation.
[Rider external tools](https://www.jetbrains.com/help/rider/Configuring_Third-Party_Tools.html).

## Checks

```powershell
dotnet build Orchid.slnx
dotnet test --solution Orchid.slnx --no-build
```

`tests/Orchid.Ui.Tests` (net10.0, also runs on Linux with `--artifacts-path /tmp/orchid-build`) covers the fixtures, fakes,
timeline core, state and the pure control logic. `tests/Orchid.Windows.Tests` keeps the crash log tests. Rendering and
input need a window check; see "Implementation status" in the plan for what was and was not checked on 2026-10-16.

## Crash diagnostics

The Windows entry point installs logging before WinUI starts. It writes startup,
normal shutdown, exceptions reaching `Main`, WinUI unhandled exceptions,
AppDomain unhandled exceptions and unobserved task exceptions to:

```text
%LOCALAPPDATA%\Orchid\Logs\orchid-YYYY-MM-DD-PID.log
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

2026-10-15: Windows SDK 10.0.102 builds the solution with zero warnings/errors;
211 tests pass with one expected non-Windows-only skip. The MusicalNote invalid-enum
failures above no longer occur. No window or hardware check was repeated.
