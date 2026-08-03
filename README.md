# Icarus Dedicated Server

[![WindowsGSH](.github/assets/windowsgsh-badge.svg)](https://windowsgsh.com)
[![Status](https://img.shields.io/badge/status-beta_candidate-1E8449)](#status)
[![Module version](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FWindowsGSH%2FWindowsGSH.Icarus%2Fmain%2FIcarus.mod%2Fmodule.json&query=%24.version&prefix=v&label=module&color=1E8449)](Icarus.mod/module.json)
[![Requires WindowsGSH](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FWindowsGSH%2FWindowsGSH.Icarus%2Fmain%2FIcarus.mod%2Fmodule.json%3Fbadge%3Dminimum&query=%24.minimumWindowsGshVersion&prefix=v&label=requires%20WindowsGSH&color=2563EB)](Icarus.mod/module.json)
[![Licence](https://img.shields.io/badge/licence-MIT-64748B)](LICENSE.md)

This WindowsGSH module installs, configures, starts, queries, imports, and backs up an Icarus dedicated server.

## Status

**BETA CANDIDATE.** Version `0.3.1` aligns its launch and managed data paths with the current Icarus server contract and reports its executable, configuration, and unproven headless-shutdown state through Readiness Check. A current live server must still prove A2S, lifecycle, remote joining, updates, and restore behavior before the module is marked beta tested.

## Installation

WindowsGSH installs SteamCMD app `2089300` using anonymous login and validation. The server executable is `Icarus/Binaries/Win64/IcarusServer-Win64-Shipping.exe`.

Import `Icarus.mod`, create a server, choose the game and query ports, set strong join/admin passwords, and install it. WindowsGSH launches with `-UserDir=<install>/Icarus`, so configuration, logs, prospects, and backups all use the managed `Icarus/Saved` tree instead of `%LocalAppData%`.

Existing installs can be imported by selecting either the Icarus install directory or a WindowsGSM server directory containing `serverfiles`. WindowsGSH checks the executable and previews settings from `Icarus/Saved/Config/WindowsServer/ServerSettings.ini` when present.

## Configuration

WindowsGSH preserves unknown keys within the managed Icarus settings section while writing these values to `Icarus/Saved/Config/WindowsServer/ServerSettings.ini`:

- Session name, join password, admin password, and maximum players. Icarus currently supports 1–8 players.
- `ShutdownIfNotJoinedFor` and `ShutdownIfEmptyFor`. Use `-1` for indefinite operation, `0` for immediate return to the lobby, or a positive number of seconds.
- Permissions for non-admin prospect launch/deletion.
- Load, create, resume, and last-prospect values.

The session name is also passed through `-SteamServerName`, because Icarus does not use `SessionName` for its browser name. WindowsGSH owns `-log`, `-UserDir`, `-PORT`, `-QueryPort`, and `-SteamServerName`; duplicate values are removed from Additional Arguments.

## Networking

| Purpose | Default | Protocol | Exposure |
| --- | ---: | --- | --- |
| Game traffic | `17777` | UDP | Public; eligible for firewall guidance and UPnP |
| Steam query | `27015` | UDP | Public; eligible for firewall guidance and UPnP |

Both ports are configurable and required for the documented direct-connect/server-browser setup. The Public IP setting is display/import metadata; it does not bind the server or change router configuration. UPnP operates only when the server's WindowsGSH forwarding policy is explicitly set to an automatic mode.

## Query, console, and administration

WindowsGSH queries A2S locally on `127.0.0.1:<query port>` and can display the returned status, players, version, and map. Version `0.3.0` no longer passes the unsupported `-NOSTEAM` argument that conflicted with Steam query behavior.

The module captures redirected process output for the embedded console view, but useful standard-input command delivery has not been proven. Icarus calls its in-game chat administration commands “RCON”; WindowsGSH does not currently implement a separate network RCON client for them. The admin password is still written because the game uses it for `/AdminLogin`.

## Files and backups

- Executable: `Icarus/Binaries/Win64/IcarusServer-Win64-Shipping.exe`
- Configuration: `Icarus/Saved/Config/WindowsServer/ServerSettings.ini`
- Prospects/player data: below `Icarus/Saved/PlayerData`
- Logs and crash output: below `Icarus/Saved/Logs` and the managed Saved tree
- Backup target: the complete `Icarus/Saved` directory

The former separate Config backup target was redundant because it sits inside `Icarus/Saved`. Stop the server before restoring and retain a separate copy until the restored prospect has been opened successfully.

## Known limitations

- A2S responsiveness and player-count accuracy require confirmation against the current live build.
- WindowsGSH does not implement Icarus' in-game administration command channel as network RCON.
- No proven graceful headless shutdown mechanism is available. Normal Stop waits briefly for a window close and then terminates the process; session-ending graceful-stop capability is intentionally not advertised.
- Saving rewrites the managed settings section and does not preserve comments or unrelated INI sections.
- Existing imports that previously stored data in `%LocalAppData%\Icarus` may need their Saved data copied into the managed install tree before first start with `0.3.0`.

## Beta verification checklist

- [ ] Fresh-install Steam app `2089300`, confirm the executable, and verify the process attaches to the correct server card.
- [ ] Round-trip every managed setting and confirm the server reads `Icarus/Saved/Config/WindowsServer/ServerSettings.ini` through the generated `-UserDir` argument.
- [ ] Start, stop, crash, restart WindowsGSH while the server runs, and verify PID reattachment plus honest forced-stop behavior.
- [ ] Confirm direct joining, server-browser discovery, both UDP ports, UPnP mappings, A2S status, and player counts.
- [ ] Test update/validation, prospect creation/resume, complete Saved backup, and restore to a disposable copy.

## Support

Report module problems through the [WindowsGSH.Icarus issue tracker](https://github.com/WindowsGSH/WindowsGSH.Icarus/issues). Include the module and WindowsGSH versions, sanitized configuration, generated launch arguments with passwords removed, relevant logs, and the exact lifecycle/query operation. Never post join/admin passwords or private prospect data.

## Support development

If you like the work I do and would like to support continued WindowsGSH and module development, you can contribute here:

- [Ko-fi](https://ko-fi.com/shenniko)
- [PayPal](https://paypal.me/shenniko)

## Trust and source

Modules execute with the same Windows permissions as WindowsGSH. Download releases from a source you trust and review [`IcarusModule.cs`](Icarus.mod/IcarusModule.cs) and [`module.json`](Icarus.mod/module.json) before running them. See [SECURITY.md](SECURITY.md) for safe-download, credential-handling, and private vulnerability-reporting guidance. Game-specific values were checked against RocketWerkz's [server setup](https://github.com/RocketWerkz/IcarusDedicatedServer/wiki/Server-Setup) and [configuration reference](https://github.com/RocketWerkz/IcarusDedicatedServer/wiki/Server-Config-%26-Launch-Parameters).
