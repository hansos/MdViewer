namespace MdViewer.Rendering.Diagrams;

/// <summary>
/// Renders Mermaid diagrams to SVG in-process using the managed Mermaider engine.
/// No external CLI, Node or Chromium is required (docs/DIAGRAMS-PLAN.md).
/// </summary>
public sealed class MermaidInProcessDiagramRenderer : IDiagramRenderer
{
    public IReadOnlyCollection<string> Languages { get; } = ["mermaid"];

    public DiagramResult Render(string source, DiagramOptions options)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return DiagramResult.Failed("Empty diagram source.");
        }

        try
        {
            var renderOptions = DiagramPalette.CreateRenderOptions(options);
            var svg = Mermaider.MermaidRenderer.RenderSvg(source, renderOptions);

            return string.IsNullOrWhiteSpace(svg)
                ? DiagramResult.Failed("Diagram produced no output.")
                : DiagramResult.SuccessSvg(svg);
        }
        catch (Exception ex)
        {
            return DiagramResult.Failed(ex.Message);
        }
    }
}
