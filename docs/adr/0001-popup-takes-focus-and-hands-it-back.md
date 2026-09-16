# 1. The popup takes foreground focus and hands it back on close

Date: 2026-09-15

## Status

Accepted

## Context

The popup must be driven by keyboard (arrows, Enter, digits, Del, Esc) and must leave the previously focused application focused after it closes, so that a following Ctrl+V lands in that application.

Windows delivers keyboard input to the foreground thread. Two ways exist to combine keyboard use with "the previous app keeps focus":

1. Never activate the popup (`WS_EX_NOACTIVATE`) and read keys through a low-level keyboard hook while the previous app stays foreground.
2. Activate a tiny anchor window, run a normal WPF `ContextMenu` on it, remember the previous foreground window, and call `SetForegroundWindow` on it when the menu closes.

## Decision

Option 2. A 1x1 transparent anchor window is placed at the caret, brought to the foreground, and the menu is opened relative to it. `ContextMenu.Closed` restores the previous window before any Ctrl+V is sent.

Foreground changes use plain `SetForegroundWindow` only. The global hotkey grants foreground rights on open, and on close ClipVault itself is the foreground process, so no workaround for the foreground lock is needed. The two common workarounds were tried and rejected: `AttachThreadInput` stalls Electron apps (Slack) for several seconds after the popup closes, and a synthetic Alt tap puts them into menu-bar mode, which swallows the next shortcut.

## Consequences

- Menu navigation, scrolling, hover, submenus and click-outside closing come from WPF for free.
- The previous app briefly loses focus while the popup is open. Apps that react to focus loss (some games, some remote-desktop clients) may notice.
- Foreground restoration depends on `SetForegroundWindow` succeeding; the workarounds cover the known refusals but a future Windows change could require revisiting.
- Option 1 remains possible later but would replace the whole popup input layer.
