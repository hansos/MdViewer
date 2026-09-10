using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Fenced and indented code blocks (SPECIFICATION.md 5.3).
///
/// M2 renders structure and chrome; the text is plain. Syntax highlighting via
/// TextMateSharp lands in M3 and slots in here without changing this shape:
/// the single <c>Run</c> becomes a sequence of scope-coloured runs.
/// </summary>
public sealed class CodeBlockRenderer : IBlockRenderer
{
    /// <summary>
    /// Above this, the block renders as plain text with a notice rather than
    /// being tokenised. A single enormous generated block should not cost a
    /// visible pause (SPECIFICATION.md 5.3).
    /// </summary>
    public const int LargeBlockThreshold = 200 * 1024;

    /// <summary>
    /// Vertical space reserved below scrollable content for the overlaid
    /// horizontal scrollbar so the last line stays readable.
    /// </summary>
    private const double ScrollBarGutter = 16;

    public bool CanRender(Block block) => block is CodeBlock;

    public Control Render(Block block, RenderContext context)
    {
        var code = (CodeBlock)block;
        var text = ExtractText(code);
        var language = (code as FencedCodeBlock)?.Info?.Trim() ?? string.Empty;

        var body = new SelectableTextBlock { Text = text };
        body.Classes.Add("md-code");

        var content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
            // The horizontal scrollbar floats over the content, so reserve space for it.
            Padding = new Avalonia.Thickness(0, 0, 0, ScrollBarGutter),
            Margin = new Avalonia.Thickness(14, 10, 14, 12)
        };

        var stack = new StackPanel();

        if (!string.IsNullOrEmpty(language) || text.Length > 0)
        {
            stack.Children.Add(BuildHeader(language, text));
        }

        stack.Children.Add(content);

        if (text.Length > LargeBlockThreshold)
        {
            var notice = new TextBlock
            {
                Text = "Large block — syntax highlighting skipped.",
                Margin = new Avalonia.Thickness(14, 0, 14, 10)
            };
            notice.Classes.Add("md-caption");
            stack.Children.Add(notice);
        }

        var frame = new Border { Child = stack };
        frame.Classes.Add("md-code-block");

        context.SpanRegistrar.Register(frame, code.Span.Start, code.Span.Length);
        return frame;
    }

    private static Border BuildHeader(string language, string sourceText)
    {
        var label = new TextBlock { Text = string.IsNullOrEmpty(language) ? "text" : language };
        label.Classes.Add("md-code-lang");

        var copy = new Button
        {
            Content = "Copy",
            FontSize = 11,
            Padding = new Avalonia.Thickness(6, 2),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        copy.Classes.Add("icon");

        // The raw source is copied, never the rendered text — once highlighting
        // lands, those are not the same string.
        copy.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(copy)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(sourceText);
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        Grid.SetColumn(label, 0);
        Grid.SetColumn(copy, 1);
        grid.Children.Add(label);
        grid.Children.Add(copy);

        var header = new Border { Child = grid };
        header.Classes.Add("md-code-header");
        return header;
    }

    private static string ExtractText(CodeBlock code)
    {
        var lines = code.Lines.Lines;
        if (lines is null || code.Lines.Count == 0) return string.Empty;

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < code.Lines.Count; i++)
        {
            if (i > 0) builder.Append('\n');
            builder.Append(lines[i].Slice.ToString());
        }

        return builder.ToString();
    }
}
