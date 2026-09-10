using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MdViewer.App.ViewModels;

/// <summary>
/// In-document find (SPECIFICATION.md 5.7). Find state is per tab and
/// persists while the tab is open.
/// </summary>
public partial class FindViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private bool _matchCase;

    [ObservableProperty]
    private bool _wholeWord;

    [ObservableProperty]
    private bool _useRegex;

    [ObservableProperty]
    private int _matchCount;

    [ObservableProperty]
    private int _currentMatch;

    /// <summary>Non-blocking feedback for an invalid regular expression.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    public string MatchSummary => MatchCount == 0
        ? (string.IsNullOrEmpty(Query) ? string.Empty : "No results")
        : $"{CurrentMatch} of {MatchCount}";

    partial void OnQueryChanged(string value)
    {
        // Placeholder counting so the bar can be evaluated with live feedback.
        // Real search runs on a background thread with debounce (M5).
        MatchCount = string.IsNullOrWhiteSpace(value) ? 0 : Math.Max(1, value.Length * 3 % 23);
        CurrentMatch = MatchCount == 0 ? 0 : 1;
        OnPropertyChanged(nameof(MatchSummary));
    }

    partial void OnMatchCountChanged(int value) => OnPropertyChanged(nameof(MatchSummary));

    partial void OnCurrentMatchChanged(int value) => OnPropertyChanged(nameof(MatchSummary));

    [RelayCommand]
    private void NextMatch()
    {
        if (MatchCount == 0) return;
        CurrentMatch = CurrentMatch >= MatchCount ? 1 : CurrentMatch + 1;
    }

    [RelayCommand]
    private void PreviousMatch()
    {
        if (MatchCount == 0) return;
        CurrentMatch = CurrentMatch <= 1 ? MatchCount : CurrentMatch - 1;
    }
}
