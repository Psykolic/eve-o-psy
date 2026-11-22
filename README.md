# EVE-O Preview (Custom)

A task switcher for multiple EVE Online clients: live thumbnails of every client, one click or
hotkey to bring one to the foreground.

Repository: `Psykolic/eve-o-psy`. Current version: **8.3.10**.

## Contributors

- **Psykolic** — maintainer of this edition;
- **Lord Smurfalot** — contributor;
- **ArthoriusFM** — contributor;
- **Phrynohyas Tig-Rah** — original EVE-O Preview author;
- everyone in [Proopai/eve-o-preview](https://github.com/Proopai/eve-o-preview), the upstream
  project this edition is built on.

See [CONTRIBUTORS.md](CONTRIBUTORS.md). Missing someone? Open a PR.

## What it does

- **Live thumbnails** of each EVE client (DWM when available, GDI as fallback), with per-client
  size, position and opacity.
- **Spotlight** — enlarged, always-on-top view of chosen clients, with optional crop region.
- **Overlay labels** — short per-client label (up to 8 characters) on top of the thumbnail.
- **Cycle groups** — group clients and rotate between them with your own hotkeys.
- **Event-driven refresh** — alt-tab, minimize and window changes update immediately.
- **Optional metrics** — `EVE_METRICS=1` writes a 30-second snapshot (refresh time, GDI/DWM
  handles, CPU) to a local file.
- **Dark theme** for the settings window (switchable in *General*).

It is a task switcher only: it does not inject input into the game, does not modify the EVE
interface and does not broadcast keyboard or mouse events.

## Install

Download the package for the latest release:

| Asset | Download | Extracted | Requires |
|---|---|---|---|
| `Release-vX.Y.Z.zip` | ~68 MB (485 files) | ~162 MB | nothing — the .NET 8 runtime is inside |
| `EVE-O-Preview-vX.Y.Z-win-x64-singlefile.zip` | ~0.85 MB (one `EVE-O-Preview.exe`, ~2.4 MB) | ~2.4 MB | **.NET 8 Desktop Runtime** |

The two packages are the same app. The big one carries the runtime (works on a clean machine); the
small one is a single executable that uses the .NET runtime already installed on Windows. Not sure
which to pick? Take `Release-vX.Y.Z.zip`. Check the runtime with `dotnet --list-runtimes`
(`Microsoft.WindowsDesktop.App 8.x`).

Extract anywhere **except** `Program Files` — the app stores `EVE-O-Preview.json` next to the
executable. Start the app and your clients (order does not matter).

## Build from source

Requirements: .NET 8 SDK. Building the whole solution on Linux works through
`EnableWindowsTargeting`; only the WinForms/Win32 desktop tests require Windows.

```bash
make check        # build + core tests (runs on Linux or Windows)
make pack         # publish win-x64 to dist/
```

On Windows you can also run the full test suite:

```bash
dotnet test src/EveO.Tests/EveO.Tests.csproj -c Release
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow and [docs/testing](docs/testing) for the
test layout.

## Documentation

| Doc | What is there |
|---|---|
| [docs/metrics.md](docs/metrics.md) | what is measured and real measurements (refresh, GDI, CPU) |
| [docs/spotlight-mode.md](docs/spotlight-mode.md) | spotlight behavior and crop |
| [docs/thumbnail-sizing.md](docs/thumbnail-sizing.md) | size policy, aspect ratio, per-client sizes |
| [docs/architecture](docs/architecture) | architecture notes and decisions |
| [docs/audits](docs/audits) | ADRs, performance and stability analyses |
| [CHANGELOG.md](CHANGELOG.md) | what changed in each version |
| [docs/releases](docs/releases) | release notes, with measurements and decisions |

## License

Distributed as a whole under **GNU GPL v3.0 only** (`GPL-3.0-only`). The original MIT license and
copyright notice are preserved in [LICENSES/MIT.txt](LICENSES/MIT.txt). See [LICENSE](LICENSE) and
the [provenance record](docs/PROVENANCE.md).

## Disclaimer

EVE Online and the EVE logo are the registered trademarks of CCP hf. This project is not
affiliated with or endorsed by CCP. Do **not** use it for anything that breaks the EVE Online EULA
or Terms of Service. If a feature could be understood that way, treat it as a bug and report it.
