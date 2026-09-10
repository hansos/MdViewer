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
        bool reducedMode = false)
    {
        Renderer = renderer;
        BaseDirectory = baseDirectory;
        SpanRegistrar = spanRegistrar;
        OnLinkActivated = onLinkActivated;
        ReducedMode = reducedMode;
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
    /// Set for documents over the size threshold: highlighting and diagram
    /// rendering are skipped (SPECIFICATION.md 5.10).
    /// </summary>
    public bool ReducedMode { get; }

    /// <summary>Nesting depth, so quotes and lists can style by level.</summary>
    public int Depth { get; private init; }

    public RenderContext Nested() => new(Renderer, BaseDirectory, SpanRegistrar, OnLinkActivated, ReducedMode)
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
