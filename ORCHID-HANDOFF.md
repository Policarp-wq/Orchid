# Orchid Windows handoff

Prepared on 2026-10-08. Inspect the current working tree before changing code; this is a session checkpoint, not proof of the copied repository's state.

## User constraints

- Follow the global AGENTS.md for communication and the repository AGENTS.md for development.
- Use informal, direct Russian in conversation. Project code, comments, tests and documentation remain English.
- Activate Ponytail full. Prefer the smallest complete change. Challenge proposals that worsen the design instead of agreeing automatically.
- Preserve the user's staged, unstaged and untracked work. The foundation is mostly uncommitted; cloning HEAD alone loses it.
- Do not fix Application/storage logic or concurrency. The user explicitly wants to observe those issues and devise solutions personally.
- Tests use MSTest with distinct Arrange, Act and Assert sections and blank lines.
- Keep abstractions in Application and external-library types in Infrastructure. Explicit constructor composition; no DI container.
- Do not alter Ponytail itself. Do not commit, push, publish or merge unrelated working-tree changes.
- Use Windows SDK and Windows execution for platform-sensitive validation. Read global.json instead of choosing arbitrary SDK versions.

## Repository and validation

The old checkout was `/home/policarp/ai_sandbox/work_projects/Orchid`, branch `refactor/aaa-tests-nullability`, base `b6e447a`. The intended new checkout is a local Windows path, such as `C:\dev\Orchid`.

Before validation, close the old Rider solution and remove only the copied checkout's generated bin/obj directories. Open the copied Orchid.slnx. Do not remove the original WSL checkout.

Windows SDK 10.0.102/runtime 10.0.2 were installed under `C:\Users\Policarp\.dotnet`. The maui-windows workload was installed there. Build the copied solution with that SDK or a compatible SDK allowed by global.json.

Last full Windows build: zero errors/warnings. Last tests: 174 passed, one expected platform-only skip, 175 total (Core 115, Infrastructure 44, UI preview 16). Rebuild before claiming this validates the copied checkout.

Commands from the repository root:

```powershell
dotnet build Orchid.slnx
dotnet test --solution Orchid.slnx --no-build
```

## Implemented pieces

- Core musical intervals and metronome timing; Application contracts and use cases.
- Infrastructure Windows audio: NAudio.Wasapi 3.1.0, NAudioBeatSoundPlayer.
- Infrastructure live MIDI: DryWetMidi 8.0.3, DryWetMidiInput forwarding into KeyListener.
- Draft Application TimingStorage has 20 ms bins. Its concurrency and precision limitations are deliberately preserved.
- tests/Orchid.AudioChecks is in the solution. Launch profiles: Orchid MIDI devices (`--midi-list`), Orchid MIDI recording (`--midi 0`), Orchid metronome (`--play`).
- MIDI enumeration last detected `0: 5704PIA-DK`. Real note recording remains unverified.
- Orchid.UiPreview is a standalone Windows MAUI window on sample data. It is not connected to real MIDI/storage/audio. It has persistent startup/managed exception logging and a Windows launcher.
- UI specification and implementation DoD: docs/orchid-design. See docs/midi-input.md, docs/metronome-audio.md, Orchid.UiPreview/README.md and docs/development-state.md. Some older verification entries in development-state are historical.

## Current unresolved issue

Rider's metronome launch crashed with FileNotFoundException HRESULT 0x8007007E in NAudio.CoreAudioApi.ComActivation.CoCreateInstance, while constructing MMDeviceEnumerator, before playback.

However, the exact built AudioChecks executable with --play subsequently completed successfully from Windows PowerShell both through the WSL UNC path and after a temporary local Windows copy. The cause is unconfirmed; do not claim UNC alone caused this audio crash or change the audio backend blindly.

The user decided to copy Orchid to the Windows filesystem and run the agent natively with sandbox restrictions. Next: confirm active permissions, local working directory and skills; reproduce the Rider launch against the local checkout, compare its environment/runtime to the successful PowerShell execution, and fix only a demonstrated cause.
