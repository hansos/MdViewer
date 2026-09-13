using Avalonia;
using Avalonia.Controls;
using Markdig.Extensions.DefinitionLists;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Definition lists: a term, then one or more indented definitions
/// (SPECIFICATION.md 5.2).
///
/// Markdig models the list as DefinitionList &gt; DefinitionItem, where an item
/// holds its DefinitionTerm leaves followed by the blocks that define them.
/// Terms render flush left; definitions are indented so the relationship stays
/// visible without a marker glyph.
/// </summary>
public sealed class DefinitionListBlockRenderer : IBlockRenderer
{
    private const double DefinitionIndent = 26;

    public bool CanRender(Block block) => block is DefinitionList;

    public Control Render(Block block, RenderContext context)
    {
        var list = (DefinitionList)block;
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        foreach (var child in list)
        {
            if (child is not DefinitionItem item)
            {
                stack.Children.Add(context.Renderer.RenderBlock(child, context));
                continue;
            }

            stack.Children.Add(RenderItem(item, context));
        }

        context.SpanRegistrar.Register(stack, list.Span.Start, list.Span.Length);
        return stack;
    }

    private static Control RenderItem(DefinitionItem item, RenderContext context)
    {
        var stack = new StackPanel();

        foreach (var child in item)
        {
            if (child is DefinitionTerm term)
            {
                stack.Children.Add(context.Renderer.CreateTextBlock(term, "md-def-term", context));
                continue;
            }

            var definition = context.Renderer.RenderBlock(child, context.Nested());

            // Set locally rather than in the theme: the indent has to win over
            // whatever margin the inner block's own style brings with it.
            definition.Margin = new Thickness(DefinitionIndent, 0, 0, 6);

            if (definition is TextBlock text)
            {
                text.Classes.Remove("md-body");
                text.Classes.Add("md-def-definition");
            }

            stack.Children.Add(definition);
        }

        context.SpanRegistrar.Register(stack, item.Span.Start, item.Span.Length);
        return stack;
    }
}
