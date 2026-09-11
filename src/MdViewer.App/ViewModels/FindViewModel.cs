using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MdViewer.Rendering;
using System.Text.RegularExpressions;

namespace MdViewer.App.ViewModels;

/// <summary>
/// In-document find (SPECIFICATION.md 5.7). Find state is per tab and
/// persists while the tab is open.
/// </summary>
public partial class FindViewModel : ViewModelBase
{
    private const int QueryDebounceMilliseconds = 300;

    private readonly List<FindMatchOccurrence> _matchOffsets = [];
    private IReadOnlyList<FindMatchOccurrence> _matches = Array.Empty<FindMatchOccurrence>();
    private string _searchText = string.Empty;
    private CancellationTokenSource? _queryDebounce;

    public event Action<int>? MatchSelected;

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

    public IReadOnlyList<FindMatchOccurrence> Matches
    {
        get => _matches;
        private set => SetProperty(ref _matches, value);
    }

    /// <summary>Non-blocking feedback for an invalid regular expression.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    public string MatchSummary => MatchCount == 0
        ? (string.IsNullOrEmpty(Query) ? string.Empty : "No results")
        : $"{CurrentMatch} of {MatchCount}";

    public void SetSearchText(string text)
    {
        _searchText = text ?? string.Empty;
        CancelPendingDebounce();
        RecalculateMatches();
    }

    partial void OnQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            CancelPendingDebounce();
            RecalculateMatches();
            return;
        }

        _ = DebounceQueryRecalculationAsync();
    }

    partial void OnMatchCaseChanged(bool value)
    {
        CancelPendingDebounce();
        RecalculateMatches();
    }

    partial void OnWholeWordChanged(bool value)
    {
        CancelPendingDebounce();
        RecalculateMatches();
    }

    partial void OnUseRegexChanged(bool value)
    {
        CancelPendingDebounce();
        RecalculateMatches();
    }

    partial void OnMatchCountChanged(int value) => OnPropertyChanged(nameof(MatchSummary));

    partial void OnCurrentMatchChanged(int value) => OnPropertyChanged(nameof(MatchSummary));

    [RelayCommand]
    private void NextMatch()
    {
        if (MatchCount == 0) return;
        CurrentMatch = CurrentMatch >= MatchCount ? 1 : CurrentMatch + 1;
        RaiseMatchSelected();
    }

    [RelayCommand]
    private void PreviousMatch()
    {
        if (MatchCount == 0) return;
        CurrentMatch = CurrentMatch <= 1 ? MatchCount : CurrentMatch - 1;
        RaiseMatchSelected();
    }

    public void SelectCurrentMatch() => RaiseMatchSelected();

    private async Task DebounceQueryRecalculationAsync()
    {
        _queryDebounce?.Cancel();
        _queryDebounce?.Dispose();

        var debounce = new CancellationTokenSource();
        _queryDebounce = debounce;

        try
        {
            await Task.Delay(QueryDebounceMilliseconds, debounce.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_queryDebounce, debounce)) return;

        RecalculateMatches();
    }

    private void CancelPendingDebounce()
    {
        _queryDebounce?.Cancel();
        _queryDebounce?.Dispose();
        _queryDebounce = null;
    }

    private void RecalculateMatches()
    {
        _matchOffsets.Clear();
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Query) || string.IsNullOrEmpty(_searchText))
        {
            Matches = Array.Empty<FindMatchOccurrence>();
            MatchCount = 0;
            CurrentMatch = 0;
            return;
        }

        if (UseRegex)
        {
            try
            {
                CollectRegexMatches();
            }
            catch (ArgumentException ex)
            {
                Matches = Array.Empty<FindMatchOccurrence>();
                MatchCount = 0;
                CurrentMatch = 0;
                ErrorMessage = ex.Message;
                return;
            }
        }
        else
        {
            CollectLiteralMatches();
        }

        Matches = _matchOffsets.Count == 0
            ? Array.Empty<FindMatchOccurrence>()
            : _matchOffsets.ToArray();

        MatchCount = Matches.Count;
        CurrentMatch = MatchCount == 0
            ? 0
            : (CurrentMatch <= 0 || CurrentMatch > MatchCount ? 1 : CurrentMatch);

        if (CurrentMatch > 0)
        {
            RaiseMatchSelected();
        }
    }

    private void CollectLiteralMatches()
    {
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var start = 0;
        var queryLength = Query.Length;

        while (start <= _searchText.Length - queryLength)
        {
            var index = _searchText.IndexOf(Query, start, comparison);
            if (index < 0) break;

            if (!WholeWord || IsWholeWordMatch(index, queryLength))
            {
                _matchOffsets.Add(new FindMatchOccurrence(index, queryLength));
            }

            start = index + Math.Max(1, queryLength);
        }
    }

    private void CollectRegexMatches()
    {
        var options = MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
        var matches = Regex.Matches(_searchText, Query, options);

        foreach (Match match in matches)
        {
            if (!match.Success || match.Length == 0) continue;
            if (WholeWord && !IsWholeWordMatch(match.Index, match.Length)) continue;

            _matchOffsets.Add(new FindMatchOccurrence(match.Index, match.Length));
        }
    }

    private bool IsWholeWordMatch(int index, int length)
    {
        var startIsBoundary = index == 0 || !IsWordCharacter(_searchText[index - 1]);
        var endIndex = index + length;
        var endIsBoundary = endIndex >= _searchText.Length || !IsWordCharacter(_searchText[endIndex]);
        return startIsBoundary && endIsBoundary;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    private void RaiseMatchSelected()
    {
        if (CurrentMatch <= 0 || CurrentMatch > Matches.Count) return;
        MatchSelected?.Invoke(Matches[CurrentMatch - 1].Start);
    }
}
