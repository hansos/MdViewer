namespace MdViewer.Core.Documents;

/// <summary>
/// How a tab displays its document (SPECIFICATION.md 5.15).
/// Per tab, and Preview by default — this is a viewer first.
/// </summary>
public enum DocumentViewMode
{
    /// <summary>The rendered document. The default.</summary>
    Preview,

    /// <summary>
    /// The Markdown source, read-only, with line numbers. Not an editor:
    /// nothing here writes to the file (SPECIFICATION.md 1.2).
    /// </summary>
    Source

    // Split — source and preview side by side with synchronised scrolling — is
    // v2 (SPECIFICATION.md 9.2). It is deliberately absent rather than stubbed:
    // adding it means adding a member here and one branch in the shell, because
    // both views already position by source offset.
}
