# 2. Clipboard access uses the raw Win32 API, not WPF's Clipboard class

Date: 2026-09-16

## Status

Accepted

## Context

WPF's `Clipboard` class (and WinForms') is built on OLE: `OleSetClipboard` publishes delayed-render placeholders for every format and only `OleFlushClipboard` renders them; `OleGetClipboard` hands other processes a COM proxy to the writer's data object.

Observed with Slack (Chromium): the first paste after ClipVault wrote the clipboard was instant, every later one froze Slack and ClipVault for about six seconds. A trace showed `Clipboard.SetDataObject` itself blocking for 6150 ms while Slack's UI thread waited on ClipVault. After its first paste Slack keeps a reference to our data object and reacts to the next clipboard change immediately, so the two processes end up waiting on each other inside our write until a timeout expires. Notepad, which uses plain Win32 calls, never showed the problem.

## Decision

`ClipboardIO` talks to the clipboard through `OpenClipboard` / `EmptyClipboard` / `SetClipboardData` / `GetClipboardData` with fully rendered `HGLOBAL` blocks, exactly like Notepad. Text is written as `CF_UNICODETEXT`; images as `CF_DIB` (32-bit BI_RGB, universally understood) plus the registered `PNG` format (keeps alpha). Reads prefer `CF_UNICODETEXT`, then `PNG`, then `CF_DIB`. The password-manager exclusion format is checked with `IsClipboardFormatAvailable`.

## Consequences

- No delayed rendering and no COM proxies: nothing another process does can call back into ClipVault, so a clipboard write can never block on someone else.
- Rich formats (HTML, RTF) are neither read nor written. This was already the product decision; the Win32 path just makes it explicit.
- Image round-trips go through BMP/PNG encoders instead of WPF's converters. `CF_DIB` blocks are decoded by rebuilding the BMP file header, which handles the common header variants; exotic DIBs are ignored rather than crashing.
- Going back to WPF's `Clipboard` would re-introduce the OLE behaviour; any future rich-format support should stay on the Win32 path and register the extra formats explicitly.
