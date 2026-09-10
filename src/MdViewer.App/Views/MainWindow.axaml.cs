using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MdViewer.App.Services;
using MdViewer.App.ViewModels;
using MdViewer.Core.Documents;
using MdViewer.Rendering;

namespace MdViewer.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _boundModel;

    public MainWindow()
    {
        InitializeComponent();

        // The tree's activation gestures live here rather than in the view
        // model: they are input concerns, and the view model stays free of
        // Avalonia so it can be tested without a UI thread.
        var tree = this.FindControl<TreeView>("FileTree");
        if (tree is not null)
        {
            tree.SelectionChanged += OnTreeSelectionChanged;
            tree.DoubleTapped += OnTreeDoubleTapped;
        }

        var presenter = this.FindControl<MarkdownPresenter>("Presenter");
        if (presenter is not null)
        {
            presenter.LinkActivated = OnLinkActivated;
        }

        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? Model => DataContext as MainWindowViewModel;

    private MarkdownPresenter? Presenter => this.FindControl<MarkdownPresenter>("Presenter");

    private ScrollViewer? PreviewScroller => this.FindControl<ScrollViewer>("DocumentScroller");

    private SourceView? Source => this.FindControl<SourceView>("Source");

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (Model is null) return;

        Model.Storage = new StorageService(this);

        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        _ = Model.InitializeAsync(args);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundModel is not null)
        {
            _boundModel.ScrollToOffsetRequested -= OnScrollToOffsetRequested;
            _boundModel.CaptureScrollOffset = null;
            _boundModel.RestoreScrollOffset = null;
        }

        _boundModel = Model;

        if (_boundModel is not null)
        {
            _boundModel.ScrollToOffsetRequested += OnScrollToOffsetRequested;
            _boundModel.CaptureScrollOffset = CaptureScrollOffset;
            _boundModel.RestoreScrollOffset = RestoreScrollOffset;
        }
    }

    // ============================================================= view mode

    /// <summary>
    /// Reads the reading position out of whichever view is currently on screen.
    /// Both answer in source offsets, which is what makes the two modes
    /// interchangeable at all (SPECIFICATION.md 5.15).
    /// </summary>
    private int CaptureScrollOffset()
    {
        var tab = Model?.SelectedTab;
        if (tab is null) return 0;

        if (tab.ViewMode == DocumentViewMode.Source)
        {
            return Source?.GetTopSourceOffset() ?? tab.ScrollOffset;
        }

        var presenter = Presenter;
        var scroller = PreviewScroller;
        if (presenter is null || scroller is null) return tab.ScrollOffset;

        return presenter.GetSourceOffsetAt(scroller.Offset.Y);
    }

    /// <summary>
    /// Puts it back into the view that just became visible. Posted rather than
    /// called directly: the incoming view has not been measured yet at the
    /// moment the mode flips, so there is nothing to scroll to until layout runs.
    /// </summary>
    private void RestoreScrollOffset(int sourceOffset)
    {
        Dispatcher.UIThread.Post(
            () => OnScrollToOffsetRequested(sourceOffset),
            DispatcherPriority.Loaded);
    }

    private void OnScrollToOffsetRequested(int sourceOffset)
    {
        var tab = Model?.SelectedTab;

        if (tab?.ViewMode == DocumentViewMode.Source)
        {
            Source?.ScrollToSourceOffset(sourceOffset);
            return;
        }

        Presenter?.ScrollToSourceOffset(sourceOffset);
    }

    // ================================================================= links

    private void OnLinkActivated(string url)
    {
        if (Model is null) return;
        _ = Model.ActivateLinkAsync(url);
    }

    // ============================================================== file tree

    /// <summary>Single click previews (italic tab, replaced by the next preview).</summary>
    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Model is null) return;
        if (sender is not TreeView tree) return;
        if (tree.SelectedItem is not FileTreeItemViewModel item) return;
        if (item.IsDirectory) return;

        _ = Model.ActivateTreeItemAsync(item, permanent: false);
    }

    /// <summary>Double click promotes the preview tab to a permanent one.</summary>
    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Model is null) return;
        if (sender is not TreeView tree) return;
        if (tree.SelectedItem is not FileTreeItemViewModel item) return;

        _ = Model.ActivateTreeItemAsync(item, permanent: true);
    }
}
