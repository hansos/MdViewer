using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace MdViewer.Rendering.Inlines;

/// <summary>
/// A selectable text block that knows which character ranges are links
/// (SPECIFICATION.md 5.6).
///
/// Links are ordinary styled <c>Run</c>s rather than embedded controls, which
/// is what keeps text wrapping, justification and cross-run selection working
/// the way they should. The cost is that a Run has no pointer events of its
/// own, so the click is resolved here: hit-test the point to a character index,
/// then look that index up in the ranges recorded while the inlines were built.
/// </summary>
public class MarkdownTextBlock : SelectableTextBlock
{
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly List<LinkRange> _links = new();

    public Action<string>? LinkActivated { get; set; }

    public void AddLinkRange(int start, int length, string url)
    {
        if (length <= 0 || string.IsNullOrEmpty(url)) return;
        _links.Add(new LinkRange(start, length, url));
    }

    public bool HasLinks => _links.Count > 0;

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_links.Count == 0 || LinkActivated is null) return;
        if (e.InitialPressMouseButton != MouseButton.Left) return;

        // A drag that selected text is not a click.
        if (!string.IsNullOrEmpty(SelectedText)) return;

        var url = FindLinkAt(e.GetPosition(this));
        if (url is null) return;

        e.Handled = true;
        LinkActivated(url);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_links.Count == 0) return;

        Cursor = FindLinkAt(e.GetPosition(this)) is null ? null : HandCursor;
    }

    /// <summary>
    /// The one place that depends on Avalonia's text hit-testing API. If this
    /// ever needs adapting to a newer Avalonia, it is three lines and nothing
    /// else in the renderer is affected.
    /// </summary>
    private string? FindLinkAt(Point point)
    {
        var layout = TextLayout;
        if (layout is null) return null;

        TextHitTestResult hit;
        try
        {
            hit = layout.HitTestPoint(point);
        }
        catch (Exception)
        {
            return null;
        }

        if (!hit.IsInside) return null;

        var index = hit.TextPosition;
        foreach (var link in _links)
        {
            if (index >= link.Start && index < link.Start + link.Length)
            {
                return link.Url;
            }
        }

        return null;
    }

    private readonly record struct LinkRange(int Start, int Length, string Url);
}
