using Avalonia;
using Avalonia.Controls;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Renders Markdig footnotes at the end of the document (SPECIFICATION.md 5.2).
/// </summary>
public sealed class FootnoteGroupRenderer : IBlockRenderer
{
    private const double MarkerColumnWidth = 26;

    public bool CanRender(Block block) => block is FootnoteGroup;

    public Control Render(Block block, RenderContext context)
    {
        var group = (FootnoteGroup)block;
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        var separator = new Border();
        separator.Classes.Add("md-rule");
        stack.Children.Add(separator);

        var fallbackOrder = 1;
        foreach (var child in group)
        {
            if (child is Footnote footnote)
            {
                stack.Children.Add(RenderFootnote(footnote, context, fallbackOrder));
                fallbackOrder++;
                continue;
            }

            stack.Children.Add(context.Renderer.RenderBlock(child, context.Nested()));
        }

        context.SpanRegistrar.Register(stack, group.Span.Start, group.Span.Length);
        return stack;
    }

    private static Control RenderFootnote(Footnote footnote, RenderContext context, int fallbackOrder)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{MarkerColumnWidth},*")
        };

        var marker = new TextBlock
        {
            Text = $"{ResolveOrder(footnote, fallbackOrder)}.",
            Margin = new Thickness(0, 0, 8, 0)
        };
        marker.Classes.Add("md-list-marker");

        var content = new StackPanel();
        foreach (var child in context.Renderer.RenderChildren(footnote, context.Nested()))
        {
            ApplyFootnoteTextStyling(child);
            content.Children.Add(child);
        }

        Grid.SetColumn(marker, 0);
        Grid.SetColumn(content, 1);
        row.Children.Add(marker);
        row.Children.Add(content);

        context.SpanRegistrar.Register(row, footnote.Span.Start, footnote.Span.Length);
        return row;
    }

    private static void ApplyFootnoteTextStyling(Control control)
    {
        if (control is not TextBlock text) return;

        text.Classes.Remove("md-body");
        text.Classes.Add("md-caption");
        text.Margin = new Thickness(0, 0, 0, 4);
    }

    private static int ResolveOrder(Footnote footnote, int fallbackOrder) =>
        footnote.Order > 0 ? footnote.Order : fallbackOrder;
}
