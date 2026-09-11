namespace MdViewer.Rendering.Diagrams;

/// <summary>
/// Result of a diagram render request.
/// </summary>
public readonly struct DiagramResult
{
    private DiagramResult(bool isSuccess, string? svg, string? message)
    {
        IsSuccess = isSuccess;
        Svg = svg;
        Message = message;
    }

    public bool IsSuccess { get; }

    /// <summary>Sanitized SVG markup for in-process renderers.</summary>
    public string? Svg { get; }

    public string? Message { get; }

    public static DiagramResult SuccessSvg(string svg) =>
        new(true, svg, null);

    public static DiagramResult Failed(string message) =>
        new(false, null, message);
}
