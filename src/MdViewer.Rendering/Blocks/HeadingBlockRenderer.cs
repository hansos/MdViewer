using Avalonia.Controls;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>Headings, H1–H6 (SPECIFICATION.md 5.2).</summary>
public sealed class HeadingBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is HeadingBlock;

    public Control Render(Block block, RenderContext context)
    {
        var heading = (HeadingBlock)block;
        var level = Math.Clamp(heading.Level, 1, 6);

        var text = context.Renderer.CreateTextBlock(heading, $"md-h{level}", context);

        // H1 and H2 carry a hairline. It is a Border around the heading rather
        // than a bottom border on the text so the rule spans the content column
        // and not just the width of the words.
        if (level <= 2)
        {
            var framed = new Border { Child = text };
            framed.Classes.Add("md-heading-rule");
            context.SpanRegistrar.Register(framed, heading.Span.Start, heading.Span.Length);
            return framed;
        }

        return text;
    }
}
