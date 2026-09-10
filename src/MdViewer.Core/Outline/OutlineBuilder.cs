using Markdig.Syntax;
using MdViewer.Core.Markdown;

namespace MdViewer.Core.Outline;

/// <summary>
/// Builds the heading tree from a parsed document (SPECIFICATION.md 5.8).
/// </summary>
public static class OutlineBuilder
{
    /// <summary>
    /// Nests headings by level, tolerating skipped levels: an H3 directly under
    /// an H1 becomes a child of that H1 rather than being dropped or promoted.
    /// Real documents skip levels constantly, and an outline that mangles them
    /// is worse than no outline.
    /// </summary>
    public static IReadOnlyList<OutlineNode> Build(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var roots = new List<OutlineNode>();
        var stack = new Stack<OutlineNode>();
        var slugs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var title = MarkdownText.ToPlainText(heading.Inline);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var anchor = HeadingSlug.MakeUnique(HeadingSlug.Create(title), slugs);
            var node = new OutlineNode(title, heading.Level, anchor, heading.Span.Start);

            // Unwind to the nearest heading shallower than this one.
            while (stack.Count > 0 && stack.Peek().Level >= node.Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                roots.Add(node);
            }
            else
            {
                stack.Peek().AddChild(node);
            }

            stack.Push(node);
        }

        return roots;
    }

    /// <summary>
    /// Depth-first walk, for the outline panel's flat operations: type-to-filter,
    /// "which heading is at the top of the viewport", and anchor lookup.
    /// </summary>
    public static IEnumerable<OutlineNode> Flatten(IEnumerable<OutlineNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }

    /// <summary>Resolves a <c>#anchor</c> to its heading, or null when unknown.</summary>
    public static OutlineNode? FindByAnchor(IEnumerable<OutlineNode> nodes, string anchor)
    {
        if (string.IsNullOrEmpty(anchor)) return null;

        var normalised = anchor.TrimStart('#');
        return Flatten(nodes).FirstOrDefault(
            n => string.Equals(n.Anchor, normalised, StringComparison.OrdinalIgnoreCase));
    }
}
