using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using MdViewer.Core.Markdown;
using MdViewer.Rendering.Images;

namespace MdViewer.Rendering.Inlines;

/// <summary>
/// Turns a Markdig inline tree into Avalonia inlines (SPECIFICATION.md 5.2).
///
/// Everything becomes a <c>Run</c> or a <c>Span</c> — no embedded controls —
/// so text wraps, selects and copies as one continuous flow. Link click targets
/// are recorded as character ranges on the host <see cref="MarkdownTextBlock"/>
/// instead (see that class for why).
/// </summary>
public sealed class InlineRenderer
{
    /// <summary>
    /// Fills <paramref name="target"/> with the rendered inlines and registers
    /// its link ranges.
    /// </summary>
    public void Render(ContainerInline? container, MarkdownTextBlock target, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(target);

        var sink = EnsureInlines(target);
        var position = 0;
        AppendChildren(container?.FirstChild, target, sink, context, ref position);
        target.LinkActivated = context.OnLinkActivated;
    }

    private void AppendChildren(
        MarkdigInline? inline,
        MarkdownTextBlock target,
        InlineCollection sink,
        RenderContext context,
        ref int position)
    {
        while (inline is not null)
        {
            Append(inline, target, sink, context, ref position);
            inline = inline.NextSibling;
        }
    }

    private void Append(
        MarkdigInline inline,
        MarkdownTextBlock target,
        InlineCollection sink,
        RenderContext context,
        ref int position)
    {
        switch (inline)
        {
            case TaskList:
                // The checkbox is drawn in the list item's marker column, not
                // in the text flow (see ListBlockRenderer).
                break;

            case LiteralInline literal:
            {
                var text = literal.Content.ToString();
                if (text.Length == 0) break;

                AppendTextWithFindHighlights(
                    sink,
                    text,
                    literal.Span.Start,
                    context);

                position += text.Length;
                break;
            }

            case CodeInline code:
            {
                var text = code.Content ?? string.Empty;
                if (text.Length == 0) break;

                AppendTextWithFindHighlights(
                    sink,
                    text,
                    code.Span.Start,
                    context,
                    run =>
                    {
                        run.Apply(TextElement.FontFamilyProperty, Themed.Keys.MonoFontFamily);
                        run.Apply(TextElement.ForegroundProperty, Themed.Keys.CodeInlineForeground);
                        run.Apply(TextElement.BackgroundProperty, Themed.Keys.CodeInlineBackground);
                    });

                position += text.Length;
                break;
            }

            case EmphasisInline emphasis:
            {
                var span = CreateEmphasisSpan(emphasis);
                AppendChildren(emphasis.FirstChild, target, EnsureInlines(span), context, ref position);
                sink.Add(span);
                break;
            }

            case LinkInline { IsImage: true } image:
            {
                // Images in a text flow are real images too (5.6); they just get
                // capped so a paragraph is not swallowed by a picture. Remote
                // images still require consent, which the loader enforces.
                var content = MarkdownImages.CreateInline(image, context);

                sink.Add(new InlineUIContainer(content));

                // Body text pins LineHeight, and a text line clips whatever is
                // taller than that — which is why an embedded image showed only
                // its top edge. A local value overrides the style so the line
                // grows to the image instead.
                target.LineHeight = double.NaN;

                // An embedded object occupies a single position in the text
                // flow, which keeps link ranges around it aligned.
                position += 1;
                break;
            }

            case LinkInline link:
            {
                var start = position;

                var span = new Span { TextDecorations = TextDecorations.Underline };
                span.Apply(TextElement.ForegroundProperty, Themed.Keys.Link);

                var linkInlines = EnsureInlines(span);
                AppendChildren(link.FirstChild, target, linkInlines, context, ref position);

                // An empty link label still needs something to click.
                if (position == start)
                {
                    var label = link.Url ?? string.Empty;
                    linkInlines.Add(new Run(label));
                    position += label.Length;
                }

                sink.Add(span);
                target.AddLinkRange(start, position - start, link.Url ?? string.Empty);
                break;
            }

            case FootnoteLink footnoteLink:
            {
                var text = footnoteLink.IsBackLink
                    ? "↩"
                    : $"[{ResolveFootnoteNumber(footnoteLink)}]";

                var run = new Run(text) { TextDecorations = TextDecorations.Underline };
                run.Apply(TextElement.ForegroundProperty, Themed.Keys.Link);

                sink.Add(run);
                position += text.Length;
                break;
            }

            case AutolinkInline autolink:
            {
                var text = autolink.Url ?? string.Empty;
                if (text.Length == 0) break;

                var run = new Run(text) { TextDecorations = TextDecorations.Underline };
                run.Apply(TextElement.ForegroundProperty, Themed.Keys.Link);
                sink.Add(run);
                target.AddLinkRange(position, text.Length, text);
                position += text.Length;
                break;
            }

            case LineBreakInline lineBreak:
            {
                // A soft break is a space in the output; a hard break is a break.
                if (lineBreak.IsHard)
                {
                    sink.Add(new LineBreak());
                    position += 1;
                }
                else
                {
                    sink.Add(new Run(" "));
                    position += 1;
                }

                break;
            }

            case HtmlEntityInline entity:
            {
                var text = entity.Transcoded.ToString();
                if (text.Length == 0) break;
                sink.Add(new Run(text));
                position += text.Length;
                break;
            }

            case HtmlInline html:
            {
                // Raw HTML is escaped, not rendered (SPECIFICATION.md 5.6).
                // Showing it dimmed rather than hiding it means the reader can
                // see what is in the file.
                var text = html.Tag ?? string.Empty;
                if (text.Length == 0) break;

                var run = new Run(text);
                run.Apply(TextElement.FontFamilyProperty, Themed.Keys.MonoFontFamily);
                run.Apply(TextElement.ForegroundProperty, Themed.Keys.TextMuted);
                sink.Add(run);
                position += text.Length;
                break;
            }

            case ContainerInline container:
            {
                AppendChildren(container.FirstChild, target, sink, context, ref position);
                break;
            }

            default:
            {
                // Unknown inline from an extension we do not render yet: show
                // its text if it has any, drop it silently otherwise. Never throw.
                if (inline is LeafInline leaf)
                {
                    var text = leaf.ToString() ?? string.Empty;
                    if (text.Length > 0)
                    {
                        sink.Add(new Run(text));
                        position += text.Length;
                    }
                }

                break;
            }
        }
    }

    /// <summary>
    /// Avalonia has changed whether Inlines is created eagerly across versions.
    /// Going through here works either way and keeps the null-analysis quiet.
    /// </summary>
    private static InlineCollection EnsureInlines(TextBlock target)
    {
        var inlines = target.Inlines;
        if (inlines is null)
        {
            inlines = new InlineCollection();
            target.Inlines = inlines;
        }

        return inlines;
    }

    private static void AppendTextWithFindHighlights(
        InlineCollection sink,
        string text,
        int sourceStart,
        RenderContext context,
        Action<Run>? configure = null)
    {
        var matches = context.FindMatches;
        if (matches.Count == 0 || sourceStart < 0)
        {
            AddRun(sink, text, 0, text.Length, configure, highlighted: false);
            return;
        }

        var sourceEnd = sourceStart + text.Length;
        var cursor = 0;
        var startIndex = FindFirstPotentialMatchIndex(matches, sourceStart);

        for (var i = startIndex; i < matches.Count; i++)
        {
            var match = matches[i];
            var matchStart = match.Start;
            var matchEnd = match.Start + match.Length;

            if (matchStart >= sourceEnd) break;

            var localStart = Math.Max(matchStart, sourceStart) - sourceStart;
            var localEnd = Math.Min(matchEnd, sourceEnd) - sourceStart;

            if (localStart > cursor)
            {
                AddRun(
                    sink,
                    text,
                    cursor,
                    localStart - cursor,
                    configure,
                    highlighted: false);
            }

            if (localEnd > localStart)
            {
                AddRun(
                    sink,
                    text,
                    localStart,
                    localEnd - localStart,
                    configure,
                    highlighted: true);
            }

            cursor = Math.Max(cursor, localEnd);
        }

        if (cursor < text.Length)
        {
            AddRun(sink, text, cursor, text.Length - cursor, configure, highlighted: false);
        }
    }

    private static int FindFirstPotentialMatchIndex(IReadOnlyList<FindMatchOccurrence> matches, int sourceStart)
    {
        var low = 0;
        var high = matches.Count - 1;
        var result = matches.Count;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var end = matches[mid].Start + matches[mid].Length;

            if (end > sourceStart)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    private static void AddRun(
        InlineCollection sink,
        string text,
        int start,
        int length,
        Action<Run>? configure,
        bool highlighted)
    {
        if (length <= 0) return;

        var run = new Run(text.Substring(start, length));
        configure?.Invoke(run);

        if (highlighted)
        {
            run.Apply(
                TextElement.BackgroundProperty,
                Themed.Keys.FindMatch);
        }

        sink.Add(run);
    }

    private static InlineCollection EnsureInlines(Span span)
    {
        var inlines = span.Inlines;
        if (inlines is null)
        {
            inlines = new InlineCollection();
            span.Inlines = inlines;
        }

        return inlines;
    }

    private static Span CreateEmphasisSpan(EmphasisInline emphasis)
    {
        var span = new Span();

        switch (emphasis.DelimiterChar)
        {
            case '*':
            case '_':
                if (emphasis.DelimiterCount >= 3)
                {
                    span.FontWeight = FontWeight.Bold;
                    span.FontStyle = FontStyle.Italic;
                }
                else if (emphasis.DelimiterCount == 2)
                {
                    span.FontWeight = FontWeight.Bold;
                }
                else
                {
                    span.FontStyle = FontStyle.Italic;
                }

                break;

            case '~':
                // ~~strike~~ and ~sub~ (EmphasisExtras)
                span.TextDecorations = emphasis.DelimiterCount >= 2
                    ? TextDecorations.Strikethrough
                    : TextDecorations.Underline;
                break;

            case '+':
                span.TextDecorations = TextDecorations.Underline;
                break;

            case '=':
                span.Apply(TextElement.BackgroundProperty, Themed.Keys.FindMatch);
                break;

            case '^':
                span.FontStyle = FontStyle.Italic;
                break;
        }

        return span;
    }

    private static int ResolveFootnoteNumber(FootnoteLink footnoteLink)
    {
        if (footnoteLink.Footnote is { Order: > 0 } footnote)
        {
            return footnote.Order;
        }

        return footnoteLink.Index + 1;
    }
}
