namespace MdViewer.Core.Documents;

/// <summary>
/// The line endings a document arrived with (SPECIFICATION.md 5.10).
/// Recorded so that the v2 editor writes the file back the way it found it.
/// </summary>
public enum LineEndingStyle
{
    /// <summary>No line break in the file at all.</summary>
    None,
    Lf,
    CrLf,
    Cr,

    /// <summary>More than one style present. Preserved as-is; never normalised on save.</summary>
    Mixed
}

public static class LineEndings
{
    public static LineEndingStyle Detect(ReadOnlySpan<char> text)
    {
        var lf = false;
        var crlf = false;
        var cr = false;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf = true;
                    i++;
                }
                else
                {
                    cr = true;
                }
            }
            else if (text[i] == '\n')
            {
                lf = true;
            }
        }

        var count = (lf ? 1 : 0) + (crlf ? 1 : 0) + (cr ? 1 : 0);
        if (count == 0) return LineEndingStyle.None;
        if (count > 1) return LineEndingStyle.Mixed;
        if (crlf) return LineEndingStyle.CrLf;
        return lf ? LineEndingStyle.Lf : LineEndingStyle.Cr;
    }

    public static string ToDisplayName(this LineEndingStyle style) => style switch
    {
        LineEndingStyle.Lf => "LF",
        LineEndingStyle.CrLf => "CRLF",
        LineEndingStyle.Cr => "CR",
        LineEndingStyle.Mixed => "Mixed",
        _ => "—"
    };
}
