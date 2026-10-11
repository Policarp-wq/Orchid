# Orchid Development Rules

## Architecture and design

- `Orchid.Core` owns musical definitions, interval arithmetic and metronome timing. Keep device APIs and external integration libraries out of Core.
- `Orchid.Application` owns use cases and platform-neutral contracts. MIDI abstractions may live here; device access and library-specific MIDI types belong in Infrastructure.
- `Orchid.Infrastructure` implements audio, timing and other external adapters. Windows audio uses NAudio.Wasapi; live MIDI input uses DryWetMidi. Keep library-specific types here. File persistence has not been selected; Application has a draft in-memory `TimingStorage`.
- Pass dependencies through constructors and compose the small object graph explicitly in `Program`. A DI container is not part of the current design; add one only when concrete construction or lifetime complexity justifies it.
- Keep `Program` limited to composition and lifetime ownership. Put application behavior in named classes.
- Prefer straightforward code and existing tools. Add layers, interfaces, factories and dependencies only for a concrete requirement; future flexibility alone is insufficient.
- Assess proposed technical solutions before implementing them. Explain concrete costs and suggest a simpler alternative when a proposal worsens the design. Consider whether the mechanism can be added cheaply later; respect the user's informed final choice.

## Code and tests

- Keep project-facing text in English, including console output, documentation, comments and identifiers. Conversation with the user may remain in Russian.
- Preserve the existing code style and keep changes focused. Give classes one responsibility and keep methods small without introducing helpers that merely obscure a simple calculation.
- Use the existing MSTest projects. Write explicit Arrange, Act and Assert blocks separated by blank lines; keep independent behaviors in separate tests.
- Test observable requirements and meaningful edge cases. For a bug, reproduce it before changing production code. Do not weaken assertions to make tests pass.
- Use nullable reference analysis for non-null internal contracts instead of redundant `ThrowIfNull` guards. Nullable warnings are build errors. Validate external values and unsupported types at runtime; nullable analysis is not a runtime guarantee.

## Windows and WSL

- Windows is the current application target; the source files are shared through WSL. A successful Linux build alone does not validate the user's Windows workflow.
- Verify platform-sensitive changes with Windows SDK and Windows execution. Audio hardware checks must use Windows; report when they have not been performed.
- Preserve the default Windows `bin` and `obj` paths. For Linux checks, use `--artifacts-path /tmp/orchid-build` to avoid sharing generated files between SDKs. Do not change project output paths solely for the agent's environment.
- Read the SDK configuration and package references from `global.json` and the project files. Do not assume technologies or implemented features from documentation written before a revert.

## Verification and Git

- For code changes, build the solution and run relevant tests, including affected consumers. Report failures and skipped platform checks accurately.
- From the repository root, the current commands are `dotnet build Orchid.slnx` and `dotnet test --solution Orchid.slnx --no-build`. The test CLI uses Microsoft.Testing.Platform as configured in `global.json`.
- Treat design documents as requirements or proposals, not proof that a feature exists. Keep instructions consistent with the actual code when architecture or technology choices change.
- Preserve the user's uncommitted changes. Never commit or push directly to `main`; work on a dedicated branch and merge through a pull request after required checks pass.
