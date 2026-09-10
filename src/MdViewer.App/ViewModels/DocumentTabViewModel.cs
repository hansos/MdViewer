using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MdViewer.Core.Documents;

namespace MdViewer.App.ViewModels;

/// <summary>
/// One open document tab (SPECIFICATION.md 4.2).
/// Each tab owns its own document, view mode, outline, scroll position,
/// zoom and find state.
/// </summary>
public partial class DocumentTabViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>The parsed document, or null while loading or after a failure.</summary>
    [ObservableProperty]
    private LoadedDocument? _document;

    /// <summary>
    /// Rendered or raw (SPECIFICATION.md 5.15). Per tab, Preview by default.
    ///
    /// Not to be confused with <see cref="IsTransient"/>, which is about the
    /// tab, not the document.
    /// </summary>
    [ObservableProperty]
    private DocumentViewMode _viewMode = DocumentViewMode.Preview;

    /// <summary>
    /// Set when the file could not be read or parsed. A failure is a value, not
    /// an exception: the tab stays open and explains itself (SPECIFICATION.md 6.4).
    /// </summary>
    [ObservableProperty]
    private string? _loadError;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Set when the file has changed on disk and the app is in "notify only"
    /// mode; shown as a dot on the tab (SPECIFICATION.md 5.10).
    /// </summary>
    [ObservableProperty]
    private bool _isModifiedOnDisk;

    /// <summary>
    /// A tab opened by a single click in the file tree, shown in italics and
    /// replaced by the next such click until promoted (SPECIFICATION.md 5.9).
    /// </summary>
    [ObservableProperty]
    private bool _isTransient;

    /// <summary>Per-tab zoom, 0.5 to 3.0 (SPECIFICATION.md 5.11).</summary>
    [ObservableProperty]
    private double _zoom = 1.0;

    /// <summary>
    /// Reading position, as a source offset rather than a pixel offset. That is
    /// what lets a reload keep the reader's place when the document changed
    /// above them, and what carries the position across a view-mode switch
    /// (SPECIFICATION.md 5.10, 5.15).
    /// </summary>
    [ObservableProperty]
    private int _scrollOffset;

    public DocumentTabViewModel(string title, string fullPath)
    {
        Title = title;
        FullPath = fullPath;
        Outline = new ObservableCollection<OutlineItemViewModel>();
    }

    public string FullPath { get; }

    public ObservableCollection<OutlineItemViewModel> Outline { get; }

    /// <summary>Line starts for the source view's gutter and offset mapping.</summary>
    public SourceLineIndex? LineIndex { get; private set; }

    public string SourceText => Document?.SourceText ?? string.Empty;

    public string LineNumberColumn { get; private set; } = string.Empty;

    public bool HasDocument => Document is not null;

    public bool HasError => !string.IsNullOrEmpty(LoadError);

    public bool IsEmpty => !HasDocument && !HasError && !IsLoading;

    public bool HasOutline => Outline.Count > 0;

    public bool IsPreviewMode => ViewMode == DocumentViewMode.Preview;

    public bool IsSourceMode => ViewMode == DocumentViewMode.Source;

    /// <summary>Preview is only shown when there is something to render.</summary>
    public bool ShowsPreview => HasDocument && IsPreviewMode;

    public bool ShowsSource => HasDocument && IsSourceMode;

    public int WordCount => Document?.WordCount ?? 0;

    public string EncodingDisplay => Document?.Encoding.ToDisplayName() ?? "—";

    public string LineEndingDisplay => Document?.LineEndings.ToDisplayName() ?? "—";

    public string ZoomDisplay => $"{Zoom * 100:0}%";

    /// <summary>Replaces the tab's content after a load or a reload.</summary>
    public void SetDocument(LoadedDocument document)
    {
        LoadError = null;
        Document = document;

        LineIndex = new SourceLineIndex(document.SourceText);
        LineNumberColumn = LineIndex.BuildLineNumberColumn();

        Outline.Clear();
        foreach (var node in document.Outline)
        {
            Outline.Add(new OutlineItemViewModel(node));
        }

        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(LineNumberColumn));
    }

    public void SetError(string message)
    {
        Document = null;
        LineIndex = null;
        LineNumberColumn = string.Empty;
        LoadError = message;
        Outline.Clear();
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(LineNumberColumn));
    }

    partial void OnZoomChanged(double value) => OnPropertyChanged(nameof(ZoomDisplay));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnViewModeChanged(DocumentViewMode value)
    {
        OnPropertyChanged(nameof(IsPreviewMode));
        OnPropertyChanged(nameof(IsSourceMode));
        OnPropertyChanged(nameof(ShowsPreview));
        OnPropertyChanged(nameof(ShowsSource));
    }

    partial void OnLoadErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnDocumentChanged(LoadedDocument? value)
    {
        OnPropertyChanged(nameof(HasDocument));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ShowsPreview));
        OnPropertyChanged(nameof(ShowsSource));
        OnPropertyChanged(nameof(WordCount));
        OnPropertyChanged(nameof(EncodingDisplay));
        OnPropertyChanged(nameof(LineEndingDisplay));
    }
}
