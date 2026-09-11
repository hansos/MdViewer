using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MdViewer.Core.Search;

namespace MdViewer.App.ViewModels;

/// <summary>
/// Quick-open (Ctrl+P), SPECIFICATION.md 5.9 and 5.14.
///
/// With an empty query the list IS the recent documents, most recent first.
/// That is the second place recency earns its keep, and it costs no permanent
/// chrome: the palette is only on screen while you are using it.
/// </summary>
public partial class QuickOpenViewModel : ViewModelBase
{
    private IReadOnlyList<QuickOpenResultViewModel> _recent;
    private IReadOnlyList<QuickOpenResultViewModel> _workspace;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private QuickOpenResultViewModel? _selectedResult;

    public QuickOpenViewModel(
        IReadOnlyList<QuickOpenResultViewModel> recent,
        IReadOnlyList<QuickOpenResultViewModel> workspace)
    {
        _recent = recent;
        _workspace = workspace;
        Results = new ObservableCollection<QuickOpenResultViewModel>();
        Refresh();
    }

    public ObservableCollection<QuickOpenResultViewModel> Results { get; }

    /// <summary>
    /// Replaces both sources. Called when a document is opened (the recent list
    /// changed) and when the workspace root changes (the index changed).
    /// </summary>
    public void SetSources(
        IReadOnlyList<QuickOpenResultViewModel> recent,
        IReadOnlyList<QuickOpenResultViewModel> workspace)
    {
        _recent = recent;
        _workspace = workspace;
        Refresh();
    }

    public bool IsShowingRecent => string.IsNullOrWhiteSpace(Query);

    public string HeaderText => IsShowingRecent ? "RECENT" : $"{Results.Count} MATCHES";

    public bool HasResults => Results.Count > 0;

    partial void OnQueryChanged(string value) => Refresh();

    /// <summary>
    /// Resets to the recent list. Called when the palette opens, so Ctrl+P
    /// always starts from the same place rather than the last search.
    /// </summary>
    public void Reset()
    {
        Query = string.Empty;
        Refresh();
    }

    public void SelectPreviousResult() => MoveSelection(-1);

    public void SelectNextResult() => MoveSelection(1);

    private void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            SelectedResult = null;
            return;
        }

        if (SelectedResult is null)
        {
            SelectedResult = delta < 0 ? Results[^1] : Results[0];
            return;
        }

        var index = Results.IndexOf(SelectedResult);
        if (index < 0)
        {
            SelectedResult = delta < 0 ? Results[^1] : Results[0];
            return;
        }

        var nextIndex = (index + delta + Results.Count) % Results.Count;
        SelectedResult = Results[nextIndex];
    }

    private void Refresh()
    {
        Results.Clear();

        if (IsShowingRecent)
        {
            foreach (var item in _recent)
            {
                Results.Add(item);
            }
        }
        else
        {
            var query = Query.Trim();

            // Recent entries are ranked ahead of the rest of the workspace at
            // equal score, so a file you were just in stays near the top.
            var ranked = _recent.Select(r => (Item: r, Score: FuzzyMatcher.Score(r.Title, query) * 2))
                .Concat(_workspace.Select(w => (Item: w, Score: FuzzyMatcher.Score(w.Title, query))))
                .Where(x => x.Score > 0)
                .GroupBy(x => x.Item.FullPath)
                .Select(g => g.OrderByDescending(x => x.Score).First())
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.Title, StringComparer.OrdinalIgnoreCase)
                .Take(30);

            foreach (var entry in ranked)
            {
                Results.Add(entry.Item);
            }
        }

        SelectedResult = Results.FirstOrDefault();
        OnPropertyChanged(nameof(IsShowingRecent));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(HasResults));
    }
}

/// <summary>One row in the quick-open palette.</summary>
public sealed class QuickOpenResultViewModel : ViewModelBase
{
    public QuickOpenResultViewModel(string title, string fullPath, bool isRecent)
    {
        Title = title;
        FullPath = fullPath;
        IsRecent = isRecent;
    }

    public string Title { get; }

    public string FullPath { get; }

    /// <summary>Drives the small "recent" marker on the row.</summary>
    public bool IsRecent { get; }

    public string Directory
    {
        get
        {
            var slash = FullPath.LastIndexOfAny(new[] { '\\', '/' });
            return slash <= 0 ? FullPath : FullPath[..slash];
        }
    }
}
