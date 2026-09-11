using System.Text;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MdViewer.Core.Markdown;

/// <summary>
/// Flattens Markdig inline trees to plain text. Used for outline titles, tab
/// titles, word counts and accessibility labels — everywhere the words matter
/// and the formatting does not.
/// </summary>
public static class MarkdownText
{
    public static string ToPlainText(ContainerInline? container)
    {
        if (container is null) return string.Empty;

        var builder = new StringBuilder();
        Append(builder, container);
        return builder.ToString().Trim();
    }

    /// <summary>
    /// Words in the document body. Fenced code blocks are excluded: counting
    /// code as prose makes the number meaningless for the thing people
    /// actually use it for, which is judging how long a document is to read.
    /// </summary>
    public static int CountWords(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();

        foreach (var block in document.Descendants())
        {
            if (block is CodeBlock) continue;

            if (block is LeafBlock { Inline: not null } leaf)
            {
                Append(builder, leaf.Inline);
                builder.Append(' ');
            }
        }

        return CountWords(builder.ToString());
    }

    public static int CountWords(ReadOnlySpan<char> text)
    {
        var count = 0;
        var inWord = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    private static void Append(StringBuilder builder, Inline? inline)
    {
        while (inline is not null)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;

                case CodeInline code:
                    builder.Append(code.Content);
                    break;

                case MathInline math:
                    builder.Append(LatexUnicode.Convert(math.Content.ToString()));
                    break;

                case LineBreakInline:
                    builder.Append(' ');
                    break;

                case HtmlEntityInline entity:
                    builder.Append(entity.Transcoded.ToString());
                    break;

                case AutolinkInline autolink:
                    builder.Append(autolink.Url);
                    break;

                case ContainerInline container:
                    Append(builder, container.FirstChild);
                    break;

                // HtmlInline and anything else contributes no words.
            }

            inline = inline.NextSibling;
        }
    }

    private static void Append(StringBuilder builder, ContainerInline container)
        => Append(builder, container.FirstChild);
}
