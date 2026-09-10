using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Pipe and grid tables (SPECIFICATION.md 5.4).
///
/// Column sizing here is the pragmatic first cut: every column sizes to its
/// content except the last, which takes the slack so the table fills the
/// measure. The full algorithm in 5.4 — measure-based auto-sizing that degrades
/// to proportional star sizing, with a sticky first column past the overflow
/// threshold — is an M3 refinement that replaces only <see cref="BuildColumns"/>.
///
/// The table always scrolls horizontally inside its own viewport rather than
/// widening the page: the document body must never scroll sideways.
/// </summary>
public sealed class TableBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is Table;

    public Control Render(Block block, RenderContext context)
    {
        var table = (Table)block;

        var rows = table.OfType<TableRow>().ToList();
        var columnCount = rows.Count == 0 ? 0 : rows.Max(r => r.Count);

        if (columnCount == 0)
        {
            return new Panel { IsVisible = false };
        }

        var grid = new Grid
        {
            ColumnDefinitions = BuildColumns(columnCount),
            RowDefinitions = new RowDefinitions(string.Join(',', Enumerable.Repeat("Auto", rows.Count)))
        };

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var isLastRow = rowIndex == rows.Count - 1;

            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (row[columnIndex] is not TableCell cell) continue;

                var control = BuildCell(
                    cell,
                    table,
                    columnIndex,
                    columnCount,
                    row.IsHeader,
                    isLastRow,
                    rowIndex,
                    context);

                Grid.SetRow(control, rowIndex);
                Grid.SetColumn(control, columnIndex);
                if (cell.ColumnSpan > 1) Grid.SetColumnSpan(control, cell.ColumnSpan);
                if (cell.RowSpan > 1) Grid.SetRowSpan(control, cell.RowSpan);

                grid.Children.Add(control);
            }
        }

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = grid
        };

        var frame = new Border { Child = scroller };
        frame.Classes.Add("md-table");

        context.SpanRegistrar.Register(frame, table.Span.Start, table.Span.Length);
        return frame;
    }

    private static ColumnDefinitions BuildColumns(int columnCount)
    {
        var parts = new string[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            parts[i] = i == columnCount - 1 ? "*" : "Auto";
        }

        return new ColumnDefinitions(string.Join(',', parts));
    }

    private static Control BuildCell(
        TableCell cell,
        Table table,
        int columnIndex,
        int columnCount,
        bool isHeader,
        bool isLastRow,
        int rowIndex,
        RenderContext context)
    {
        var content = new StackPanel();
        foreach (var child in context.Renderer.RenderChildren(cell, context.Nested()))
        {
            ApplyCellStyling(child, isHeader, Alignment(table, columnIndex));
            content.Children.Add(child);
        }

        var border = new Border
        {
            Child = content,
            // Right border on all but the last column, bottom on all but the
            // last row: the frame supplies the outer edge.
            BorderThickness = new Thickness(
                0,
                0,
                columnIndex == columnCount - 1 ? 0 : 1,
                isLastRow ? 0 : 1)
        };

        border.Classes.Add("md-table-cell");
        if (isHeader) border.Classes.Add("md-table-header");
        else if (rowIndex % 2 == 0) border.Classes.Add("alt");

        context.SpanRegistrar.Register(border, cell.Span.Start, cell.Span.Length);
        return border;
    }

    private static void ApplyCellStyling(Control control, bool isHeader, TextAlignment alignment)
    {
        if (control is not TextBlock text) return;

        // Cells use tighter type than body copy.
        text.Classes.Remove("md-body");
        text.Classes.Add("md-table-text");
        if (isHeader) text.Classes.Add("header");

        text.TextAlignment = alignment;
    }

    private static TextAlignment Alignment(Table table, int columnIndex)
    {
        if (columnIndex >= table.ColumnDefinitions.Count) return TextAlignment.Left;

        return table.ColumnDefinitions[columnIndex].Alignment switch
        {
            TableColumnAlign.Center => TextAlignment.Center,
            TableColumnAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };
    }
}
