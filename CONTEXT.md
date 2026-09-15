# ClipVault Context

ClipVault is a Windows clipboard-history tool. It watches the clipboard, keeps what was copied, and lets the user pick an earlier copy from a popup menu.

## Glossary

| Term | Meaning |
|---|---|
| **Clip** | One captured clipboard entry. Either plain Unicode text or a bitmap image. Never both. |
| **History** | The ordered list of Clips, newest first, capped at the configured number of stored items. Kept across restarts. |
| **Capture** | Adding what is currently on the system clipboard to the History. Content the source app marks as private, oversized content, and ClipVault's own clipboard writes are never captured. |
| **Popup** | The menu listing the History, opened with the history hotkey. Rows are numbered; image rows show a thumbnail and "(BITMAP)". |
| **Choose** | The user picking a Clip in the Popup. The Clip goes onto the system clipboard and becomes the Last Chosen Clip. |
| **Last Chosen Clip** | The Clip most recently chosen. Shown in bold in the Popup and preselected when the Popup opens. At most one exists. |
| **Insert** | Pasting the clipboard into the Previous Window by sending Ctrl+V. Happens after Choose only when the auto-insert setting is on; always happens for a Template. |
| **Previous Window** | The application window that had focus when the Popup was opened. It regains focus when the Popup closes, whatever closed it. |
| **Template** | A named, predefined piece of text with an optional hotkey. Pressing the hotkey or picking it from the Popup's Template submenu Inserts it and then restores what was on the clipboard before. Templates are never captured into the History. |
| **Hotkey** | A global key combination (modifiers plus a key) that works in every application. The history hotkey opens the Popup; a Template hotkey Inserts that Template. |
| **Evict** | Dropping the oldest Clip when the History exceeds its cap, or all Clips beyond a newly lowered cap. |
| **Duplicate** | A Capture whose content already exists in the History. The existing Clip moves to the top instead of a second Clip being added. |
