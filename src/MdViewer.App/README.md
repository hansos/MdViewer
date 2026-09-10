# KT MD Viewer

KT MD Viewer is a cross-platform Markdown viewer for Windows and Linux, built on Avalonia UI.

See [docs/SPECIFICATION.md](docs/SPECIFICATION.md) for the full specification.

---

## What this build is

**M2: the viewer renders real documents.** Files are read, encoding-detected and
parsed with Markdig off the UI thread, then turned into a native Avalonia visual
tree by `MdViewer.Rendering`. Nothing on screen is mock data any more.

What works:

- Open files (`Ctrl+O`), open a workspace folder, or pass paths on the command line
- Real rendering: headings, paragraphs, emphasis, inline code, links, lists,
  task lists, block quotes, fenced and indented code blocks, tables, thematic
  breaks, footnote and definition-list content
- Outline built from the document's own headings; clicking one scrolls to it
- Anchor links (`#heading`) resolve and scroll; `http(s)` and `mailto` open in
  the OS handler; relative `.md` links open in a new tab
- Encoding detection (BOM, UTF-8 scan, system fallback) and line-ending
  detection, both shown in the status bar
- Workspace tree with lazy directory loading; single click previews, double
  click opens permanently
- Quick-open (`Ctrl+P`) over a real workspace index, with recent documents first
- Recent documents on the start view, populated by what you actually open
- **Preview / raw toggle** (`Ctrl+E`, or the segmented control in the status bar).
  Preview is the default; raw is read-only source with a fixed line-number
  gutter. The reading position carries across the switch, because both views
  position by source offset.

What is still deliberately absent, by milestone:

| Missing                                                  | Arrives in         |
| -------------------------------------------------------- | ------------------ |
| Syntax highlighting in code blocks (they render plain)   | M3 — TextMateSharp |
| GitHub alerts and `:::` containers as callouts           | M3                 |
| Full table column algorithm and sticky headers           | M3                 |
| Find actually searching (the bar is still a placeholder) | M5                 |
| Cross-block selection, copy-as-source                    | M5                 |
| File watching, session restore, persisted recent list    | M6                 |
| Virtualization — every block is realised eagerly         | M7                 |
| Images, diagrams                                         | M6 / M8            |
| Live zoom (the control updates the indicator only)       | M7                 |

Raw HTML is escaped and shown dimmed rather than rendered, and remote images are
not fetched. Both are deliberate — see SPECIFICATION.md 5.6 and 6.5.

See [Issues · hansos/MdViewer](https://github.com/hansos/MdViewer/issues) for full road map and issue list.

## Requirements

- Visual Studio 2026 with the **.NET desktop development** workload
- .NET 10 SDK

## Getting started

```
dotnet restore
dotnet run --project src/MdViewer.App
```

Or open `MdViewer.slnx` in Visual Studio 2026 and press F5. Visual Studio 2026
opens `.slnx` solutions natively; there is no `.sln` file.

You can also pass files on the command line — `MdViewer README.md` opens it and
makes its folder the workspace.

## Try this

| Action                             | What to look at                                                                 |
| ---------------------------------- | ------------------------------------------------------------------------------- |
| Open `docs/SPECIFICATION.md`       | A long real document: heading hierarchy, tables, code blocks, the outline panel |
| Click a heading in the outline     | Scrolls by source offset, not by pixel                                          |
| Click an internal `#anchor` link   | Same mechanism, resolved through the slug table                                 |
| `Ctrl+P`                           | Recent first, then fuzzy workspace matches                                      |
| `Ctrl+E`                           | Preview ⇄ raw source — note it keeps your place in a long document              |
| `Ctrl+Shift+W`                     | Closes every tab and shows the start view                                       |
| Theme button, top right            | System → Light → Dark, with no re-parse and no re-render                        |
| Single vs double click in the tree | Preview tab (italic) vs permanent tab                                           |

## Project structure

```
src/
  MdViewer.Core/        No UI reference. Parsing, loading, outline, encoding,
                        workspace scanning, fuzzy matching — all unit-testable
                        without a UI thread.
  MdViewer.Rendering/   Markdig AST → Avalonia visual tree. Applies style
                        classes, never literal colours.
  MdViewer.App/         Views, view models, composition root.
docs/
  SPECIFICATION.md      The full specification.
```

Two invariants hold the design together:

**Source text is the single source of truth.** Every rendered control registers
its source span with `SourceSpanRegistry`. Scroll positions, the outline, anchor
navigation and — from M5 — selection and find all work in source-offset space.
That is what makes the v2 split-pane editor an addition rather than a rewrite.

**Renderers apply classes, not colours.** Themes are a token dictionary in
`MdViewer.App/Themes/Tokens.axaml`. The one exception is inline content, which
has no class support: those bind to theme tokens dynamically via `Themed`, so a
theme switch still repaints without re-parsing.

## Housekeeping

`src/MdViewer.App/SampleData.cs` is superseded and can be deleted — everything it
supplied now comes from real files. `Views/SampleDocumentView.axaml` is no longer
shown; it is kept as the hand-authored visual reference the block renderers are
checked against.
