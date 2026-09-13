using Avalonia.Controls;

namespace MdViewer.Rendering;

/// <summary>
/// Everything a renderer needs that is not the block itself
/// (SPECIFICATION.md 5.2).
/// </summary>
public sealed class RenderContext
{
    public RenderContext(
        MarkdownRenderer renderer,
        string baseDirectory,
        ISourceSpanRegistrar spanRegistrar,
        Action<string> onLinkActivated,
        bool allowRemoteImages = false,
        Action? onLoadRemoteImagesRequested = null,
        bool reducedMode = false,
        string diagramTheme = "default",
        double diagramDpi = 96d,
        double diagramFontSize = 15d,
        string? diagramBodyFontFamily = null,
        string? diagramMonoFontFamily = null,
        IReadOnlyList<FindMatchOccurrence>? findMatches = null,
        int currentFindMatch = 0,
        bool allowTaskListEditing = false,
        Action<int, bool>? onTaskListToggled = null)
    {
        Renderer = renderer;
        BaseDirectory = baseDirectory;
        SpanRegistrar = spanRegistrar;
        OnLinkActivated = onLinkActivated;
        AllowRemoteImages = allowRemoteImages;
        OnLoadRemoteImagesRequested = onLoadRemoteImagesRequested;
        ReducedMode = reducedMode;
        DiagramTheme = string.IsNullOrWhiteSpace(diagramTheme) ? "default" : diagramTheme.Trim();
        DiagramDpi = diagramDpi <= 0 ? 96d : diagramDpi;
        DiagramFontSize = diagramFontSize <= 0 ? 15d : diagramFontSize;
        DiagramBodyFontFamily = string.IsNullOrWhiteSpace(diagramBodyFontFamily) ? null : diagramBodyFontFamily.Trim();
        DiagramMonoFontFamily = string.IsNullOrWhiteSpace(diagramMonoFontFamily) ? null : diagramMonoFontFamily.Trim();
        FindMatches = findMatches ?? Array.Empty<FindMatchOccurrence>();
        CurrentFindMatch = currentFindMatch;
        AllowTaskListEditing = allowTaskListEditing;
        OnTaskListToggled = onTaskListToggled;
    }

    /// <summary>Used by container blocks to render their children.</summary>
    public MarkdownRenderer Renderer { get; }

    /// <summary>Directory of the document, for resolving relative links and images.</summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Every renderer MUST register the source span of every control it creates.
    /// Search highlighting, outline scroll-to, offset-based scroll restoration
    /// and the v2 editor's scroll sync all depend on this.
    /// </summary>
    public ISourceSpanRegistrar SpanRegistrar { get; }

    /// <summary>Raised with the raw href; classification happens in the shell (5.6).</summary>
    public Action<string> OnLinkActivated { get; }

    /// <summary>
    /// True when this document has consent to fetch remote images in this
    /// session (SPECIFICATION.md 5.6).
    /// </summary>
    public bool AllowRemoteImages { get; }

    /// <summary>
    /// Raised by image placeholders to ask the shell to enable remote images
    /// for this document.
    /// </summary>
    public Action? OnLoadRemoteImagesRequested { get; }

    /// <summary>
    /// Set for documents over the size threshold: highlighting and diagram
    /// rendering are skipped (SPECIFICATION.md 5.10).
    /// </summary>
    public bool ReducedMode { get; }

    /// <summary>Theme key used by optional diagram renderers.</summary>
    public string DiagramTheme { get; }

    /// <summary>DPI value used by raster diagram renderers.</summary>
    public double DiagramDpi { get; }

    /// <summary>Base font size passed to diagram renderers.</summary>
    public double DiagramFontSize { get; }

    /// <summary>Body font family for diagram labels.</summary>
    public string? DiagramBodyFontFamily { get; }

    /// <summary>Monospace font family for diagram code-like text.</summary>
    public string? DiagramMonoFontFamily { get; }

    /// <summary>Find matches in source-offset space for the current document.</summary>
    public IReadOnlyList<FindMatchOccurrence> FindMatches { get; }

    /// <summary>The 1-based index of the active find match, or 0 when none.</summary>
    public int CurrentFindMatch { get; }

    /// <summary>
    /// True when task list checkboxes are interactive. Opt-in, because ticking
    /// one rewrites the marker in the file on disk (SPECIFICATION.md 5.2).
    /// </summary>
    public bool AllowTaskListEditing { get; }

    /// <summary>
    /// Raised with the source offset of the task list marker and its new state.
    /// The shell owns the edit; the renderer only reports the gesture.
    /// </summary>
    public Action<int, bool>? OnTaskListToggled { get; }

    /// <summary>Nesting depth, so quotes and lists can style by level.</summary>
    public int Depth { get; private init; }

    public RenderContext Nested() => new(
        Renderer,
        BaseDirectory,
        SpanRegistrar,
        OnLinkActivated,
        AllowRemoteImages,
        OnLoadRemoteImagesRequested,
        ReducedMode,
        DiagramTheme,
        DiagramDpi,
        DiagramFontSize,
        DiagramBodyFontFamily,
        DiagramMonoFontFamily,
        FindMatches,
        CurrentFindMatch,
        AllowTaskListEditing,
        OnTaskListToggled)
    {
        Depth = Depth + 1
    };
}

/// <summary>
/// Records the mapping between rendered controls and their source text ranges.
/// </summary>
public interface ISourceSpanRegistrar
{
    void Register(Control control, int sourceStart, int sourceLength);

    bool TryGetSpan(Control control, out int sourceStart, out int sourceLength);

    Control? FindControlContaining(int sourceOffset);
}
