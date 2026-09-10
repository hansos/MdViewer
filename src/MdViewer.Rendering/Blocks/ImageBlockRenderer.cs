using Avalonia.Controls;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdViewer.Rendering.Images;

namespace MdViewer.Rendering.Blocks;

/// <summary>
/// Standalone paragraph images (SPECIFICATION.md 5.6).
///
/// A paragraph that contains nothing but an image becomes a real block image.
/// Everything else keeps flowing through the inline pipeline, which renders
/// images through the same loader so both paths behave identically.
/// </summary>
public sealed class ImageBlockRenderer : IBlockRenderer
{
    public bool CanRender(Block block) =>
        block is ParagraphBlock paragraph && TryGetStandaloneImage(paragraph, out _);

    public Control Render(Block block, RenderContext context)
    {
        var paragraph = (ParagraphBlock)block;
        if (!TryGetStandaloneImage(paragraph, out var image))
        {
            return context.Renderer.CreateTextBlock(paragraph, "md-body", context);
        }

        var control = MarkdownImages.CreateBlock(image!, context);
        context.SpanRegistrar.Register(control, paragraph.Span.Start, paragraph.Span.Length);
        return control;
    }

    private static bool TryGetStandaloneImage(ParagraphBlock paragraph, out LinkInline? image)
    {
        image = null;

        if (paragraph.Inline is null)
        {
            return false;
        }

        var inline = paragraph.Inline.FirstChild;
        while (inline is not null)
        {
            switch (inline)
            {
                case LiteralInline literal when string.IsNullOrWhiteSpace(literal.Content.ToString()):
                case LineBreakInline:
                    inline = inline.NextSibling;
                    continue;

                case LinkInline { IsImage: true } candidate when image is null:
                    image = candidate;
                    inline = inline.NextSibling;
                    continue;

                // A link wrapping a single image is still a standalone image
                // block; the link target is handled by the inline pipeline.
                case LinkInline { IsImage: false } link when image is null && IsSingleImageLink(link, out var wrapped):
                    image = wrapped;
                    inline = inline.NextSibling;
                    continue;

                default:
                    return false;
            }
        }

        return image is not null;
    }

    private static bool IsSingleImageLink(LinkInline link, out LinkInline? image)
    {
        image = null;

        var child = link.FirstChild;
        while (child is not null)
        {
            switch (child)
            {
                case LiteralInline literal when string.IsNullOrWhiteSpace(literal.Content.ToString()):
                case LineBreakInline:
                    break;

                case LinkInline { IsImage: true } candidate when image is null:
                    image = candidate;
                    break;

                default:
                    return false;
            }

            child = child.NextSibling;
        }

        return image is not null;
    }
}
