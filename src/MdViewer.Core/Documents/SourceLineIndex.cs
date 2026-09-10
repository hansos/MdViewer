using System.Text;

namespace MdViewer.Core.Documents;

/// <summary>
/// Maps between source offsets and line numbers (SPECIFICATION.md 5.15).
///
/// Built once per document. The source view needs it for its line-number
/// gutter, and switching between preview and source needs it to keep the
/// reader's place, because both views position by source offset.
/// </summary>
public sealed class SourceLineIndex
{
    private readonly int[] _lineStarts;
    private readonly int _textLength;

    public SourceLineIndex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _textLength = text.Length;

        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
            else if (text[i] == '\r')
            {
                // Lone CR is a line break too; CRLF is one break, not two.
                if (i + 1 < text.Length && text[i + 1] == '\n') continue;
                starts.Add(i + 1);
            }
        }

        // A trailing newline does not open a line anyone can point at.
        if (starts.Count > 1 && starts[^1] == text.Length)
        {
            starts.RemoveAt(starts.Count - 1);
        }

        _lineStarts = starts.ToArray();
    }

    /// <summary>Number of lines. At least 1, even for empty text.</summary>
    public int LineCount => _lineStarts.Length;

    /// <summary>Source offset where a 0-based line starts.</summary>
    public int LineToOffset(int line)
    {
        if (line <= 0) return 0;
        if (line >= _lineStarts.Length) return _lineStarts[^1];
        return _lineStarts[line];
    }

    /// <summary>0-based line containing a source offset.</summary>
    public int OffsetToLine(int offset)
    {
        if (offset <= 0) return 0;
        if (offset >= _textLength) return _lineStarts.Length - 1;

        var index = Array.BinarySearch(_lineStarts, offset);
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>
    /// The gutter contents: "1\n2\n3…". Built as one string rather than one
    /// control per line, because a control per line would cost more than the
    /// document itself on anything long.
    /// </summary>
    public string BuildLineNumberColumn()
    {
        var builder = new StringBuilder(LineCount * 4);

        for (var i = 1; i <= LineCount; i++)
        {
            if (i > 1) builder.Append('\n');
            builder.Append(i);
        }

        return builder.ToString();
    }
}
