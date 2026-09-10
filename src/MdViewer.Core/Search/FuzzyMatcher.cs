namespace MdViewer.Core.Search;

/// <summary>
/// Subsequence matching and scoring for quick-open (SPECIFICATION.md 5.9).
/// Pure and allocation-light so it can run over a large workspace index
/// on every keystroke.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>
    /// True when every character of <paramref name="query"/> appears in
    /// <paramref name="candidate"/> in order, ignoring case. An empty query
    /// matches everything.
    /// </summary>
    public static bool IsMatch(ReadOnlySpan<char> candidate, ReadOnlySpan<char> query)
    {
        if (query.IsEmpty) return true;
        if (candidate.IsEmpty) return false;

        var q = 0;
        for (var c = 0; c < candidate.Length && q < query.Length; c++)
        {
            if (char.ToUpperInvariant(candidate[c]) == char.ToUpperInvariant(query[q]))
            {
                q++;
            }
        }

        return q == query.Length;
    }

    /// <summary>
    /// Higher is better; 0 means no match. Rewards, in order: a prefix match,
    /// contiguous runs, and matches that start at a word boundary. Short
    /// candidates win ties, so <c>notes.md</c> outranks <c>release-notes.md</c>
    /// for the query <c>notes</c>.
    /// </summary>
    public static int Score(ReadOnlySpan<char> candidate, ReadOnlySpan<char> query)
    {
        if (query.IsEmpty) return 1;
        if (!IsMatch(candidate, query)) return 0;

        var score = 1;
        var q = 0;
        var previousMatched = false;

        for (var c = 0; c < candidate.Length && q < query.Length; c++)
        {
            if (char.ToUpperInvariant(candidate[c]) != char.ToUpperInvariant(query[q]))
            {
                previousMatched = false;
                continue;
            }

            score += 10;
            if (c == 0) score += 40;                       // prefix
            if (previousMatched) score += 15;              // contiguous run
            if (c > 0 && IsBoundary(candidate[c - 1])) score += 20;

            previousMatched = true;
            q++;
        }

        // Prefer the shorter of two otherwise equal candidates.
        score += Math.Max(0, 40 - candidate.Length);
        return score;
    }

    private static bool IsBoundary(char c) =>
        c is ' ' or '-' or '_' or '.' or '/' or '\\';
}
