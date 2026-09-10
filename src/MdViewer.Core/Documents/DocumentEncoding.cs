namespace MdViewer.Core.Documents;

/// <summary>
/// The encoding detected for a document, per SPECIFICATION.md 5.10.
/// Surfaced in the status bar and overridable per document.
/// </summary>
public enum DocumentEncoding
{
    Utf8,
    Utf8Bom,
    Utf16Le,
    Utf16Be,
    Utf32,
    SystemDefault,
    Unknown
}

public static class DocumentEncodingExtensions
{
    public static string ToDisplayName(this DocumentEncoding encoding) => encoding switch
    {
        DocumentEncoding.Utf8 => "UTF-8",
        DocumentEncoding.Utf8Bom => "UTF-8 BOM",
        DocumentEncoding.Utf16Le => "UTF-16 LE",
        DocumentEncoding.Utf16Be => "UTF-16 BE",
        DocumentEncoding.Utf32 => "UTF-32",
        DocumentEncoding.SystemDefault => "System",
        _ => "Unknown"
    };
}
