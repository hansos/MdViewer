namespace MdViewer.Core.Documents;

/// <summary>
/// Writes a document back to disk (SPECIFICATION.md 5.2).
///
/// The only edit MdViewer performs is toggling a task list marker, so the whole
/// job is to put the text back exactly as it came in: the encoding detected on
/// load, its byte order mark, and nothing else added.
/// </summary>
public static class DocumentWriter
{
    public static async Task WriteAsync(
        string path,
        string text,
        DocumentEncoding encoding,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(text);

        var bytes = Encode(text, encoding);

        // Written in place rather than through a temp file and rename: a rename
        // would drop the original file's ACLs and alternate streams, which is a
        // poor trade for a one-character change.
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    public static byte[] Encode(string text, DocumentEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bom = Bom(encoding);
        var payload = EncodingDetector.ToEncoding(encoding).GetBytes(text);

        if (bom.Length == 0) return payload;

        var buffer = new byte[bom.Length + payload.Length];
        bom.CopyTo(buffer, 0);
        payload.CopyTo(buffer, bom.Length);
        return buffer;
    }

    /// <summary>
    /// The byte order mark that <see cref="EncodingDetector"/> stripped on load.
    /// UTF-16 and UTF-32 are only ever detected by their BOM, so they always get
    /// one back; plain UTF-8 never does.
    /// </summary>
    private static byte[] Bom(DocumentEncoding encoding) => encoding switch
    {
        DocumentEncoding.Utf8Bom => [0xEF, 0xBB, 0xBF],
        DocumentEncoding.Utf16Le => [0xFF, 0xFE],
        DocumentEncoding.Utf16Be => [0xFE, 0xFF],
        DocumentEncoding.Utf32 => [0xFF, 0xFE, 0x00, 0x00],
        _ => [],
    };
}
