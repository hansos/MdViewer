using Avalonia.Controls;
using Markdig.Syntax;

namespace MdViewer.Rendering;

/// <summary>
/// Renders one Markdig block into an Avalonia control (SPECIFICATION.md 5.2).
///
/// Renderers are resolved through <see cref="BlockRendererRegistry"/> rather
/// than a switch, so a renderer can be replaced or added — a diagram renderer,
/// a custom container — without touching the presenter.
/// </summary>
public interface IBlockRenderer
{
    bool CanRender(Block block);

    Control Render(Block block, RenderContext context);
}
