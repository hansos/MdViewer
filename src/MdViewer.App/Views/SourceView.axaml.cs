using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MdViewer.Rendering;

namespace MdViewer.App.Views;

/// <summary>
/// The raw source view (SPECIFICATION.md 5.15).
///
/// Read-only: this is the viewer's other half, not an editor. It positions by
/// source offset like the preview does, which is what lets a mode switch keep
/// the reader's place — and is the same machinery the v2 split editor will use.
/// </summary>
public partial class SourceView : UserControl
{
    public static readonly StyledProperty<double> ZoomFactorProperty =
        AvaloniaProperty.Register<SourceView, double>(nameof(ZoomFactor), 1d);

    private readonly TranslateTransform _gutterOffset = new();

    public SourceView()
    {
        InitializeComponent();

        var gutter = this.FindControl<TextBlock>("Gutter");
        if (gutter is not null)
        {
            gutter.RenderTransform = _gutterOffset;
        }

        var scroller = this.FindControl<ScrollViewer>("Scroller");
        if (scroller is not null)
        {
            scroller.ScrollChanged += (_, _) => _gutterOffset.Y = -scroller.Offset.Y;
        }

        ApplyZoomResources();
    }

    public double ZoomFactor
    {
        get => GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    private ScrollViewer? SourceScroller => this.FindControl<ScrollViewer>("Scroller");

    private SelectableTextBlock? Body => this.FindControl<SelectableTextBlock>("SourceText");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomFactorProperty)
        {
            ApplyZoomResources();
        }
    }

    /// <summary>
    /// The source offset of the first character visible at the top of the
    /// viewport. Used to hand the reading position to the preview.
    /// </summary>
    public int GetTopSourceOffset()
    {
        var scroller = SourceScroller;
        var body = Body;
        if (scroller is null || body is null) return 0;

        try
        {
            var hit = body.TextLayout.HitTestPoint(new Point(0, scroller.Offset.Y));
            return hit.TextPosition;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Scrolls so the line containing the offset is at the top.</summary>
    public bool ScrollToSourceOffset(int sourceOffset)
    {
        var scroller = SourceScroller;
        var body = Body;
        if (scroller is null || body is null) return false;

        try
        {
            var rect = body.TextLayout.HitTestTextPosition(Math.Max(0, sourceOffset));
            scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, rect.Y));
            _gutterOffset.Y = -scroller.Offset.Y;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void ApplyZoomResources() => ZoomTypography.Apply(this, ZoomFactor);
}
