# Orchid UI handoff

Copy this entire folder into the main Orchid repository, for example as `docs/ui-design/`. All references below are relative to this folder. No changes were made to the copied Orchid repository.

## Start here

1. Read [UI-SPEC.md](UI-SPEC.md) for the intended behavior, design decisions, scope, and implementation notes.
2. Open [workspace-light.png](workspace-light.png) for the primary desktop layout.
3. Open [mockup.html](mockup.html) in a modern browser and move **Session position**. The playhead, current-time details, and piano keys change together. Click a recorded note or use playback to inspect the same behavior.
4. Read [IMPLEMENTATION-DOD.md](IMPLEMENTATION-DOD.md) for the current code gaps, required recording data and acceptance criteria. Its working-tree assessment is newer than the implementation snapshot in the UI specification.
5. Run the [Windows app](../../Orchid.Windows/README.md) to inspect the workspace with a real MIDI input, or with `--fake` and `--fixture <name>` on sample data without the recording backend.

## Files

| File | Purpose |
| --- | --- |
| `UI-SPEC.md` | Implementation handoff for the next agent |
| `IMPLEMENTATION-DOD.md` | Independent prototype acceptance, integration gaps, required data and delivery criteria |
| `UI-IMPLEMENTATION-PLAN.md` | UI library split, backend contracts and fakes, components, layouts and build phases |
| `workspace-light.png` | Primary desktop composition, light theme |
| `workspace-dark.png` | Same composition, dark theme |
| `workspace-narrow.png` | Narrow-window reflow of the desktop composition |
| `practice-light.png` | Alternative desktop composition with settings on the left |
| `mockup.html` | Self-contained interactive design reference, including both variants |
| `THIRD-PARTY-NOTICES.txt` | License for the bundled Lucide icons |

The mockup contains an eight-second simulated performance. It does not connect to a MIDI device, play audio, read Orchid logs, or save recordings. Its buttons illustrate UI states only. The carousel and its design picker belong to the preview, not the Orchid application.

The selected implementation direction is **.NET MAUI with shared C#/XAML UI for Windows, macOS, Android, and iOS**. Linux is deferred. Treat the HTML as an interaction and visual reference; implement the production app in MAUI rather than treating this browser prototype as the app.
