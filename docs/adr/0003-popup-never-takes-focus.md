# 3. The popup never takes focus; keys arrive through a low-level hook

Date: 2026-09-17

## Status

Accepted. Supersedes [ADR 1](0001-popup-takes-focus-and-hands-it-back.md).

## Context

ADR 1 chose to activate a small anchor window so a normal WPF `ContextMenu` could receive keyboard input, then hand focus back on close. In use this had a cost that only shows with certain applications: any transient popup in the previously focused app closes the moment that app loses focus. Cursor's and VS Code's quick-open widgets do this by default (`workbench.quickOpen.closeOnFocusLost`), so ClipVault could not be used to paste into them.

The two options from ADR 1 were re-evaluated with that in mind.

## Decision

Option 1 from ADR 1. The popup is a plain WPF window with `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`, shown with `SWP_NOACTIVATE`. The foreground window never changes while it is open.

While the popup is open three hooks are installed and removed on close:

- `WH_KEYBOARD_LL`: every non-modifier key press is forwarded to the popup (Up/Down/PageUp/PageDown/Home/End, Enter/Space, digits, Delete, Esc, the history hotkey to toggle) and swallowed, so the focused application sees none of it. Modifier keys pass through so the application's modifier state stays consistent. Handling is dispatched asynchronously; the hook callback itself returns immediately, well within Windows' low-level hook timeout.
- `WH_MOUSE_LL`: a button press outside the popup rectangle closes it (a non-activated window cannot use mouse capture for this).
- `EVENT_SYSTEM_FOREGROUND` win-event: a foreground change (Alt+Tab, clicking another app) closes it.

Mouse hover and clicks inside the popup work natively; non-activated windows still receive mouse messages, and Windows 10+ routes the wheel to the window under the pointer.

Templates are not listed in the popup; they are reached through their hotkeys only.

### Lone-modifier masking

Because the app underneath keeps focus, it now sees the hotkey's modifiers go down and up while the hotkey's own key is consumed by `RegisterHotKey`. A Win32 app that sees Alt go down and up with nothing in between enters menu-bar keyboard mode (Win alone opens Start), and the Ctrl+V sent afterwards lands there. Verified with `GetGUIThreadInfo` reporting `GUI_INMENUMODE` on Notepad2 after Alt+C when Alt is released before C. Mitigation, the same one AutoHotkey uses: right after the hotkey fires, if Alt or Win is still physically down, tap Ctrl so the release is no longer "lone". The Ctrl+V sender also presses Ctrl before releasing any modifier the user still holds, and sends Esc first if the foreground thread is already in menu mode.

## Consequences

- The previous application keeps focus and its transient popups throughout. No focus restoration code exists anymore, and Ctrl+V can be sent right after the popup closes.
- Because the popup is not a menu, ClipVault owns its own navigation, hover-after-movement rule, scrolling and placement (`PopupWindow`). That is more code than the `ContextMenu` version but all of it is straightforward.
- Low-level hooks are global for their lifetime. They exist only while the popup is open, typically well under a second, and are removed on close in every path (choose, cancel, outside click, foreground change).
- Anti-cheat or security software that objects to low-level keyboard hooks would object only during those moments.
