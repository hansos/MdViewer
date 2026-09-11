namespace MdViewer.Rendering.Diagrams;

/// <summary>
/// Input options shared by diagram renderers.
/// </summary>
public sealed class DiagramOptions
{
    public DiagramOptions(
        string theme,
        double dpi,
        double fontSize,
        string? bodyFontFamily,
        string? monoFontFamily)
    {
        Theme = string.IsNullOrWhiteSpace(theme) ? "default" : theme.Trim();
        Dpi = dpi <= 0 ? 96d : dpi;
        FontSize = fontSize <= 0 ? 15d : fontSize;
        BodyFontFamily = string.IsNullOrWhiteSpace(bodyFontFamily) ? null : bodyFontFamily.Trim();
        MonoFontFamily = string.IsNullOrWhiteSpace(monoFontFamily) ? null : monoFontFamily.Trim();
    }

    /// <summary>Theme key used both for rendering choice and cache identity.</summary>
    public string Theme { get; }

    /// <summary>Output DPI used for rasterization and cache identity.</summary>
    public double Dpi { get; }

    /// <summary>Base body font size in px.</summary>
    public double FontSize { get; }

    /// <summary>Preferred body font family.</summary>
    public string? BodyFontFamily { get; }

    /// <summary>Preferred monospace font family.</summary>
    public string? MonoFontFamily { get; }
}
