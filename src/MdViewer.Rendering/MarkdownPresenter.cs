using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MdViewer.Core.Documents;

namespace MdViewer.Rendering;

/// <summary>
/// Hosts one rendered document (SPECIFICATION.md 5.2).
///
/// Derives from <see cref="Decorator"/> rather than ContentControl because a
/// Decorator has a Child and no control template: there is no styling layer
/// between this and the document, which is what the rendering pipeline wants.
/// </summary>
public class MarkdownPresenter : Decorator
{
    public static readonly StyledProperty<LoadedDocument?> DocumentProperty =
        AvaloniaProperty.Register<MarkdownPresenter, LoadedDocument?>(nameof(Document));

    public static readonly StyledProperty<double> ContentMaxWidthProperty =
        AvaloniaProperty.Register<MarkdownPresenter, double>(nameof(ContentMaxWidth), 900d);

    public static readonly StyledProperty<double> ZoomFactorProperty =
        AvaloniaProperty.Register<MarkdownPresenter, double>(nameof(ZoomFactor), 1d);

    private readonly MarkdownRenderer _renderer = new();

    public MarkdownPresenter()
    {
        SpanRegistry = new SourceSpanRegistry();
    }

    /// <summary>The document to render. Setting it rebuilds the visual tree.</summary>
    public LoadedDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>Reading measure. Long lines hurt reading (SPECIFICATION.md 5.11).</summary>
    public double ContentMaxWidth
    {
        get => GetValue(ContentMaxWidthProperty);
        set => SetValue(ContentMaxWidthProperty, value);
    }

    /// <summary>Per-tab zoom factor (0.5-3.0).</summary>
    public double ZoomFactor
    {
        get => GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    /// <summary>
    /// Source spans for the currently rendered document. The shell uses this to
    /// scroll to a heading, restore a reading position by offset, and — from M5 —
    /// place find highlights.
    /// </summary>
    public SourceSpanRegistry SpanRegistry { get; private set; }

    /// <summary>Raised with the raw href of an activated link (SPECIFICATION.md 5.6).</summary>
    public Action<string>? LinkActivated { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomFactorProperty)
        {
            ApplyZoomResources();
            return;
        }

        if (change.Property == DocumentProperty || change.Property == ContentMaxWidthProperty)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// Scrolls so that the element containing <paramref name="sourceOffset"/> is
    /// at the top of the viewport. Positioning by source offset rather than by
    /// pixel is what lets a reload keep the reader's place when the document has
    /// changed above them (SPECIFICATION.md 5.10).
    /// </summary>
    public bool ScrollToSourceOffset(int sourceOffset)
    {
        var target = SpanRegistry.FindControlContaining(sourceOffset);
        if (target is null) return false;

        target.BringIntoView();
        return true;
    }

    /// <summary>
    /// The source offset of the last block that starts at or above
    /// <paramref name="verticalOffset"/> — that is, the block the reader is
    /// looking at. The caller supplies the offset because the scroll viewer
    /// belongs to the shell, not to the presenter.
    /// </summary>
    public int GetSourceOffsetAt(double verticalOffset)
    {
        var result = 0;

        foreach (var span in SpanRegistry.Ordered)
        {
            var position = span.Control.TranslatePoint(default, this);
            if (position is null) continue;

            if (position.Value.Y > verticalOffset) break;
            result = span.Start;
        }

        return result;
    }

    private void Rebuild()
    {
        ApplyZoomResources();
        SpanRegistry = new SourceSpanRegistry();

        var document = Document;
        if (document is null)
        {
            Child = null;
            return;
        }

        var context = new RenderContext(
            _renderer,
            document.BaseDirectory,
            SpanRegistry,
            url => LinkActivated?.Invoke(url),
            reducedMode: document.ByteLength > DocumentLoader.ReducedModeThresholdBytes);

        var body = _renderer.RenderDocument(document.Ast, context);
        body.MaxWidth = ContentMaxWidth;
        body.HorizontalAlignment = HorizontalAlignment.Center;

        Child = new Border
        {
            Padding = new Thickness(48, 36, 48, 80),
            Child = body
        };
    }

    private void ApplyZoomResources() => ZoomTypography.Apply(this, ZoomFactor);
}
