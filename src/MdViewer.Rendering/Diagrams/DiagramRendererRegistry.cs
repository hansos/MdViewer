namespace MdViewer.Rendering.Diagrams;

/// <summary>
/// Resolves a diagram language to a registered renderer.
/// </summary>
public sealed class DiagramRendererRegistry
{
    private readonly List<IDiagramRenderer> _renderers = new();

    public static DiagramRendererRegistry Default { get; } = CreateDefault();

    public static DiagramRendererRegistry CreateDefault() =>
        new DiagramRendererRegistry()
            .Add(new MermaidInProcessDiagramRenderer());

    public DiagramRendererRegistry Add(IDiagramRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderers.Add(renderer);
        return this;
    }

    public IDiagramRenderer? Resolve(string language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;

        foreach (var renderer in _renderers)
        {
            foreach (var candidate in renderer.Languages)
            {
                if (string.Equals(candidate, language, StringComparison.OrdinalIgnoreCase))
                {
                    return renderer;
                }
            }
        }

        return null;
    }

    public bool Supports(string? fenceInfo)
    {
        var language = ExtractLanguage(fenceInfo);
        return Resolve(language) is not null;
    }

    private static string ExtractLanguage(string? info)
    {
        if (string.IsNullOrWhiteSpace(info)) return string.Empty;

        var trimmed = info.Trim();
        var separator = trimmed.IndexOfAny([' ', '\t', '{']);
        return separator < 0 ? trimmed : trimmed[..separator];
    }
}
