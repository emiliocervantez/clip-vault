# ClipVault Context

ClipVault is a Windows clipboard-history tool. It watches the clipboard, keeps what was copied, and lets the user pick an earlier copy from a popup menu.

## Glossary

| Term | Meaning |
|---|---|
| **Clip** | One captured clipboard entry. Either plain Unicode text or a bitmap image. Never both. |
| **History** | The ordered list of Clips, newest first, capped at the configured number of stored items. Kept across restarts. |
| **Capture** | Adding what is currently on the system clipboard to the History. Content the source app marks as private, oversized content, and ClipVault's own clipboard writes are never captured. |
| **Popup** | The menu listing the History, opened with the history hotkey. Rows are numbered; image rows show a thumbnail and "(BITMAP)". |
| **Hint** | The hover tooltip on a Popup row. For a text Clip it shows more of the text and appears only when the row could not show all of it. For an image Clip it shows a larger preview. Each kind can be switched off in Settings. |
| **Choose** | The user picking a Clip in the Popup. The Clip goes onto the system clipboard and becomes the Last Chosen Clip. |
| **Last Chosen Clip** | The Clip most recently chosen. Shown in bold in the Popup. At most one exists. The Popup always opens with the first (newest) row selected. |
| **Insert** | Pasting the clipboard into the Previous Window by sending Ctrl+V. Happens after every Choose unless Shift was held while choosing (then the Clip is only copied to the clipboard); always happens for a Template. |
| **Previous Window** | The application window that had focus when the Popup was opened. It keeps focus the whole time the Popup is open; the Popup never takes it. |
| **Template** | A named, predefined piece of text with a hotkey. Pressing the hotkey Inserts it and then restores what was on the clipboard before. Templates do not appear in the Popup and are never captured into the History. |
| **Hotkey** | A global key combination (modifiers plus a key) that works in every application. The history hotkey opens the Popup; a Template hotkey Inserts that Template. |
| **Evict** | Dropping the oldest Clip when the History exceeds its cap, or all Clips beyond a newly lowered cap. |
| **Duplicate** | A Capture whose content already exists in the History. The existing Clip moves to the top instead of a second Clip being added. |
