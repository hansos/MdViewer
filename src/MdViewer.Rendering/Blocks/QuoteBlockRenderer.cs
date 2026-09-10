using Avalonia.Controls;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Block quotes, nesting to any depth (SPECIFICATION.md 5.2).
///
/// GitHub alerts (<c>&gt; [!NOTE]</c>) arrive here too until the alert
/// extension and the callout renderer land in M3 — they are block quotes
/// syntactically, so nothing is lost in the meantime.
/// </summary>
public sealed class QuoteBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is QuoteBlock;

    public Control Render(Block block, RenderContext context)
    {
        var quote = (QuoteBlock)block;

        var stack = new StackPanel();
        foreach (var child in context.Renderer.RenderChildren(quote, context.Nested()))
        {
            stack.Children.Add(child);
        }

        var frame = new Border { Child = stack };
        frame.Classes.Add("md-quote");

        context.SpanRegistrar.Register(frame, quote.Span.Start, quote.Span.Length);
        return frame;
    }
}

/// <summary>Thematic breaks (SPECIFICATION.md 5.2).</summary>
public sealed class ThematicBreakRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is ThematicBreakBlock;

    public Control Render(Block block, RenderContext context)
    {
        var rule = new Border();
        rule.Classes.Add("md-rule");
        context.SpanRegistrar.Register(rule, block.Span.Start, block.Span.Length);
        return rule;
    }
}

/// <summary>
/// Raw HTML blocks (SPECIFICATION.md 5.6).
///
/// HTML is escaped and shown dimmed in monospace rather than rendered. Native
/// rendering of arbitrary HTML would mean reimplementing a browser badly, and a
/// WebView would reintroduce the dependency the whole architecture rejects.
/// Showing it rather than hiding it means the reader can see what is in the file.
/// </summary>
public sealed class HtmlBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is HtmlBlock;

    public Control Render(Block block, RenderContext context)
    {
        var html = (HtmlBlock)block;

        var builder = new System.Text.StringBuilder();
        var lines = html.Lines.Lines;
        if (lines is not null)
        {
            for (var i = 0; i < html.Lines.Count; i++)
            {
                if (i > 0) builder.Append('\n');
                builder.Append(lines[i].Slice.ToString());
            }
        }

        var text = new SelectableTextBlock { Text = builder.ToString() };
        text.Classes.Add("md-code");
        text.Classes.Add("md-html-escaped");

        var frame = new Border { Child = text };
        frame.Classes.Add("md-html-block");

        context.SpanRegistrar.Register(frame, html.Span.Start, html.Span.Length);
        return frame;
    }
}
