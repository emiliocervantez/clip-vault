# ClipVault

Clipboard history for Windows 10 and 11. Runs in the tray, remembers what you copy (Unicode text and images), and lets you paste any earlier copy from a numbered popup menu.

## Features

- History of up to 500 clips, kept across restarts in `%LOCALAPPDATA%\ClipVault`.
- Global hotkey opens the popup at the text caret (default `Ctrl+Shift+V`).
- Keyboard: arrows, `Enter`, digits `1`-`9`, `Del` removes a clip, `Esc` closes.
- The last chosen clip is bold and preselected.
- The window that had focus before the popup opened gets it back when the popup closes.
- Templates: predefined text with optional hotkeys, also reachable from the popup's Template submenu.
- Settings: number of stored items, move chosen item to top, insert automatically, start with Windows, hotkeys, templates.
- Content that password managers mark as private is never stored.

## Build

Requires the .NET 8 SDK.

```
.\build.cmd            # Debug build + tests
.\build.cmd -Release   # Release build + tests
.\build.cmd -NoTest    # skip tests
```

## Publish (self-contained, single exe)

```
.\publish.cmd                  # -> publish\ClipVault.exe
.\publish.cmd -Output C:\tmp   # custom folder
.\publish.cmd -StopRunning     # first stop a ClipVault started from the output folder
```

The `.cmd` files run the matching `.ps1` with `-ExecutionPolicy Bypass`, so they work even when running
PowerShell scripts is disabled on the machine. If your policy allows scripts, `.\build.ps1` and `.\publish.ps1`
take the same arguments.

The result is a single `ClipVault.exe` for win-x64. No .NET runtime needs to be installed on the target machine.
The script refuses to overwrite an exe that is currently running unless `-StopRunning` is given.

## Layout

- `src/ClipVault.Core` - history, settings, storage. No UI dependencies.
- `src/ClipVault` - WPF app: clipboard monitor, hotkeys, popup, settings window, tray icon.
- `tests/ClipVault.Tests` - xUnit tests for the core.
- `CONTEXT.md` - glossary of domain terms.
- `docs/adr` - architecture decision records.
