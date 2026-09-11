using Avalonia.Controls;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.CustomContainers;
using Markdig.Syntax;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Callouts (SPECIFICATION.md 5.1): GitHub alerts (<c>&gt; [!NOTE]</c>) and
/// fenced custom containers (<c>::: warning</c>).
///
/// Both shapes collapse onto the same four visual variants — note, tip,
/// warning, danger — so a document using either syntax looks the same. The
/// renderer must sit ahead of <see cref="QuoteBlockRenderer" /> in the registry
/// because an alert is a <see cref="QuoteBlock" /> subclass.
/// </summary>
public sealed class CalloutBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) => block is AlertBlock or CustomContainer;

    public Control Render(Block block, RenderContext context)
    {
        var (kind, title) = block switch
        {
            AlertBlock alert => Describe(alert.Kind.ToString()),
            CustomContainer container => Describe(container.Info),
            _ => ("note", "Note"),
        };

        var stack = new StackPanel();

        var heading = new TextBlock { Text = title };
        heading.Classes.Add("md-callout-title");
        heading.Apply(TextBlock.ForegroundProperty, AccentKey(kind));
        stack.Children.Add(heading);

        foreach (var child in context.Renderer.RenderChildren((ContainerBlock)block, context.Nested()))
        {
            stack.Children.Add(child);
        }

        var frame = new Border { Child = stack };
        frame.Classes.Add("md-callout");
        frame.Classes.Add(kind);

        context.SpanRegistrar.Register(frame, block.Span.Start, block.Span.Length);
        return frame;
    }

    private static (string Kind, string Title) Describe(string? info) => (info ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "tip" => ("tip", "Tip"),
        "success" => ("tip", "Success"),
        "important" => ("note", "Important"),
        "info" => ("note", "Info"),
        "warning" => ("warning", "Warning"),
        "caution" => ("danger", "Caution"),
        "danger" => ("danger", "Danger"),
        "error" => ("danger", "Error"),
        "" => ("note", "Note"),
        var other => ("note", char.ToUpperInvariant(other[0]) + other[1..]),
    };

    private static string AccentKey(string kind) => kind switch
    {
        "tip" => "CalloutTipAccentBrush",
        "warning" => "CalloutWarningAccentBrush",
        "danger" => "CalloutDangerAccentBrush",
        _ => "CalloutNoteAccentBrush",
    };
}
