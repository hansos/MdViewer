using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml.MarkupExtensions;
using MdViewer.App.ViewModels;
using MdViewer.Core.Documents;
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
    private MainWindowViewModel? _boundModel;

    public static readonly StyledProperty<double> ZoomFactorProperty =
        AvaloniaProperty.Register<SourceView, double>(nameof(ZoomFactor), 1d);

    public SourceView()
    {
        InitializeComponent();

        var scroller = this.FindControl<ScrollViewer>("Scroller");
        if (scroller is not null)
        {
            scroller.ScrollChanged += (_, _) =>
            {
                SyncGutter(scroller.Offset.Y);
                Scrolled?.Invoke();
            };
        }

        ApplyZoomResources();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Raised whenever the reading position in the source changes.</summary>
    public event Action? Scrolled;

    public double ZoomFactor
    {
        get => GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    private ScrollViewer? SourceScroller => this.FindControl<ScrollViewer>("Scroller");

    private ScrollViewer? GutterScrollHost => this.FindControl<ScrollViewer>("GutterScroller");

    private SelectableTextBlock? Body => this.FindControl<SelectableTextBlock>("SourceText");

    /// <summary>
    /// Line starts of the document currently on screen. Positions are mapped
    /// through line numbers rather than through text-layout character indices:
    /// the source text keeps the file's original endings, and a CRLF counts as
    /// two characters to Markdig but as one break to the layout, so indexing by
    /// character drifts one place per line as the reader goes down a document.
    /// </summary>
    private SourceLineIndex? LineIndex =>
        (DataContext as MainWindowViewModel)?.SelectedTab?.LineIndex;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundModel is not null)
        {
            _boundModel.PropertyChanged -= OnModelPropertyChanged;
            _boundModel.Find.PropertyChanged -= OnFindPropertyChanged;
        }

        _boundModel = DataContext as MainWindowViewModel;
        if (_boundModel is null) return;

        _boundModel.PropertyChanged += OnModelPropertyChanged;
        _boundModel.Find.PropertyChanged += OnFindPropertyChanged;
        RefreshSourceTextAndHighlights();
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTab))
        {
            RefreshSourceTextAndHighlights();
        }
    }

    private void OnFindPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FindViewModel.Matches))
        {
            RefreshSourceTextAndHighlights();
        }
    }

    /// <summary>Keeps the gutter aligned with the document it numbers.</summary>
    private void SyncGutter(double verticalOffset)
    {
        var gutter = GutterScrollHost;
        if (gutter is null) return;

        gutter.Offset = new Vector(0, verticalOffset);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomFactorProperty)
        {
            ApplyZoomResources();
        }
        else if (change.Property == IsVisibleProperty && IsVisible)
        {
            RefreshSourceTextAndHighlights();
        }
    }

    private void RefreshSourceTextAndHighlights()
    {
        var body = Body;
        if (body is null) return;

        var model = _boundModel;
        var tab = model?.SelectedTab;
        var text = tab?.SourceText ?? string.Empty;

        if (tab?.ShowsSource != true)
        {
            body.Inlines = null;
            body.Text = text;
            return;
        }

        var matches = model?.Find.Matches;
        if (matches is null || matches.Count == 0)
        {
            body.Inlines = null;
            body.Text = text;
            return;
        }

        body.Text = string.Empty;

        var inlines = EnsureInlines(body);
        inlines.Clear();

        var cursor = 0;
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var start = Math.Clamp(match.Start, 0, text.Length);
            var end = Math.Clamp(match.Start + match.Length, start, text.Length);

            if (start > cursor)
            {
                inlines.Add(new Run(text.Substring(cursor, start - cursor)));
            }

            if (end > start)
            {
                var run = new Run(text.Substring(start, end - start));
                ApplyFindHighlight(run);

                inlines.Add(run);
            }

            cursor = Math.Max(cursor, end);
        }

        if (cursor < text.Length)
        {
            inlines.Add(new Run(text.Substring(cursor, text.Length - cursor)));
        }
    }

    private static InlineCollection EnsureInlines(SelectableTextBlock target)
    {
        var inlines = target.Inlines;
        if (inlines is null)
        {
            inlines = new InlineCollection();
            target.Inlines = inlines;
        }

        return inlines;
    }

    private static void ApplyFindHighlight(Run run)
    {
        run.Bind(
            TextElement.BackgroundProperty,
            new DynamicResourceExtension("FindMatchBrush"));
    }

    /// <summary>
    /// The source offset of the first character visible at the top of the
    /// viewport. Used to hand the reading position to the preview.
    /// </summary>
    public int GetTopSourceOffset()
    {
        var scroller = SourceScroller;
        var body = Body;
        var index = LineIndex;
        if (scroller is null || body is null || index is null) return 0;

        var lines = body.TextLayout.TextLines;
        if (lines.Count == 0) return 0;

        var target = scroller.Offset.Y;
        var y = 0d;
        var line = lines.Count - 1;

        for (var i = 0; i < lines.Count; i++)
        {
            var height = lines[i].Height;
            if (y + height > target + 0.5)
            {
                line = i;
                break;
            }

            y += height;
        }

        return index.LineToOffset(line);
    }

    /// <summary>Scrolls so the line containing the offset is at the top.</summary>
    public bool ScrollToSourceOffset(int sourceOffset)
    {
        var scroller = SourceScroller;
        var body = Body;
        var index = LineIndex;
        if (scroller is null || body is null || index is null) return false;

        var lines = body.TextLayout.TextLines;
        if (lines.Count == 0) return false;

        var line = Math.Min(index.OffsetToLine(Math.Max(0, sourceOffset)), lines.Count - 1);

        var y = 0d;
        for (var i = 0; i < line; i++)
        {
            y += lines[i].Height;
        }

        scroller.Offset = new Vector(scroller.Offset.X, Math.Max(0, y));
        SyncGutter(scroller.Offset.Y);
        return true;
    }

    private void ApplyZoomResources() => ZoomTypography.Apply(this, ZoomFactor);
}
