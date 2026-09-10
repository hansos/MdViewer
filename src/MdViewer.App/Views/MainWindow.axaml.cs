using Avalonia;
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
    private const double DefaultSidebarWidth = 260;

    private MainWindowViewModel? _boundModel;
    private double _lastSidebarWidth = DefaultSidebarWidth;

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

        var documentPane = this.FindControl<Panel>("DocumentPane");
        if (documentPane is not null)
        {
            documentPane.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnDocumentPointerWheelChanged,
                RoutingStrategies.Tunnel);
        }

        DataContextChanged += OnDataContextChanged;
    }

    private MainWindowViewModel? Model => DataContext as MainWindowViewModel;

    private MarkdownPresenter? PresenterControl => this.FindControl<MarkdownPresenter>("Presenter");

    private ScrollViewer? PreviewScroller => this.FindControl<ScrollViewer>("DocumentScroller");

    private SourceView? SourceViewControl => this.FindControl<SourceView>("Source");

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
            _boundModel.PropertyChanged -= OnModelPropertyChanged;
            _boundModel.CaptureScrollOffset = null;
            _boundModel.RestoreScrollOffset = null;
        }

        _boundModel = Model;

        if (_boundModel is not null)
        {
            _boundModel.ScrollToOffsetRequested += OnScrollToOffsetRequested;
            _boundModel.PropertyChanged += OnModelPropertyChanged;
            _boundModel.CaptureScrollOffset = CaptureScrollOffset;
            _boundModel.RestoreScrollOffset = RestoreScrollOffset;

            ApplySidebarVisibility(_boundModel.IsSidebarVisible);
        }
    }

    // =============================================================== sidebar

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsSidebarVisible) && _boundModel is not null)
        {
            ApplySidebarVisibility(_boundModel.IsSidebarVisible);
        }
    }

    /// <summary>
    /// A pixel-sized column does not collapse when its child is hidden the way
    /// an Auto column does, so hiding the sidebar has to zero the column — and
    /// its MinWidth with it, or the minimum would hold the gap open.
    /// The width the user dragged to is remembered and restored.
    /// </summary>
    private void ApplySidebarVisibility(bool visible)
    {
        var grid = this.FindControl<Grid>("BodyGrid");
        if (grid is null || grid.ColumnDefinitions.Count == 0) return;

        var column = grid.ColumnDefinitions[0];

        if (visible)
        {
            column.MinWidth = 180;
            column.MaxWidth = 640;
            column.Width = new GridLength(_lastSidebarWidth, GridUnitType.Pixel);
        }
        else
        {
            if (column.Width.IsAbsolute && column.Width.Value > 0)
            {
                _lastSidebarWidth = column.Width.Value;
            }

            column.MinWidth = 0;
            column.MaxWidth = 0;
            column.Width = new GridLength(0, GridUnitType.Pixel);
        }
    }

    // ================================================================== zoom

    private void OnDocumentPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var model = Model;
        if (model?.SelectedTab is null) return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        if (e.Delta.Y > 0)
        {
            if (model.ZoomInCommand.CanExecute(null)) model.ZoomInCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Delta.Y < 0)
        {
            if (model.ZoomOutCommand.CanExecute(null)) model.ZoomOutCommand.Execute(null);
            e.Handled = true;
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
            return SourceViewControl?.GetTopSourceOffset() ?? tab.ScrollOffset;
        }

        var presenter = PresenterControl;
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
            SourceViewControl?.ScrollToSourceOffset(sourceOffset);
            return;
        }

        PresenterControl?.ScrollToSourceOffset(sourceOffset);
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
