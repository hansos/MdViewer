using Avalonia;
using Avalonia.Controls;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Bullet, ordered and task lists (SPECIFICATION.md 5.2).
///
/// Each item is a two-column grid — marker, then content — so a wrapped line
/// stays aligned under the first line of its item instead of sliding back under
/// the bullet. That is the whole reason this is not a StackPanel with a prefix
/// string.
/// </summary>
public sealed class ListBlockRenderer : IBlockRenderer
{
    private const double MarkerColumnWidth = 26;

    public bool CanRender(Block block) => block is ListBlock;

    public Control Render(Block block, RenderContext context)
    {
        var list = (ListBlock)block;
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        var ordinal = list.IsOrdered ? ParseStart(list.OrderedStart) : 0;

        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
            {
                stack.Children.Add(context.Renderer.RenderBlock(child, context));
                continue;
            }

            stack.Children.Add(RenderItem(list, item, context, ref ordinal));
        }

        context.SpanRegistrar.Register(stack, list.Span.Start, list.Span.Length);
        return stack;
    }

    private static Control RenderItem(
        ListBlock list,
        ListItemBlock item,
        RenderContext context,
        ref int ordinal)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{MarkerColumnWidth},*")
        };

        var content = new StackPanel();
        foreach (var child in context.Renderer.RenderChildren(item, context.Nested()))
        {
            content.Children.Add(child);
        }

        var marker = BuildMarker(list, item, ref ordinal);

        Grid.SetColumn(marker, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(marker);
        grid.Children.Add(content);

        context.SpanRegistrar.Register(grid, item.Span.Start, item.Span.Length);
        return grid;
    }

    private static Control BuildMarker(ListBlock list, ListItemBlock item, ref int ordinal)
    {
        var taskState = FindTaskState(item);

        string glyph;
        if (taskState is not null)
        {
            glyph = taskState.Value ? "☑" : "☐";   // ☑ / ☐
        }
        else if (list.IsOrdered)
        {
            glyph = $"{ordinal}{list.OrderedDelimiter}";
            ordinal++;
        }
        else
        {
            glyph = "•";                                 // •
        }

        var marker = new TextBlock
        {
            Text = glyph,
            Margin = new Thickness(0, 0, 8, 0)
        };

        marker.Classes.Add("md-list-marker");

        // Checked task items get the accent; see MarkdownStyles.axaml.
        if (taskState == true) marker.Classes.Add("checked");

        return marker;
    }

    /// <summary>
    /// A task list marker is the first inline of the item's first paragraph.
    /// Returns null for an ordinary list item.
    /// </summary>
    private static bool? FindTaskState(ListItemBlock item)
    {
        if (item.Count == 0) return null;
        if (item[0] is not ParagraphBlock { Inline: not null } paragraph) return null;

        var first = paragraph.Inline.FirstChild;
        return first is TaskList task ? task.Checked : null;
    }

    private static int ParseStart(string? orderedStart) =>
        int.TryParse(orderedStart, out var value) ? value : 1;
}
