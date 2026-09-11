# Orchid Development Rules

- Keep all project-facing text in English, including console output, documentation, comments, and identifiers where applicable.
- Do not add MIDI domain logic to `Orchid.Core` or `Orchid.Application` unless explicitly requested.
- Prefer abstractions and dependency injection for new services when the requested scope requires them.
- Keep platform-specific code in the relevant presentation or infrastructure project.
- Keep `Program` limited to the composition root. Put application behavior in named classes.
- Give each class one clear responsibility. Split unrelated responsibilities into separate files.
- Keep methods small and focused. Extract meaningful private methods instead of accumulating procedural logic in one method.
- Do not introduce a new layer, service, interface, or abstraction without an explicit task requirement.
- Build and run the affected project after each implementation task when the environment supports it.
- The repository is shared between Windows and WSL. Do not change the host-specific output paths in `Directory.Build.props`; Windows uses `bin/Windows` and `obj/Windows`, while WSL uses `bin/Linux` and `obj/Linux`.
- Run MIDI device detection on Windows or macOS. The current DryWetMidi live input implementation does not enumerate devices on Linux/WSL.
