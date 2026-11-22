# Contributing

Thanks for taking the time to help. This project is small, so the rules are short.

## Before you start

- **.NET 8 SDK** is enough to build. On Linux, the whole solution compiles via
  `EnableWindowsTargeting`; the desktop tests (DWM/GDI/hotkey) only run on Windows.
- Bug reports and feature ideas: open an issue with the version, Windows build, number of EVE
  clients and, if possible, the metrics line (`EVE_METRICS=1`).

## Build and test

```bash
make check      # build + core tests (206 tests, Linux or Windows)
make pack       # publish win-x64 to dist/
```

Windows-only, with a real desktop session:

```bash
dotnet test src/EveO.Tests/EveO.Tests.csproj -c Release
```

## Pull requests

1. Branch from `main` with a descriptive name (`fix/spotlight-resize`, `docs/...`).
2. Keep the change focused: one behavior per PR.
3. **Behavior changes need a test.** Core logic goes in `EveO.Core.Tests` (runs everywhere);
   WinForms/Win32 code goes in `EveO.Tests`.
4. CI must be green (build on Linux, core tests, Windows tests).
5. Do not commit secrets, tokens or machine-specific paths.
6. Configuration changes must keep `ConfigVersion` compatibility and read old files without
   losing settings.

## Style

- C# with the existing conventions (tabs, braces on their own line, Portuguese comments in the
  domain code, English for public APIs).
- Prefer small, testable classes in `EveO.Core`; UI and Win32 stay in `EveO.Presentation` /
  `EveO.Infrastructure`.
- No new dependency without a reason: the app ships self-contained and small.

## Releases

Releases are cut from `main` by the maintainer. The version comes from the tag (`vX.Y.Z`) and the
release notes live in `docs/releases/`.
