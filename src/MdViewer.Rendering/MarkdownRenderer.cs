using Avalonia;
using Avalonia.Controls;
using Markdig.Syntax;
using MdViewer.Rendering.Blocks;
using MdViewer.Rendering.Inlines;

namespace MdViewer.Rendering;

/// <summary>
/// Turns a parsed document into an Avalonia visual tree (SPECIFICATION.md 5.2).
///
/// Must be called on the UI thread. Parsing and file access happen off it, in
/// MdViewer.Core; this class does nothing but build controls.
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly BlockRendererRegistry _registry;

    public MarkdownRenderer(BlockRendererRegistry? registry = null)
    {
        _registry = registry ?? BlockRendererRegistry.CreateDefault();
        Inlines = new InlineRenderer();
    }

    public InlineRenderer Inlines { get; }

    public Control RenderDocument(MarkdownDocument document, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(document);

        // M2 realises every block. Virtualization with height estimation is M7
        // (SPECIFICATION.md 6.2); it replaces this panel and nothing else,
        // because selection and find live outside the visual tree by design.
        var stack = new StackPanel();

        foreach (var control in RenderChildren(document, context))
        {
            stack.Children.Add(control);
        }

        return stack;
    }

    public IEnumerable<Control> RenderChildren(ContainerBlock container, RenderContext context)
    {
        foreach (var child in container)
        {
            yield return RenderBlock(child, context);
        }
    }

    /// <summary>
    /// Renders one block. A renderer that throws produces an inline error card
    /// naming the block and its source line; the rest of the document renders
    /// normally (SPECIFICATION.md 6.4). One malformed table must not cost the
    /// reader the other forty pages.
    /// </summary>
    public Control RenderBlock(Block block, RenderContext context)
    {
        try
        {
            var renderer = _registry.Resolve(block);
            if (renderer is null)
            {
                return BuildUnsupportedCard(block, context);
            }

            var control = renderer.Render(block, context);

            // Author-supplied {: .class #id } wins over nothing and loses to
            // nothing: it is added on top of whatever classes the renderer set.
            MarkdownAttributes.Apply(block, control);

            return control;
        }
        catch (Exception ex)
        {
            return BuildErrorCard(block, ex, context);
        }
    }

    /// <summary>
    /// Builds a text block for a leaf block's inline content, applies the given
    /// style class, and registers its source span. Every text-bearing renderer
    /// goes through here so the registration can never be forgotten.
    /// </summary>
    public MarkdownTextBlock CreateTextBlock(LeafBlock leaf, string styleClass, RenderContext context)
    {
        var text = new MarkdownTextBlock();
        text.Classes.Add(styleClass);

        Inlines.Render(leaf.Inline, text, context);
        context.SpanRegistrar.Register(text, leaf.Span.Start, leaf.Span.Length);

        return text;
    }

    private static Control BuildUnsupportedCard(Block block, RenderContext context)
    {
        var card = BuildCard(
            $"{block.GetType().Name} is not rendered yet",
            $"Line {block.Line + 1}",
            context,
            block);

        return card;
    }

    private static Control BuildErrorCard(Block block, Exception ex, RenderContext context)
    {
        return BuildCard(
            $"Could not render {block.GetType().Name}",
            $"Line {block.Line + 1} — {ex.Message}",
            context,
            block);
    }

    private static Control BuildCard(string title, string detail, RenderContext context, Block block)
    {
        var heading = new TextBlock { Text = title };
        heading.Classes.Add("md-callout-title");

        var body = new TextBlock { Text = detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        body.Classes.Add("md-caption");

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(heading);
        stack.Children.Add(body);

        var frame = new Border { Child = stack, Margin = new Thickness(0, 0, 0, 14) };
        frame.Classes.Add("md-callout");
        frame.Classes.Add("warning");

        context.SpanRegistrar.Register(frame, block.Span.Start, block.Span.Length);
        return frame;
    }
}
