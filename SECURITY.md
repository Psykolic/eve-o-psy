# Security policy

## Supported versions

Only the latest release receives fixes. Check [docs/releases](../docs/releases) for the current
version.

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Use GitHub's private vulnerability
reporting (Security → Advisories → Report a vulnerability) on this repository.

Include:

- affected version and Windows build;
- steps to reproduce;
- impact (for example: reading the configuration file, arbitrary window manipulation, token
  exposure in logs).

You can expect an initial answer within a few days.

## Scope

In scope: the application itself (WPF/WinForms, DWM/GDI interop, configuration handling) and the
packaging scripts in this repository.

Out of scope: issues that require modifying EVE Online, breaking the game's EULA/ToS, or that
depend on third-party software (Wine, DWM itself, the .NET runtime).
