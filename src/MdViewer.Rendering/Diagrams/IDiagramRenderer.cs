namespace MdViewer.Rendering.Diagrams;

/// <summary>
/// Pluggable renderer for fenced diagram languages.
/// </summary>
public interface IDiagramRenderer
{
    /// <summary>
    /// Fence languages this renderer handles, e.g. <c>mermaid</c>.
    /// </summary>
    IReadOnlyCollection<string> Languages { get; }

    /// <summary>
    /// Renders source text to an SVG payload.
    /// </summary>
    DiagramResult Render(string source, DiagramOptions options);
}
