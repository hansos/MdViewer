using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Markdig.Syntax;
using MdViewer.Rendering.Diagrams;

namespace MdViewer.Rendering.Blocks;

public sealed class DiagramBlockRenderer : IBlockRenderer
{
    private const int MaxSourceLength = 100 * 1024;

    public bool CanRender(Block block) =>
        block is FencedCodeBlock fenced
        && DiagramRendererRegistry.Default.Supports(fenced.Info);

    public Control Render(Block block, RenderContext context)
    {
        var fenced = (FencedCodeBlock)block;
        var source = ExtractText(fenced);
        var language = ExtractLanguage(fenced.Info ?? string.Empty);

        var stack = new StackPanel();
        stack.Children.Add(BuildHeader(language, source));

        if (source.Length > MaxSourceLength)
        {
            stack.Children.Add(BuildFallbackSource(source));
            stack.Children.Add(BuildCaption("Diagram too large to render."));
            return BuildFrame(stack, fenced, context);
        }

        if (context.ReducedMode)
        {
            stack.Children.Add(BuildFallbackSource(source));
            stack.Children.Add(BuildCaption("Diagram rendering disabled in reduced mode."));
            return BuildFrame(stack, fenced, context);
        }

        var renderer = DiagramRendererRegistry.Default.Resolve(language);
        if (renderer is null)
        {
            stack.Children.Add(BuildFallbackSource(source));
            stack.Children.Add(BuildCaption("Diagram rendering unavailable — showing source."));
            return BuildFrame(stack, fenced, context);
        }

        var options = new DiagramOptions(
            context.DiagramTheme,
            context.DiagramDpi,
            context.DiagramFontSize,
            context.DiagramBodyFontFamily,
            context.DiagramMonoFontFamily);

        var result = renderer.Render(source, options);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Svg))
        {
            stack.Children.Add(BuildFallbackSource(source));
            stack.Children.Add(BuildCaption($"Diagram could not be rendered: {result.Message ?? "Unknown error."}"));
            return BuildFrame(stack, fenced, context);
        }

        var diagram = new DiagramView
        {
            Svg = result.Svg,
            ErrorText = result.Message
        };

        stack.Children.Add(diagram);
        return BuildFrame(stack, fenced, context);
    }

    private static Border BuildFrame(StackPanel stack, FencedCodeBlock block, RenderContext context)
    {
        var frame = new Border { Child = stack };
        frame.Classes.Add("md-code-block");
        context.SpanRegistrar.Register(frame, block.Span.Start, block.Span.Length);
        return frame;
    }

    private static Border BuildHeader(string language, string sourceText)
    {
        var label = new TextBlock { Text = string.IsNullOrEmpty(language) ? "diagram" : language };
        label.Classes.Add("md-code-lang");

        var copy = new Button
        {
            Content = "Copy",
            FontSize = 11,
            Padding = new Thickness(6, 2),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        copy.Classes.Add("icon");
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

    private static Control BuildFallbackSource(string source)
    {
        var body = new SelectableTextBlock
        {
            Text = source
        };

        body.Classes.Add("md-code");

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
            Padding = new Thickness(0, 0, 0, 16),
            Margin = new Thickness(14, 10, 14, 12)
        };
    }

    private static TextBlock BuildCaption(string message)
    {
        var caption = new TextBlock
        {
            Text = message,
            Margin = new Thickness(14, 0, 14, 10)
        };

        caption.Classes.Add("md-caption");
        return caption;
    }

    private static string ExtractLanguage(string info)
    {
        if (string.IsNullOrWhiteSpace(info)) return string.Empty;

        var separator = info.IndexOfAny([' ', '\t', '{']);
        return separator < 0 ? info : info[..separator];
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
