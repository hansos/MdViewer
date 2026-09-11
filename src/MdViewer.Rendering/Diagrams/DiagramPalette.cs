using Mermaider.Models;
using System.Globalization;

namespace MdViewer.Rendering.Diagrams;

/// <summary>
/// Maps the viewer's diagram theme tokens onto Mermaider render options.
/// </summary>
internal static class DiagramPalette
{
    public static RenderOptions CreateRenderOptions(DiagramOptions options)
    {
        var isDark = options.Theme.Contains("dark", StringComparison.OrdinalIgnoreCase);
        var font = string.IsNullOrWhiteSpace(options.BodyFontFamily)
            ? "Inter, Segoe UI, Ubuntu, Noto Sans, sans-serif"
            : options.BodyFontFamily;

        var mono = string.IsNullOrWhiteSpace(options.MonoFontFamily)
            ? "Cascadia Mono, Consolas, JetBrains Mono, DejaVu Sans Mono, monospace"
            : options.MonoFontFamily;

        return isDark
            ? new RenderOptions
            {
                Bg = "#1e1e1e",
                Surface = "#252526",
                Fg = "#e6e6e6",
                Line = "#8a8a8a",
                Border = "#3f3f46",
                Muted = "#a0a0a0",
                Accent = "#4fa3ff",
                Font = font,
                MonoFont = mono,
                FontSize = options.FontSize.ToString("0.###", CultureInfo.InvariantCulture) + "px",
                Transparent = true
            }
            : new RenderOptions
            {
                Bg = "#ffffff",
                Surface = "#f6f8fa",
                Fg = "#1f2328",
                Line = "#57606a",
                Border = "#d0d7de",
                Muted = "#656d76",
                Accent = "#0969da",
                Font = font,
                MonoFont = mono,
                FontSize = options.FontSize.ToString("0.###", CultureInfo.InvariantCulture) + "px",
                Transparent = true
            };
    }
}
