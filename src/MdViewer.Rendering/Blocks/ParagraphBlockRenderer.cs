using Avalonia.Controls;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>Paragraphs — the bulk of every document (SPECIFICATION.md 5.2).</summary>
public sealed class ParagraphBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is ParagraphBlock;

    public Control Render(Block block, RenderContext context) =>
        context.Renderer.CreateTextBlock((ParagraphBlock)block, "md-body", context);
}
