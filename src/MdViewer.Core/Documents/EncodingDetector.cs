using System.Text;

namespace MdViewer.Core.Documents;

/// <summary>
/// Detects a document's encoding (SPECIFICATION.md 5.10), in order:
/// BOM, then a UTF-8 validity scan, then the system default.
///
/// Pure and synchronous over a byte buffer so it is testable without touching
/// the file system.
/// </summary>
public static class EncodingDetector
{
    /// <summary>
    /// How many bytes to scan for UTF-8 validity before giving up and accepting
    /// the text as UTF-8. Scanning a whole 50 MB file to answer a question the
    /// first few KB almost always settle is not worth the latency.
    /// </summary>
    public const int ScanLimit = 64 * 1024;

    public static DetectionResult Detect(ReadOnlySpan<byte> bytes)
    {
        // 1. BOM. Check UTF-32 before UTF-16: the UTF-32 LE BOM starts with the
        //    UTF-16 LE BOM, so the shorter test would swallow it.
        if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00)) return new DetectionResult(DocumentEncoding.Utf32, 4);
        if (StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF)) return new DetectionResult(DocumentEncoding.Utf32, 4);
        if (StartsWith(bytes, 0xEF, 0xBB, 0xBF)) return new DetectionResult(DocumentEncoding.Utf8Bom, 3);
        if (StartsWith(bytes, 0xFF, 0xFE)) return new DetectionResult(DocumentEncoding.Utf16Le, 2);
        if (StartsWith(bytes, 0xFE, 0xFF)) return new DetectionResult(DocumentEncoding.Utf16Be, 2);

        // 2. Valid UTF-8 without a BOM is by far the common case for Markdown.
        if (IsLikelyUtf8(bytes.Length > ScanLimit ? bytes[..ScanLimit] : bytes))
        {
            return new DetectionResult(DocumentEncoding.Utf8, 0);
        }

        // 3. Last resort. On Linux that is still UTF-8; on Windows it is the
        //    ANSI code page, which is the only place legacy Markdown files
        //    realistically show up.
        return new DetectionResult(DocumentEncoding.SystemDefault, 0);
    }

    /// <summary>Maps a detection result to the <see cref="Encoding"/> used to decode.</summary>
    public static Encoding ToEncoding(DocumentEncoding encoding) => encoding switch
    {
        DocumentEncoding.Utf8 or DocumentEncoding.Utf8Bom => new UTF8Encoding(false),
        DocumentEncoding.Utf16Le => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
        DocumentEncoding.Utf16Be => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
        DocumentEncoding.Utf32 => new UTF32Encoding(bigEndian: false, byteOrderMark: false),
        _ => SystemDefault()
    };

    private static Encoding SystemDefault()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new UTF8Encoding(false);
        }

        try
        {
            // Registered by CodePagesEncodingProvider when available; falls back
            // to Latin-1, which at least never throws on arbitrary bytes.
            return Encoding.GetEncoding(0);
        }
        catch (ArgumentException)
        {
            return Encoding.Latin1;
        }
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length) return false;

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i]) return false;
        }

        return true;
    }

    /// <summary>
    /// Structural UTF-8 validation. A truncated multi-byte sequence at the very
    /// end of the scanned window is accepted, because the window boundary is
    /// arbitrary and cutting a character in half is not evidence of anything.
    /// </summary>
    private static bool IsLikelyUtf8(ReadOnlySpan<byte> bytes)
    {
        var i = 0;
        while (i < bytes.Length)
        {
            var b = bytes[i];

            if (b <= 0x7F)
            {
                i++;
                continue;
            }

            int following;
            if ((b & 0xE0) == 0xC0) following = 1;
            else if ((b & 0xF0) == 0xE0) following = 2;
            else if ((b & 0xF8) == 0xF0) following = 3;
            else return false;                       // 0x80-0xBF lead, or 0xF8+

            if (i + following >= bytes.Length) return true;   // truncated at the window edge

            for (var k = 1; k <= following; k++)
            {
                if ((bytes[i + k] & 0xC0) != 0x80) return false;
            }

            i += following + 1;
        }

        return true;
    }

    public readonly record struct DetectionResult(DocumentEncoding Encoding, int BomLength);
}
