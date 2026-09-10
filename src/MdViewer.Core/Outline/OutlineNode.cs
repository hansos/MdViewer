namespace MdViewer.Core.Outline;

/// <summary>
/// A heading in the document outline (SPECIFICATION.md 5.8).
/// Built from the Markdig heading tree at parse time; in this
/// GUI-evaluation build the tree is supplied by <c>SampleData</c>.
/// </summary>
public sealed class OutlineNode
{
    public OutlineNode(string title, int level, string anchor, int sourceOffset)
    {
        Title = title;
        Level = level;
        Anchor = anchor;
        SourceOffset = sourceOffset;
        Children = new List<OutlineNode>();
    }

    public string Title { get; }

    /// <summary>Heading level, 1-6.</summary>
    public int Level { get; }

    /// <summary>Auto-identifier used for <c>#anchor</c> links.</summary>
    public string Anchor { get; }

    /// <summary>
    /// Byte offset of the heading in the source text. Scroll positioning,
    /// search and the future editor all work in source-offset space.
    /// </summary>
    public int SourceOffset { get; }

    public List<OutlineNode> Children { get; }

    public OutlineNode AddChild(OutlineNode child)
    {
        Children.Add(child);
        return this;
    }
}
