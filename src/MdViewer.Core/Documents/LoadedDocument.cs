using Markdig.Syntax;
using MdViewer.Core.Outline;

namespace MdViewer.Core.Documents;

/// <summary>
/// A parsed document (SPECIFICATION.md 5.2).
///
/// <see cref="SourceText"/> is the single source of truth. The AST carries
/// source spans into it, the outline carries source offsets into it, and
/// scroll positions and selections are expressed in it. That is what makes the
/// v2 editor an addition rather than a rewrite.
/// </summary>
public sealed class LoadedDocument
{
    public LoadedDocument(
        string path,
        string sourceText,
        MarkdownDocument ast,
        IReadOnlyList<OutlineNode> outline,
        DocumentEncoding encoding,
        LineEndingStyle lineEndings,
        int wordCount)
    {
        Path = path;
        SourceText = sourceText;
        Ast = ast;
        Outline = outline;
        Encoding = encoding;
        LineEndings = lineEndings;
        WordCount = wordCount;
    }

    /// <summary>Absolute path. Empty for a document that has never been saved.</summary>
    public string Path { get; }

    /// <summary>The directory links and images resolve against.</summary>
    public string BaseDirectory =>
        string.IsNullOrEmpty(Path) ? string.Empty : (System.IO.Path.GetDirectoryName(Path) ?? string.Empty);

    public string SourceText { get; }

    public MarkdownDocument Ast { get; }

    public IReadOnlyList<OutlineNode> Outline { get; }

    public DocumentEncoding Encoding { get; }

    public LineEndingStyle LineEndings { get; }

    public int WordCount { get; }

    public int ByteLength => SourceText.Length;
}

/// <summary>
/// The outcome of a load. A failure is a value, not an exception, because a
/// document that will not open must still get a tab with a readable
/// explanation rather than taking down the app (SPECIFICATION.md 6.4).
/// </summary>
public sealed class DocumentLoadResult
{
    private DocumentLoadResult(LoadedDocument? document, string? errorMessage, string path)
    {
        Document = document;
        ErrorMessage = errorMessage;
        Path = path;
    }

    public LoadedDocument? Document { get; }

    public string? ErrorMessage { get; }

    public string Path { get; }

    public bool IsSuccess => Document is not null;

    public static DocumentLoadResult Success(LoadedDocument document) =>
        new(document, null, document.Path);

    public static DocumentLoadResult Failure(string path, string message) =>
        new(null, message, path);
}
