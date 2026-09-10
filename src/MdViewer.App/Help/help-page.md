# Help page — keyboard map, Markdown dialect, and limits

This help page is bundled with the app and rendered by the same Markdown pipeline as every other document.

## Keyboard map

| Shortcut | Action |
| --- | --- |
| `F1` | Open this help page |
| `Ctrl+O` | Open file picker |
| `Ctrl+K` | Open folder picker |
| `Ctrl+P` | Quick open |
| `Ctrl+F` | Toggle find bar |
| `Ctrl+Home` | Show start page |
| `Ctrl+B` | Toggle sidebar |
| `Ctrl+E` | Toggle Preview / Raw source |
| `Ctrl+R` or `F5` | Reload current tab |
| `Ctrl+W` | Close current tab |
| `Ctrl+Shift+W` | Close all tabs |
| `Ctrl++` / `Ctrl+-` / `Ctrl+0` | Zoom in / out / reset |
| `Ctrl+Shift+F` | Toggle focus mode |
| `Ctrl+,` | Open settings |
| `Esc` | Dismiss overlays |

## Supported Markdown dialect (SPECIFICATION.md §5.1)

The app uses Markdig with an explicit extension set:

- CommonMark core
- Pipe tables and grid tables
- Task lists (`- [ ]`, `- [x]`)
- Emphasis extras: strikethrough, subscript/superscript, inserted text (`++ins++`), marked text (`==mark==`)
- Autolinks (`https://...` and `<https://...>`)
- Footnotes
- Definition lists
- Abbreviations
- YAML front matter (parsed as metadata)
- Custom containers (`::: note`, `::: warning`, etc.)
- Auto-identifiers on headings (`#anchor` links)
- Smart punctuation (setting-controlled)

Deliberately not enabled in v1: Bootstrap, Figures, JiraLinks, SelfPipeline, Globalization, and math.

## Deliberate limits

These boundaries are intentional:

1. **Raw HTML is escaped.**
   - Inline and block HTML are shown as text instead of being executed/rendered as HTML.
   - Example input:

     ```html
     <script>alert('no')</script>
     <div class="note">Visible as escaped text</div>
     ```

2. **Remote images are not fetched by default.**
   - Local relative images can load from disk.
   - Remote image URLs show placeholders until the document is explicitly allowed to fetch them.

   ```markdown
   ![Remote image example](https://example.com/image.png)
   ```

These defaults keep document viewing predictable, private, and safe.
