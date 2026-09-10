# Diagram Rendering — Implementation Plan

**Status:** proposed
**Supersedes:** SPECIFICATION.md §5.5 (see §9 of this document for the exact spec amendments)
**Target milestone:** M3, alongside code blocks
**Date:** 2026-09-10

---

## 1. Why this replaces §5.5

SPECIFICATION.md §5.5 opens with a claim that is no longer true:

> Mermaid rendering has no mature native .NET implementation.

It then builds an entire subsystem around that claim — a pluggable *out-of-process*
renderer shelling out to `mmdc`, an availability probe, a 20-second timeout, process
sandboxing, a 200 MB LRU disk cache keyed on `hash(source + theme + dpi)`, and a
settings page for locating the executable. Every one of those exists to survive the
cost and unreliability of launching Node and Chromium from a native desktop app.

That cost is avoidable. **Mermaider** parses Mermaid's DSL and runs its own Sugiyama
layout entirely in managed C#, emitting sanitized SVG. No browser, no DOM, no JS
runtime, no child process.

| | §5.5 as written (`mmdc`) | This plan (Mermaider) |
|---|---|---|
| Runtime dependency | Node + Puppeteer + Chromium (~300 MB), user-installed | None — a NuGet reference |
| Render latency | 2–10 s process launch | 12–71 µs in-process |
| Works out of the box | No | Yes |
| Render call | Async, cancellable, placeholder UI | Synchronous, inside `IBlockRenderer.Render` |
| Theme change | Re-render via external process | Re-render at ~25 µs |
| Cache | 200 MB on-disk LRU, hashed key | Small in-memory dictionary, or none |
| Attack surface | Untrusted process output, timeouts, sandbox flags | Always-on SVG allowlist sanitizer |
| Platform parity | Chromium-on-Linux is the weak spot | Identical managed code both platforms |

The disk cache, the timeout, the sandbox, the availability probe, the settings page and
the async placeholder state all disappear. What remains is roughly one afternoon of
plumbing plus a theming pass.

### 1.1 The dependencies

| Package | Version | Role | License |
|---|---|---|---|
| `Mermaider` | 0.12.2 (2026-08-11) | Mermaid DSL → SVG, pure managed, `net10.0`, Native-AOT clean | MIT |
| `Svg.Controls.Skia.Avalonia` | 12.0.0.13 (2026-06-15) | SVG → Avalonia visual | MIT |

`Mermaider` pulls `Sugiyama` and `Microsoft.Extensions.ObjectPool`.
`Svg.Controls.Skia.Avalonia` pulls `Avalonia.Skia ≥ 12.0.0` and `Svg.Skia ≥ 5.1.1`.
The project is on Avalonia 12.1.2, which satisfies the floor.

`Svg.Controls.Skia.Avalonia` is the renamed `Avalonia.Svg.Skia` from the same
maintainer. **Its existence closes Open Question #1** (§2.2, §10): there *is* an
Avalonia 12-compatible SVG path, so the PNG-rasterization fallback in §2.2 option 2
is no longer needed and should be struck.

### 1.2 What Mermaider covers

24 diagram types. Flowchart, Sequence, State, Class, ER, Pie, Quadrant, Timeline,
GitGraph, Gantt, Journey, C4, Kanban, TreeView and Block are stable; Radar, Treemap,
Venn, Mindmap, Sankey, XY Chart, Requirement, Packet and Architecture are marked beta.

### 1.3 The risk, stated plainly

Mermaider is at 0.12.2 with roughly 23 GitHub stars and ~29k downloads. It is young,
and it is a reimplementation, so it will diverge from mermaid.js on edge-case syntax.
Three things contain that risk:

1. **Every failure degrades to a code block.** A diagram that will not parse is still
   readable source. Nothing about the document breaks.
2. **`IDiagramRenderer` stays.** The spec's seam is worth keeping even though we ship
   one engine. A second engine — including the original `mmdc` renderer, for users
   who want byte-exact mermaid.js fidelity — slots in behind it without touching the
   block registry. That is what the interface was for.
3. **It is MIT and it is C#.** If upstream stalls, the source can be vendored into
   `src/MdViewer.Rendering/Diagrams/` and maintained in-tree.

---

## 2. Architecture

```
src/MdViewer.Rendering/
  Diagrams/
    IDiagramRenderer.cs          # the seam from SPECIFICATION.md 5.5, minus the async
    DiagramRendererRegistry.cs   # language string -> renderer
    DiagramResult.cs             # Ok(svg) | Unsupported | Failed(message)
    MermaidDiagramRenderer.cs    # Mermaider adapter
    DiagramPalette.cs            # theme token set -> Mermaider RenderOptions
    SvgThemeFlattener.cs         # resolves var(--x) and rem, see 4.3
    DiagramView.cs               # the Control: theme- and zoom-reactive, owns the SVG
  Blocks/
    DiagramBlockRenderer.cs      # IBlockRenderer, sits ahead of CodeBlockRenderer
```

This lands exactly where SPECIFICATION.md §3 already reserved space
(`MdViewer.Rendering/Diagrams/`). `MdViewer.Core` is untouched — it stays
Avalonia-free, and diagram rendering is a rendering concern.

### 2.1 The synchronous insight

`IBlockRenderer.Render` returns a `Control` on the UI thread. §5.5's async signature
existed only because a process launch cannot block a frame:

```csharp
Task<DiagramResult> RenderAsync(string source, DiagramOptions options, CancellationToken ct);
```

At 12–71 µs, that machinery buys nothing. A 16 ms frame budget absorbs roughly 200
diagram renders. The interface becomes:

```csharp
public interface IDiagramRenderer
{
    /// <summary>Fence info strings this renderer claims: "mermaid", "dot", ...</summary>
    IReadOnlyCollection<string> Languages { get; }

    DiagramResult Render(string source, DiagramOptions options);
}
```

An engine that genuinely *is* slow — a future `mmdc` renderer — can expose that through
`DiagramOptions`/a capability flag and be driven from a background task by
`DiagramView`, which already owns the swap-in path for its own re-render. The
interface does not need to be async to accommodate it; only that one implementation
does.

### 2.2 Where it hooks in

A ` ```mermaid ` fence *is* a `FencedCodeBlock`. `CodeBlockRenderer.CanRender` already
matches every `CodeBlock`, and `BlockRendererRegistry` documents registration order as
priority order — this is precisely the case the comment anticipates:

> Registration order is priority order, so a more specific renderer added later —
> diagrams, custom containers — can be inserted ahead of a general one without editing
> anything that already works.

So the only change to existing code is one line:

```csharp
public static BlockRendererRegistry CreateDefault() => new BlockRendererRegistry()
    .Add(new HeadingBlockRenderer())
    .Add(new ImageBlockRenderer())
    .Add(new ParagraphBlockRenderer())
    .Add(new DiagramBlockRenderer())   // <- ahead of CodeBlockRenderer, by design
    .Add(new CodeBlockRenderer())
    .Add(new TableBlockRenderer())
    // ...
```

with

```csharp
public bool CanRender(Block block) =>
    block is FencedCodeBlock fenced
    && DiagramRendererRegistry.Default.Supports(fenced.Info);
```

---

## 3. Theming, without breaking the token discipline

The README's two invariants are load-bearing here:

> **Renderers apply classes, not colours.**

A diagram is a picture, so it cannot carry style classes — the same problem `Themed`
already solves for inlines, and it gets the same solution. Mermaider takes its palette
as explicit `RenderOptions` values (`Bg`, `Fg`, `Accent`, `Muted`, `Surface`, `Border`,
`Line`, `Font`, `MonoFont`, `FontSize`, `DataPalette`), so the diagram's colours come
from the token dictionary and nowhere else.

The trick that makes this reactive: `DiagramView` declares one `StyledProperty` per
token and binds each to a `DynamicResource`, reusing the existing `Themed.Apply`
helper.

```csharp
public sealed class DiagramView : Decorator
{
    public static readonly StyledProperty<IBrush?> ForegroundTokenProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(ForegroundToken));
    // ... SurfaceToken, BorderToken, LineToken, AccentToken, MutedToken, BackgroundToken

    public static readonly StyledProperty<double> FontSizeTokenProperty =
        AvaloniaProperty.Register<DiagramView, double>(nameof(FontSizeToken), 15d);

    public DiagramView()
    {
        this.Apply(ForegroundTokenProperty, "TextPrimaryBrush");
        this.Apply(BackgroundTokenProperty, "ContentBackgroundBrush");
        this.Apply(SurfaceTokenProperty,    "DiagramSurfaceBrush");
        this.Apply(BorderTokenProperty,     "DiagramBorderBrush");
        this.Apply(LineTokenProperty,       "DiagramLineBrush");
        this.Apply(AccentTokenProperty,     "AccentBrush");
        this.Apply(MutedTokenProperty,      "TextMutedBrush");
        this.Apply(FontSizeTokenProperty,   "BodyFontSize");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (TokenProperties.Contains(change.Property)) InvalidateDiagram();
    }
}
```

Two things fall out of this for free, and both are worth naming because they are
requirements elsewhere in the spec:

- **Theme switch (§5.11, §6.1: < 200 ms, no re-parse).** Changing the theme variant
  swaps the brushes, which fires `OnPropertyChanged`, which re-renders the SVG at
  ~25 µs per diagram. No re-parse, no document rebuild.
- **Zoom (§5.11, per-tab).** `ZoomTypography.Apply` writes scaled metrics into
  `presenter.Resources`. `BodyFontSize` is one of them, and a `DynamicResource` lookup
  walks up the tree — so a zoom change re-renders diagram *text* at the new size
  rather than scaling a bitmap. Diagram type sizes track body text, which is the
  correct behaviour and something the PNG plan could not have delivered.

`InvalidateDiagram` should coalesce: a theme switch changes seven brushes at once, and
each one must not trigger its own render. Post a single re-render to the dispatcher at
`DispatcherPriority.Render` and drop duplicates.

### 3.1 New tokens

Add to both `Light` and `Dark` in `Themes/Tokens.axaml`:

| Key | Light | Dark | Purpose |
|---|---|---|---|
| `DiagramSurfaceBrush` | `#F7F8FA` | `#22262D` | Node fills |
| `DiagramBorderBrush` | `#D5DAE1` | `#3A414B` | Node strokes |
| `DiagramLineBrush` | `#8B939E` | `#767F8B` | Edges, arrowheads |

Reusing `CodeBlockBackgroundBrush` would work today and hurt later — diagram surfaces
and code surfaces want to diverge (a diagram node needs more contrast against the page
than a code block does). Three keys is cheap insurance.

**The categorical palette** (pie, timeline, gitgraph, radar, sankey, treemap, journey)
is 12 colours, and 12 more brush keys per theme is a poor trade. Two options:

- `DiagramDataPalette` as a single string resource of comma-separated hex values, parsed
  once by `DiagramPalette`. Keeps everything in the token file; slightly off-idiom.
- A `DiagramPalette.ForVariant(ThemeVariant)` static holding two `string[]`.
  Idiomatic C#, but puts twelve colours outside the token dictionary.

**Recommendation: the string resource.** The token file staying the single place
colours live is worth more than the mild inelegance, and the spec is explicit that
"adding a theme means adding one dictionary, not touching renderers."

---

## 4. Rendering path

### 4.1 Source → SVG

```csharp
var options = DiagramPalette.ToRenderOptions(tokens, fontSize, fontFamily, monoFamily);
var svg = MermaidRenderer.RenderSvg(source, options);
```

`RenderOptions` settings that matter for us:

- `Transparent = true` — the page background shows through, so the diagram sits on the
  content surface in both themes without a seam.
- `SanitizeMode.Strip` (the default) with the `OnStripped` hook wired to the logger.
  `Block` would throw and degrade the whole diagram over one bad attribute; stripping
  and logging is the better failure mode for a *viewer*.
- `AllowedDiagrams` — leave at `All` initially. It exists if a beta diagram type turns
  out to render badly and needs disabling without a release.
- `Strict` — not used. That feature enforces a design system on authored diagrams; we
  are displaying other people's documents, not policing them.

### 4.2 SVG → Avalonia

```csharp
var source = SvgSource.LoadFromSvg(flattenedSvg);   // API name to confirm in Phase 0
Child = new Image
{
    Source = new SvgImage { Source = source },
    Stretch = Stretch.Uniform,
    StretchDirection = StretchDirection.DownOnly,
    MaxWidth = intrinsicWidth,
};
```

The sizing rule mirrors `MarkdownImages.BuildImage` exactly — scale down to the content
column, never blow up past natural size — so a diagram and an image behave the same way
in a document. That consistency is free and worth having.

### 4.3 The one real integration risk: `var()` and `rem`

Mermaider embeds its palette as CSS custom properties and sizes text in `rem`:

```css
:root { --bg: #1E1E2E; --fg: #CDD6F4; --fs-xs: 0.75rem; --fs-m: 1rem; }
```

That design is aimed at a browser, where live theme switching means poking `:root`.
Svg.Skia's CSS support is partial, and `var()` resolution and `rem` resolution are both
likely to be missing. If they are, text renders black-on-black or at the wrong size —
and it will look like *our* bug.

The fix is small, deterministic and entirely under our control: **`SvgThemeFlattener`**,
a pre-pass that runs between Mermaider and Svg.Skia.

1. Read the `:root { ... }` declaration block out of the emitted `<style>`.
2. Replace every `var(--name)` and `var(--name, fallback)` with the resolved literal,
   resolving transitively and falling back to the declared fallback when a name is
   missing.
3. Convert `<n>rem` to `<n × baseFontSize>px`, using the same base font size passed into
   `RenderOptions`.
4. Assert in a unit test that the output contains neither `var(` nor `rem`.

Roughly 40 lines and a handful of tests. Because we re-render on theme change anyway
(§3), we lose nothing by flattening — the live-`:root` switching Mermaider offers is a
browser optimisation we have no use for.

**This is the gate for Phase 0.** If Svg.Skia turns out to handle `var()` natively, the
flattener is deleted and nothing else changes. If Svg.Skia mishandles something larger —
text metrics, `text-anchor`, dominant-baseline — the fallback is a hand-written
SVG→Avalonia `Geometry` mapper over the subset Mermaider's sanitizer guarantees
(`path`, `rect`, `line`, `polygon`, `polyline`, `circle`, `ellipse`, `text`, `g`
+ transforms). That is roughly 700 lines and is SPECIFICATION.md §2.2 option 3 — far
cheaper against Mermaider's small, sanitized, `foreignObject`-free output than it would
have been against real mermaid.js SVG.

### 4.4 Caching

An in-memory `Dictionary` on `DiagramView`'s owning presenter, keyed on
`(sourceHash, themeVariant, fontSize)`, capped at 64 entries. It exists for M7
virtualization — §6.2 requires that recycled blocks re-realize cheaply — not for
first render, which is already faster than the allocation.

**The §5.5 disk cache is deleted.** A 200 MB on-disk LRU to avoid a 25 µs computation is
not a trade anyone should make; the cache lookup would cost more than the render.

---

## 5. Chrome, failure and degradation

### 5.1 The diagram frame

Reuse the code-block chrome — same `md-code-block` border, same header grid:

- Left: the language label (`mermaid`), styled `md-code-lang`, same as a code block.
- Right: **Copy** (copies the diagram source, matching `CodeBlockRenderer`'s
  "raw source, never the rendered text" rule) and **Source**, a toggle that swaps the
  rendered diagram for the fenced code block.

The Source toggle is not decoration. It is the honest answer to three separate
requirements at once: a fidelity escape hatch when Mermaider renders something oddly,
the accessibility fallback (§5.2 below), and consistency with the app's stated
principle that the source text is the source of truth.

### 5.2 Accessibility (§6.3)

A picture cannot expose document structure to a screen reader. Set
`AutomationProperties.Name` to `"{DiagramType} diagram"` and keep the **Source** toggle
keyboard-reachable in the tab order, so the diagram's actual content — its source — is
always one keystroke away. Note this honestly in the spec rather than claiming diagram
accessibility we do not have.

### 5.3 Failure ladder

Every rung stays readable. No rung throws.

| Condition | Result |
|---|---|
| Renders | The diagram, with chrome |
| Parse failure / unsupported syntax | Code block + `md-caption` line: *"Diagram could not be rendered: {message}"* |
| Unknown fence language | `CanRender` returns false; `CodeBlockRenderer` handles it, unchanged |
| Source over 100 KB | Code block + *"Diagram too large to render."* |
| Reduced mode (doc > 5 MB, §5.10) | Code block + a *"Render diagrams"* button |
| Renderer throws | `MarkdownRenderer.RenderBlock`'s existing guard produces the error card (§6.4) |

`ReducedMode` is already on `RenderContext`, and the consent button reuses the
`OnLoadRemoteImagesRequested` pattern from `MarkdownImages` — a per-document opt-in
raised to the shell. §5.10 asks for "a bar offers to enable them"; the same signal
serves both.

### 5.4 Guardrails

- 100 KB source cap (above, mostly to bound pathological input).
- `Stopwatch` around the render; log anything over 50 ms so a bad case surfaces in
  diagnostics rather than as a mystery stutter.
- Mermaider's sanitizer is always on and blocks `<script>`, `<foreignObject>`, event
  handlers and `http(s):` URIs, permitting only base64 `data:image/png` and
  `data:image/svg+xml` on `<image>`. Those are local bytes, never a fetch — so
  §6.5's "remote images are not fetched without consent" holds without a special case.

---

## 6. Phases

**Phase 0 — Spike (half a day). This is the gate.**
Headless console app: Mermaider → `SvgThemeFlattener` → `SvgSource` → render a
flowchart, a sequence diagram, a class diagram and a pie chart to PNG; eyeball them.
Confirm the `SvgSource` / `SvgImage` API surface, confirm whether `var()` and `rem`
need flattening, confirm text metrics and `text-anchor` survive. Everything below
assumes this passes. If it does not, the decision point is §4.3's own-mapper fallback,
made with evidence rather than in advance.

**Phase 1 — Engine.**
`IDiagramRenderer`, `DiagramRendererRegistry`, `DiagramResult`,
`MermaidDiagramRenderer`, `SvgThemeFlattener`, `DiagramPalette`. All unit-testable
without a UI thread. Versions into `Directory.Build.props`
(`MermaiderVersion`, `SvgControlsSkiaAvaloniaVersion`), package references into
`MdViewer.Rendering.csproj`.

**Phase 2 — Control and wiring.**
`DiagramView` with the token bindings and coalesced invalidation; three new tokens plus
the palette resource in `Tokens.axaml` for both variants; `DiagramBlockRenderer`;
one line in `BlockRendererRegistry.CreateDefault()`. Source span registration on the
outer frame, per `RenderContext.SpanRegistrar`'s contract. First diagrams on screen.

**Phase 3 — Chrome and degradation.**
Header, copy, Source toggle, the failure ladder, reduced-mode consent,
`AutomationProperties`, the 100 KB cap.

**Phase 4 — Hardening.**
The 64-entry cache, the render-time budget log, tests (§7), mermaid samples appended to
`test-data/rendering-test.md`, README table update, spec amendments (§9).

Phases 1–3 are the working feature. Phase 4 is what makes it shippable.

---

## 7. Testing

There is **no `tests/` directory in the repository yet**, though SPECIFICATION.md §3 and
§8 both assume one. This work is a reasonable excuse to create
`tests/MdViewer.Rendering.Tests`, but that is a real decision with its own cost — worth
naming rather than smuggling in.

| Test | Kind |
|---|---|
| `SvgThemeFlattener` resolves `var()`, nested `var()`, fallbacks, and `rem` | Unit |
| Flattened output contains no `var(` and no `rem` | Unit |
| `DiagramRendererRegistry` claims `mermaid` and declines `csharp`, `python`, `""` | Unit |
| Malformed diagram source returns `Failed`, never throws | Unit |
| `DiagramPalette` maps every token to a `RenderOptions` field | Unit |
| One golden SVG per stable diagram type | Snapshot |
| `DiagramBlockRenderer` registers a source span | Headless Avalonia |
| Theme variant change re-renders exactly once for seven brush changes | Headless Avalonia |
| 1 MB document containing 50 diagrams still meets the §6.1 open target | Benchmark |

Golden SVG snapshots are worth the maintenance here specifically because Mermaider is
young: they turn an upstream fidelity regression into a failing test instead of a bug
report.

---

## 8. What is explicitly not in scope

- **PlantUML and Graphviz.** They stay in §9.2 as v2 items. `IDiagramRenderer` is the
  reason adding them later is cheap; building them now is not the same thing as
  designing for them.
- **An `mmdc` renderer.** Kept in the design's back pocket for exact mermaid.js fidelity,
  built only if real documents show Mermaider diverging in ways that matter.
- **Remote rendering services** (Kroki, mermaid.ink). Nothing needs them now, and
  §6.5's "document content never leaves the machine" is worth more than the diagrams
  they would add.
- **Interactive diagrams** — click-through, pan, zoom-into-diagram. Mermaider's output
  is static geometry by design. Reconsider if anyone asks.
- **Math / LaTeX.** Stays in §9.2. It is a genuinely different problem and shares none
  of this machinery.

---

## 9. Required SPECIFICATION.md amendments

These sections currently assert things this plan makes false. They should be corrected
in one pass.

**§2.2 — Dependencies table.** Add `Mermaider` 0.12.2 (Mermaid → SVG, pure managed) and
`Svg.Controls.Skia.Avalonia` 12.0.0.13 (SVG display).

**§2.2 — "Open dependency risk — SVG rendering".** Delete or rewrite. The premise
("`Avalonia.Svg.Skia` has no Avalonia 12–compatible release") is stale: the package was
renamed to `Svg.Controls.Skia.Avalonia` and shipped a 12.x line in April 2026. The
three mitigations and the "v1 ships with option 2" decision all fall away — though
option 3 survives as the documented fallback in §4.3 of this plan.

**§5.5 — Diagrams.** Replace wholesale. Everything from "no mature native .NET
implementation" onward describes a subsystem we are not building: the async
`IDiagramRenderer` signature, `mmdc`, `IsAvailable`, the settings-configured path, the
20-second timeout, the process sandboxing, and the 200 MB disk cache. The graceful-
degradation principle is the one part that survives intact and should be kept verbatim —
it is still the rule, it simply now covers parse failures rather than a missing
executable.

**§6.5 — Security posture, point 3.** *"External diagram servers are opt-in and
disclosed"* is now vacuous; nothing external is contacted. Either strike it or reword it
to record that no diagram rendering leaves the machine at all, which is a stronger
claim and worth stating.

**§9.1 — Roadmap.** Move diagrams from M8 to M3, and update M3's exit criterion. Delete
the note beneath the table: *"Diagrams (§5.5) sit in M8 deliberately: they depend on an
external tool and an unresolved package question, and nothing else depends on them."*
Both reasons are gone. The third clause still holds and is now the only argument for
scheduling flexibility.

**§10 — Open Questions, #1.** Strike. *"Does an Avalonia 12–compatible
`Avalonia.Svg.Skia` land before M8, or do we ship PNG rasterization?"* — answered:
it landed, under a new name, and PNG rasterization is not needed.

**Appendix A.** Add both packages with their verified dates. Correct the
`Avalonia.Svg.Skia — 11.3.0, no Avalonia 12 build yet` line, which is the source of the
stale assumption in §2.2.

**README.md.** The "still deliberately absent" table lists *"Images, diagrams — M6 / M8"*.
Update the diagram half to M3.

---

## 10. Summary

One NuGet reference, one line in `BlockRendererRegistry`, one new control, three new
theme tokens and a small SVG pre-pass. No external process, no bundled runtime, no disk
cache, no async placeholder machinery, and no WebView anywhere near it — which is the
whole point of §2.1's decision, and the reason the `mmdc` plan sat badly in this
application in the first place.

The single unknown worth spending time on before committing is whether Svg.Skia renders
Mermaider's SVG faithfully. That is Phase 0, it costs half a day, and it has a known
fallback.
