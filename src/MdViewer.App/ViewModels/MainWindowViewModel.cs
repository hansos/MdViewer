using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MdViewer.App.Services;
using MdViewer.Core.Documents;
using MdViewer.Core.Outline;
using MdViewer.Core.Session;
using MdViewer.Core.Workspace;

namespace MdViewer.App.ViewModels;

/// <summary>
/// The shell (SPECIFICATION.md 4). From M2 this drives real documents: files
/// are read and parsed off the UI thread and rendered by MdViewer.Rendering.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DocumentLoader _loader = new();
    private readonly WorkspaceScanner _scanner = new();
    private readonly RecentDocumentList _recent = new();

    private CancellationTokenSource? _loadCancellation;

    [ObservableProperty]
    private DocumentTabViewModel? _selectedTab;

    [ObservableProperty]
    private OutlineItemViewModel? _selectedOutlineItem;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private bool _isFindBarVisible;

    [ObservableProperty]
    private bool _isQuickOpenVisible;

    [ObservableProperty]
    private bool _isFocusMode;

    [ObservableProperty]
    private string _themeName = "System";

    [ObservableProperty]
    private string _workspaceName = "No folder";

    [ObservableProperty]
    private string? _statusMessage;

    public MainWindowViewModel()
    {
        Find = new FindViewModel();
        QuickOpen = new QuickOpenViewModel(Array.Empty<QuickOpenResultViewModel>(), Array.Empty<QuickOpenResultViewModel>());

        Tabs = new ObservableCollection<DocumentTabViewModel>();
        Tabs.CollectionChanged += OnTabsChanged;

        WorkspaceRoots = new ObservableCollection<FileTreeItemViewModel>();
        RecentDocuments = new ObservableCollection<RecentDocumentViewModel>();
    }

    /// <summary>Set by the view; the view models never touch Avalonia directly.</summary>
    public IStorageService? Storage { get; set; }

    /// <summary>
    /// Raised when the document pane should scroll to a source offset — from an
    /// outline click, an anchor link, or a restored reading position.
    /// </summary>
    public event Action<int>? ScrollToOffsetRequested;

    public ObservableCollection<DocumentTabViewModel> Tabs { get; }

    public ObservableCollection<FileTreeItemViewModel> WorkspaceRoots { get; }

    public ObservableCollection<RecentDocumentViewModel> RecentDocuments { get; }

    public FindViewModel Find { get; }

    public QuickOpenViewModel QuickOpen { get; }

    public string? WorkspaceRoot { get; private set; }

    public bool HasWorkspace => !string.IsNullOrEmpty(WorkspaceRoot);

    public bool HasNoTabs => Tabs.Count == 0;

    public bool HasRecentDocuments => RecentDocuments.Count > 0;

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(HasNoTabs));

    // ============================================================== startup

    /// <summary>
    /// Command-line arguments open as tabs; the first one's folder becomes the
    /// workspace (SPECIFICATION.md 4.3).
    /// </summary>
    public async Task InitializeAsync(IReadOnlyList<string> args)
    {
        var paths = args
            .Where(a => !a.StartsWith('-'))
            .Select(a => Path.GetFullPath(a))
            .Where(File.Exists)
            .ToList();

        if (paths.Count > 0)
        {
            var folder = Path.GetDirectoryName(paths[0]);
            if (!string.IsNullOrEmpty(folder)) SetWorkspace(folder);

            foreach (var path in paths)
            {
                await OpenDocumentAsync(path).ConfigureAwait(true);
            }
        }
    }

    // ============================================================ documents

    /// <summary>
    /// Opens a document, or focuses the tab that already has it. Reading and
    /// parsing happen on a thread-pool thread; only the tab update runs here.
    /// </summary>
    public async Task OpenDocumentAsync(string path, bool preview = false)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var full = Path.GetFullPath(path);

        var existing = Tabs.FirstOrDefault(t => PathsEqual(t.FullPath, full));
        if (existing is not null)
        {
            // A previewed tab that is opened again is promoted to permanent.
            if (!preview) existing.IsTransient = false;
            SelectedTab = existing;
            return;
        }

        var tab = CreateTab(full, preview);
        SelectedTab = tab;

        await LoadIntoAsync(tab, full).ConfigureAwait(true);
    }

    /// <summary>
    /// A preview tab takes the place of the previous preview tab rather than
    /// accumulating one tab per file glanced at in the tree
    /// (SPECIFICATION.md 5.9). FullPath is immutable, so the old preview is
    /// removed and the new one takes its position in the strip.
    /// </summary>
    private DocumentTabViewModel CreateTab(string full, bool preview)
    {
        var tab = new DocumentTabViewModel(Path.GetFileName(full), full) { IsTransient = preview };

        if (preview)
        {
            var existingPreview = Tabs.FirstOrDefault(t => t.IsTransient);
            if (existingPreview is not null)
            {
                var index = Tabs.IndexOf(existingPreview);
                Tabs.RemoveAt(index);
                Tabs.Insert(index, tab);
                return tab;
            }
        }

        Tabs.Add(tab);
        return tab;
    }

    private async Task LoadIntoAsync(DocumentTabViewModel tab, string path)
    {
        _loadCancellation?.Cancel();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;

        tab.IsLoading = true;
        StatusMessage = null;

        try
        {
            var result = await Task.Run(() => _loader.LoadAsync(path, token), token).ConfigureAwait(true);

            if (token.IsCancellationRequested) return;

            if (result.IsSuccess && result.Document is not null)
            {
                tab.SetDocument(result.Document);
                tab.Title = Path.GetFileName(path);
                RecordRecent(result.Document);
            }
            else
            {
                tab.SetError(result.ErrorMessage ?? "The document could not be opened.");
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load; the newer one owns the tab now.
        }
        catch (Exception ex)
        {
            tab.SetError($"Unexpected error: {ex.Message}");
        }
        finally
        {
            tab.IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (SelectedTab is null || string.IsNullOrEmpty(SelectedTab.FullPath)) return;
        await LoadIntoAsync(SelectedTab, SelectedTab.FullPath).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenFilesAsync()
    {
        if (Storage is null) return;

        var paths = await Storage.PickMarkdownFilesAsync().ConfigureAwait(true);
        foreach (var path in paths)
        {
            await OpenDocumentAsync(path).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (Storage is null) return;

        var folder = await Storage.PickFolderAsync().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(folder)) SetWorkspace(folder);
    }

    // ============================================================ workspace

    public void SetWorkspace(string folder)
    {
        if (!Directory.Exists(folder)) return;

        WorkspaceRoot = Path.GetFullPath(folder);
        WorkspaceName = new DirectoryInfo(WorkspaceRoot).Name;

        var root = _scanner.CreateRoot(WorkspaceRoot);
        _scanner.Load(root);

        WorkspaceRoots.Clear();
        WorkspaceRoots.Add(new FileTreeItemViewModel(root, _scanner) { IsExpanded = true });

        OnPropertyChanged(nameof(HasWorkspace));
        RefreshQuickOpenSources();
    }

    /// <summary>Single click previews, double click opens permanently.</summary>
    public async Task ActivateTreeItemAsync(FileTreeItemViewModel? item, bool permanent)
    {
        if (item is null) return;

        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
            return;
        }

        await OpenDocumentAsync(item.FullPath, preview: !permanent).ConfigureAwait(true);
    }

    // ====================================================== recent documents

    private void RecordRecent(LoadedDocument document)
    {
        _recent.Touch(new RecentDocument(
            document.Path,
            Path.GetFileName(document.Path),
            DateTimeOffset.Now,
            SelectedTab?.ScrollOffset ?? 0));

        RefreshRecent();
    }

    private void RefreshRecent()
    {
        var now = DateTimeOffset.Now;

        RecentDocuments.Clear();
        foreach (var entry in _recent.Items)
        {
            RecentDocuments.Add(new RecentDocumentViewModel(entry, now, File.Exists(entry.Path)));
        }

        OnPropertyChanged(nameof(HasRecentDocuments));
        RefreshQuickOpenSources();
    }

    private void RefreshQuickOpenSources()
    {
        var recent = RecentDocuments
            .Select(r => new QuickOpenResultViewModel(r.Title, r.FullPath, isRecent: true))
            .ToList();

        var workspace = HasWorkspace
            ? _scanner.EnumerateFiles(WorkspaceRoot!)
                .Select(f => new QuickOpenResultViewModel(f.Name, f.FullPath, isRecent: false))
                .ToList()
            : new List<QuickOpenResultViewModel>();

        QuickOpen.SetSources(recent, workspace);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(RecentDocumentViewModel? recent)
    {
        if (recent is null || !recent.Exists) return;
        await OpenDocumentAsync(recent.FullPath).ConfigureAwait(true);
    }

    // =================================================================== tabs

    [RelayCommand]
    private void CloseTab(DocumentTabViewModel? tab)
    {
        if (tab is null) return;

        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

        Tabs.Remove(tab);

        if (SelectedTab == tab)
        {
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        }
    }

    [RelayCommand]
    private void CloseAllTabs()
    {
        Tabs.Clear();
        SelectedTab = null;
    }

    // ============================================================ view mode

    /// <summary>
    /// Supplied by the view: reads the reading position out of whichever view
    /// is on screen, and puts it back into the one that replaces it. Both work
    /// in source offsets, which is the whole reason a mode switch can keep the
    /// reader's place at all (SPECIFICATION.md 5.15).
    /// </summary>
    public Func<int>? CaptureScrollOffset { get; set; }

    public Action<int>? RestoreScrollOffset { get; set; }

    private void SetViewMode(DocumentViewMode mode)
    {
        var tab = SelectedTab;
        if (tab is null || !tab.HasDocument || tab.ViewMode == mode) return;

        // Capture before the switch, restore after: the outgoing view still
        // knows where the reader was, and only it can say.
        tab.ScrollOffset = CaptureScrollOffset?.Invoke() ?? tab.ScrollOffset;
        tab.ViewMode = mode;
        RestoreScrollOffset?.Invoke(tab.ScrollOffset);
    }

    [RelayCommand]
    private void ShowPreview() => SetViewMode(DocumentViewMode.Preview);

    [RelayCommand]
    private void ShowSource() => SetViewMode(DocumentViewMode.Source);

    [RelayCommand]
    private void ToggleViewMode()
    {
        var tab = SelectedTab;
        if (tab is null) return;

        SetViewMode(tab.ViewMode == DocumentViewMode.Preview
            ? DocumentViewMode.Source
            : DocumentViewMode.Preview);
    }

    // ================================================================= links

    /// <summary>
    /// Handles an activated link (SPECIFICATION.md 5.6). Classification is here,
    /// in the shell, rather than in the renderer.
    /// </summary>
    public async Task ActivateLinkAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // In-document anchor
        if (url.StartsWith('#'))
        {
            var node = SelectedTab is null
                ? null
                : OutlineBuilder.FindByAnchor(SelectedTab.Document?.Outline ?? Array.Empty<OutlineNode>(), url);

            if (node is null)
            {
                StatusMessage = $"No heading matches {url}";
                return;
            }

            ScrollToOffsetRequested?.Invoke(node.SourceOffset);
            return;
        }

        // Absolute web and mail links go to the OS handler.
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) &&
            absolute.Scheme is "http" or "https" or "mailto")
        {
            OpenExternal(absolute.ToString());
            return;
        }

        // Relative path, possibly with an anchor.
        var baseDirectory = SelectedTab?.Document?.BaseDirectory;
        if (string.IsNullOrEmpty(baseDirectory))
        {
            StatusMessage = "Relative links need a saved document.";
            return;
        }

        var hashIndex = url.IndexOf('#');
        var relative = hashIndex >= 0 ? url[..hashIndex] : url;

        if (string.IsNullOrEmpty(relative))
        {
            StatusMessage = null;
            return;
        }

        var target = Path.GetFullPath(Path.Combine(baseDirectory, Uri.UnescapeDataString(relative)));

        if (!File.Exists(target))
        {
            StatusMessage = $"Not found: {relative}";
            return;
        }

        if (WorkspaceScanner.IsMarkdown(target))
        {
            await OpenDocumentAsync(target).ConfigureAwait(true);
            return;
        }

        // Non-Markdown local files open with the OS handler, but only after a
        // confirmation dialog (5.6). Until there is a dialog service, the link
        // is reported rather than acted on — silently shelling out to an
        // arbitrary file is exactly the behaviour that rule exists to prevent.
        StatusMessage = $"Opening non-Markdown files is not enabled yet: {Path.GetFileName(target)}";
    }

    private void OpenExternal(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open the link: {ex.Message}";
        }
    }

    partial void OnSelectedOutlineItemChanged(OutlineItemViewModel? value)
    {
        if (value is null) return;
        ScrollToOffsetRequested?.Invoke(value.SourceOffset);
    }

    // ================================================================ chrome

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarVisible = !IsSidebarVisible;

    [RelayCommand]
    private void ToggleFindBar()
    {
        IsFindBarVisible = !IsFindBarVisible;
        if (!IsFindBarVisible) Find.Query = string.Empty;
    }

    [RelayCommand]
    private void DismissOverlays()
    {
        if (IsQuickOpenVisible)
        {
            IsQuickOpenVisible = false;
            return;
        }

        if (IsFindBarVisible)
        {
            IsFindBarVisible = false;
            Find.Query = string.Empty;
        }
    }

    [RelayCommand]
    private void CloseFindBar()
    {
        IsFindBarVisible = false;
        Find.Query = string.Empty;
    }

    [RelayCommand]
    private void ToggleFocusMode()
    {
        IsFocusMode = !IsFocusMode;
        if (IsFocusMode) IsSidebarVisible = false;
    }

    // ============================================================ quick open

    [RelayCommand]
    private void ShowQuickOpen()
    {
        RefreshQuickOpenSources();
        QuickOpen.Reset();
        IsQuickOpenVisible = true;
    }

    [RelayCommand]
    private void CloseQuickOpen() => IsQuickOpenVisible = false;

    [RelayCommand]
    private async Task AcceptQuickOpenAsync()
    {
        var result = QuickOpen.SelectedResult;
        IsQuickOpenVisible = false;

        if (result is not null)
        {
            await OpenDocumentAsync(result.FullPath).ConfigureAwait(true);
        }
    }

    // ================================================================== zoom

    [RelayCommand]
    private void ZoomIn()
    {
        if (SelectedTab is null) return;
        SelectedTab.Zoom = Math.Min(3.0, Math.Round(SelectedTab.Zoom + 0.1, 2));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (SelectedTab is null) return;
        SelectedTab.Zoom = Math.Max(0.5, Math.Round(SelectedTab.Zoom - 0.1, 2));
    }

    [RelayCommand]
    private void ZoomReset()
    {
        if (SelectedTab is not null) SelectedTab.Zoom = 1.0;
    }

    // ================================================================= theme

    [RelayCommand]
    private void CycleTheme()
    {
        var app = Application.Current;
        if (app is null) return;

        if (app.RequestedThemeVariant == ThemeVariant.Light)
        {
            app.RequestedThemeVariant = ThemeVariant.Dark;
            ThemeName = "Dark";
        }
        else if (app.RequestedThemeVariant == ThemeVariant.Dark)
        {
            app.RequestedThemeVariant = ThemeVariant.Default;
            ThemeName = "System";
        }
        else
        {
            app.RequestedThemeVariant = ThemeVariant.Light;
            ThemeName = "Light";
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
}
