# Markdown Editor (Windows)

A simple, native Windows markdown editor. You edit a **rendered** view — headings
look like headings, bold looks bold — but the file on disk is always plain `.md`.
No server, no browser, no terminal. In the spirit of [Typora](https://typora.io/),
deliberately kept minimal.

See [`SPEC.md`](SPEC.md) for the full design and rationale.

## Stack

- **C# + WPF** on **.NET 8** (Windows-only by design)
- Editing surface: WPF `RichTextBox` over a native `FlowDocument`
- One dependency: [Markdig](https://github.com/xoofx/markdig) (used on the open path)

## Project layout

```
src/MdEditor/                  the app
  MainWindow.xaml(.cs)         shell: menu, toolbar, editor, file I/O, commands
  Markdown/
    MarkdownToFlowDocument.cs  open path:  markdown -> FlowDocument (via Markdig)
    FlowDocumentToMarkdown.cs  save path:  FlowDocument -> markdown (the core work)
tests/MdEditor.Tests/          round-trip tests over the markdown vocabulary
```

## Build & run

Requires the **.NET 8 SDK** (or newer with net8.0 targeting packs).

```bash
dotnet build MdEditor.slnx
dotnet run --project src/MdEditor/MdEditor.csproj
```

## Test

```bash
dotnet test tests/MdEditor.Tests/MdEditor.Tests.csproj
```

## Package (single self-contained .exe)

```bash
dotnet publish src/MdEditor/MdEditor.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true
```

Produces one `.exe` that bundles the .NET runtime — double-click and it runs on a
clean Windows 11 machine.

## Supported markdown

Headings (H1–H6), bold / italic / bold-italic, subscript & superscript,
ordered & unordered lists (nested), inline code and fenced code blocks, links,
inline images, blockquotes, horizontal rules, tables, paragraphs.
