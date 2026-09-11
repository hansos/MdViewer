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
    private readonly Stack<int> _footnoteReturnOffsets = new();

    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>The parsed document, or null while loading or after a failure.</summary>
    [ObservableProperty]
    private LoadedDocument? _document;

    /// <summary>
    /// Rendered or raw (SPECIFICATION.md 5.15). Per tab, Preview by default.
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
    /// Set when the file has changed on disk; shown as a dot on the tab
    /// (SPECIFICATION.md 5.10).
    /// </summary>
    [ObservableProperty]
    private bool _isModifiedOnDisk;

    /// <summary>Per-tab zoom, 0.5 to 3.0 (SPECIFICATION.md 5.11).</summary>
    [ObservableProperty]
    private double _zoom = 1.0;

    /// <summary>
    /// Per-document consent for remote images (SPECIFICATION.md 5.6).
    /// Remote image placeholders can flip this to true via the shell.
    /// </summary>
    [ObservableProperty]
    private bool _allowRemoteImages;

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

    public bool HasFootnoteReturnTarget => _footnoteReturnOffsets.Count > 0;

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
        _footnoteReturnOffsets.Clear();

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
        _footnoteReturnOffsets.Clear();
        LineIndex = null;
        LineNumberColumn = string.Empty;
        LoadError = message;
        Outline.Clear();
        OnPropertyChanged(nameof(HasOutline));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(LineNumberColumn));
    }

    partial void OnZoomChanged(double value) => OnPropertyChanged(nameof(ZoomDisplay));

    public void PushFootnoteReturnOffset(int sourceOffset)
    {
        if (sourceOffset < 0) return;
        _footnoteReturnOffsets.Push(sourceOffset);
        OnPropertyChanged(nameof(HasFootnoteReturnTarget));
    }

    public bool TryPopFootnoteReturnOffset(out int sourceOffset)
    {
        var result = _footnoteReturnOffsets.TryPop(out sourceOffset);
        if (result)
        {
            OnPropertyChanged(nameof(HasFootnoteReturnTarget));
        }

        return result;
    }

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
