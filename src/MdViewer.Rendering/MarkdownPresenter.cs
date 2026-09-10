using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
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

    public static readonly StyledProperty<bool> AllowRemoteImagesProperty =
        AvaloniaProperty.Register<MarkdownPresenter, bool>(nameof(AllowRemoteImages), false);

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
    /// When true, remote images are fetched for the current document;
    /// otherwise placeholders are shown (SPECIFICATION.md 5.6).
    /// </summary>
    public bool AllowRemoteImages
    {
        get => GetValue(AllowRemoteImagesProperty);
        set => SetValue(AllowRemoteImagesProperty, value);
    }

    /// <summary>
    /// Source spans for the currently rendered document. The shell uses this to
    /// scroll to a heading, restore a reading position by offset, and — from M5 —
    /// place find highlights.
    /// </summary>
    public SourceSpanRegistry SpanRegistry { get; private set; }

    /// <summary>Raised with the raw href of an activated link (SPECIFICATION.md 5.6).</summary>
    public Action<string>? LinkActivated { get; set; }

    /// <summary>
    /// Raised when a remote-image placeholder requests per-document consent to
    /// load images.
    /// </summary>
    public Action? RemoteImagesRequested { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomFactorProperty)
        {
            ApplyZoomResources();
            return;
        }

        if (change.Property == DocumentProperty
            || change.Property == ContentMaxWidthProperty
            || change.Property == AllowRemoteImagesProperty)
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

        // BringIntoView() scrolls the minimum amount needed, so it does nothing
        // when the target already happens to be on screen and otherwise parks it
        // at a viewport edge. The reading position needs the block at the top,
        // which means translating its Y into the scroll content and setting the
        // offset outright.
        var scroller = this.FindAncestorOfType<ScrollViewer>();
        if (scroller?.Content is Control content)
        {
            var position = target.TranslatePoint(default, content);
            if (position is not null)
            {
                scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, position.Value.Y));
                return true;
            }
        }

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
        var bestY = double.NegativeInfinity;

        // Ordered is sorted by source offset, but Y is not monotonic in source
        // order: nested registrations (table cells, list items, block-quote
        // children) can start above their parent's successor. Breaking on the
        // first control past the viewport would stop the walk early, so every
        // span is considered and the lowest one still at or above the fold wins.
        foreach (var span in SpanRegistry.Ordered)
        {
            var position = span.Control.TranslatePoint(default, this);
            if (position is null) continue;

            if (position.Value.Y > verticalOffset + 0.5) continue;
            if (position.Value.Y < bestY) continue;

            bestY = position.Value.Y;
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
            allowRemoteImages: AllowRemoteImages,
            onLoadRemoteImagesRequested: () => RemoteImagesRequested?.Invoke(),
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
