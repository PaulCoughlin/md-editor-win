# Fast native markdown editor (Tauri + TipTap) — design

A second markdown editor with the **same functionality as the WPF app** — live
WYSIWYG editing of a *rendered* markdown document, saved as native `.md`, no preview
pane — but **faster and smoother**. This fills a gap the user's other tools don't:
EditPlus edits raw markdown, the Python app covers cross-platform, the WPF app is the
current native rendered editor but feels sluggish (jerky window move, not-instant
launch). This targets the smooth/fast native niche.

## Goals

- Feature parity with the WPF app: headings H1–H6, bold/italic/bold-italic,
  subscript/superscript, ordered & unordered lists, inline & fenced code, links,
  inline images, blockquotes, horizontal rules, paragraphs, tables + table editing.
- Live WYSIWYG rendered document (a table is a real grid, bold *is* bold, no `**`
  ever shown). No source/preview pane.
- Native `.md` on disk. Font is a display preference only, never written to the file.
- Preferences (font family, size, spellcheck language), window bounds, print.
- **Smooth and fast** — this is the acceptance criterion, judged against the WPF app
  and eyeballed against EditPlus.

## Non-goals (first version)

- Multiple documents / tabs — single document, matching WPF. (Noted as a likely
  follow-up given the user's interest in EditPlus's multi-doc feel.)
- Cross-platform packaging — Windows-first (Tauri is cross-platform, but not a goal).
- Plugins, cloud, collaboration.

## Stack

- **Shell:** Tauri (Rust backend + WebView2 frontend). Small binary, fast cold-start,
  native window/menu/dialogs, uses the already-installed Edge WebView2.
- **Editor:** TipTap (on ProseMirror) — a true rendered-document WYSIWYG editor, the
  closest analogue to WPF's FlowDocument/RichTextBox. Markdown mode + table extension.
- **Frontend build:** Vite + vanilla TypeScript (no UI framework — leanest for a
  single-window editor).
- Requires installing the Rust toolchain (not currently present).

## Architecture

```
Tauri shell (Rust): window, native menu, file dialogs,
  read_file / write_file / set_title commands, window-bounds persistence
  └─ WebView2: Vite + TypeScript
       TipTap editor (rendered document) + markdown ⇄ doc + toolbar +
       context menu + dialogs + preferences
```

Rust owns OS concerns (the role WPF's code-behind played for window/disk/menu);
TipTap owns the rendered document (the role of RichTextBox/FlowDocument). The
markdown⇄document translation — the heart of the WPF app — is mostly provided by
TipTap's markdown support, so far less serializer code is hand-written.

## Components

| Unit | Responsibility | WPF analogue |
|---|---|---|
| `src-tauri/main.rs` + commands | Window, menu, file open/save dialogs, read/write/title, bounds | `MainWindow` OS parts |
| Rust menu + accelerators | File menu (New/Open/Save/Save As/Print) + Ctrl shortcuts → events | `Menu` + `CommandBindings` |
| `editor.ts` | TipTap instance: which extensions (nodes/marks) = the fixed vocabulary | `RichTextBox` config |
| `markdown.ts` | Document ⇄ markdown; config for sub/sup and images where needed | `MarkdownToFlowDocument` + `FlowDocumentToMarkdown` |
| `commands.ts` | Toolbar + shortcut actions (bold, H1–H6, lists, quote, rule, link, table) | formatting handlers |
| `contextmenu.ts` | Right-click: native spelling, clipboard, table ops in a table | `Editor_ContextMenuOpening` |
| `state.ts` | Dirty tracking, current-path, save-before-discard, title | dirty-state logic |
| `preferences.ts` | Font/size/spellcheck + window bounds, persisted to app-config JSON | `Settings`/`SettingsStore`/`PreferencesWindow` |

Boundaries match the WPF app: OS in Rust, document in TS, translation isolated.

## Feature mapping

- Headings, bold, italic, lists, code, links, images, blockquotes, rules → built-in
  TipTap nodes/marks.
- Subscript/superscript → TipTap `Subscript`/`Superscript` marks.
- Tables + row/column/header editing → TipTap `Table` extension (insert/delete row &
  column, toggle header) — upstream equivalent of our `TableOperations`.
- Spellcheck (en-GB) → WebView native: `spellcheck="true"` + `lang="en-GB"`;
  right-click spelling suggestions come from the OS/WebView for free.
- Print → `window.print()` → native print dialog renders the document.
- Preferences → persisted JSON in the Tauri app-config dir (same idea as
  `%APPDATA%\MdEditor\settings.json`). Font/size applied to the editor surface only.

Wins over WPF: spelling suggestions, print, and most of the serializer come from the
platform. Risk: TipTap's emitted markdown must match our vocabulary/round-trip —
locked by ported tests.

## Data flow

- **Open:** menu/Ctrl+O → Rust file dialog → `read_file` → TS parses markdown → TipTap
  document. Set current path, mark clean, update title.
- **Save:** Ctrl+S → TS serializes TipTap doc → markdown → Rust `write_file`. Save As
  prompts for a path. Mark clean.
- **Dirty/close:** edits set dirty (title `*`); New/Open/close prompt to save when
  dirty. Fresh document is clean (the bug we fixed in WPF — designed in from the start).

## Testing & verification

- Round-trip tests (Vitest): markdown → TipTap doc → markdown, porting the 12 WPF
  cases (headings, bold/italic, inline code, lists, blockquote, rule, link, code
  block, table, empty doc). Locks fidelity.
- Table-ops tests: build → edit → serialize → assert.
- Runtime verification: launch the real app; measure launch time and typing/scroll
  smoothness against the WPF app and eyeball vs EditPlus (smooth is the whole point);
  insert/edit a table; save and reopen a file for round-trip fidelity.

## Honest risks

1. Rust first-compile is slow (minutes); incremental fine after.
2. "Smooth" is the acceptance criterion, not a feature — measured against WPF/EditPlus.
   If it doesn't clearly beat WPF, that is a finding to surface, not hide.
3. TipTap's default markdown may differ from our exact output (`*` vs `_`, list
   markers); tests pin it, may need small serializer tweaks.
4. Full parity is a larger first build than the WPF MVP — a fresh app, not an edit.

## Layout

```
tauri-editor/            (new subfolder in the repo)
  src/                   frontend TS (editor.ts, markdown.ts, commands.ts, …)
  src-tauri/             Rust shell (main.rs, commands, menu)
  tests/                 Vitest round-trip + table tests
  index.html, package.json, vite.config.ts
```

Kept in a separate top-level subfolder so it stands alongside the WPF app (`src/`,
`tests/`) without entangling the two builds.
