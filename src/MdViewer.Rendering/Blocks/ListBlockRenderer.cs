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
            if (!list.IsLoose) ApplyItemTextStyling(child);
            content.Children.Add(child);
        }

        var marker = BuildMarker(list, item, context, ref ordinal);

        Grid.SetColumn(marker, 0);
        Grid.SetColumn(content, 1);
        grid.Children.Add(marker);
        grid.Children.Add(content);

        context.SpanRegistrar.Register(grid, item.Span.Start, item.Span.Length);
        return grid;
    }

    /// <summary>
    /// Paragraphs inside a tight list item come back with the body class, whose
    /// margin is paragraph-sized. Swap it for the tighter list item spacing.
    /// </summary>
    private static void ApplyItemTextStyling(Control control)
    {
        if (control is not TextBlock text) return;

        text.Classes.Remove("md-body");
        text.Classes.Add("md-list-item");
    }

    private static Control BuildMarker(
        ListBlock list,
        ListItemBlock item,
        RenderContext context,
        ref int ordinal)
    {
        var task = FindTask(item);

        if (task is not null && context.AllowTaskListEditing && context.OnTaskListToggled is not null)
        {
            return BuildEditableTaskMarker(task, context);
        }

        string glyph;
        if (task is not null)
        {
            glyph = task.Checked ? "☑" : "☐";   // ☑ / ☐
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
        if (task is { Checked: true }) marker.Classes.Add("checked");

        return marker;
    }

    /// <summary>
    /// The interactive form of a task marker (SPECIFICATION.md 5.2). The source
    /// offset of the marker travels with the control, so the shell can patch the
    /// single character in the file without re-serialising the document.
    /// </summary>
    private static Control BuildEditableTaskMarker(TaskList task, RenderContext context)
    {
        var offset = task.Span.Start;
        var onToggled = context.OnTaskListToggled!;
        var suppress = false;

        var box = new CheckBox
        {
            IsChecked = task.Checked,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };

        box.Classes.Add("md-task-check");

        box.IsCheckedChanged += (_, _) =>
        {
            // The rebuild that follows the write re-creates this control, but
            // guard anyway so a programmatic state change cannot re-enter.
            if (suppress) return;
            suppress = true;

            onToggled(offset, box.IsChecked == true);
        };

        return box;
    }

    /// <summary>
    /// A task list marker is the first inline of the item's first paragraph.
    /// Returns null for an ordinary list item.
    /// </summary>
    private static TaskList? FindTask(ListItemBlock item)
    {
        if (item.Count == 0) return null;
        if (item[0] is not ParagraphBlock { Inline: not null } paragraph) return null;

        return paragraph.Inline.FirstChild as TaskList;
    }

    private static int ParseStart(string? orderedStart) =>
        int.TryParse(orderedStart, out var value) ? value : 1;
}
