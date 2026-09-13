using Avalonia.Controls;
using Markdig.Extensions.Figures;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Figures — <c>^^^</c> fenced content with an optional caption on the closing
/// fence (SPECIFICATION.md 5.2).
///
/// The body is rendered with the ordinary block renderers, so a figure can hold
/// an image, a table, a code block or anything else. The caption is a leaf block
/// with inline content and is always placed below the body, matching how the
/// HTML &lt;figcaption&gt; reads.
/// </summary>
public sealed class FigureBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is Figure;

    public Control Render(Block block, RenderContext context)
    {
        var figure = (Figure)block;

        var stack = new StackPanel();
        var nested = context.Nested();

        foreach (var child in figure)
        {
            stack.Children.Add(child is FigureCaption caption
                ? context.Renderer.CreateTextBlock(caption, "md-figure-caption", nested)
                : context.Renderer.RenderBlock(child, nested));
        }

        var frame = new Border { Child = stack };
        frame.Classes.Add("md-figure");

        context.SpanRegistrar.Register(frame, figure.Span.Start, figure.Span.Length);
        return frame;
    }
}
