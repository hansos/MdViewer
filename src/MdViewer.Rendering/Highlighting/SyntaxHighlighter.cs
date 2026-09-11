using TextMateSharp.Grammars;
using TextMateSharp.Registry;

namespace MdViewer.Rendering.Highlighting;

/// <summary>
/// TextMate-backed tokenizer that emits scope-bucketed highlight segments
/// (SPECIFICATION.md 5.3).
///
/// This class is thread-safe for concurrent tokenization requests and is
/// designed to run off the UI thread.
/// </summary>
internal sealed class SyntaxHighlighter
{
    private readonly Registry _registry;
    private readonly TextMateGrammarCatalog _catalog;
    private readonly Dictionary<string, IGrammar?> _grammarCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _faultedScopes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly object _tokenizeSync = new();

    public SyntaxHighlighter()
    {
        var options = new RegistryOptions(ThemeName.DarkPlus);
        _registry = new Registry(options);
        _catalog = new TextMateGrammarCatalog(options);
    }

    public IReadOnlyList<HighlightSegment>? Tokenize(string code, string infoString)
    {
        if (string.IsNullOrEmpty(code)) return Array.Empty<HighlightSegment>();

        foreach (var scope in _catalog.ResolveScopeCandidates(infoString))
        {
            var grammar = GetOrLoadGrammar(scope);
            if (grammar is null) continue;

            IReadOnlyList<HighlightSegment>? segments;
            lock (_tokenizeSync)
            {
                segments = TokenizeWithGrammar(code, grammar);
            }

            if (segments is not null)
            {
                return segments;
            }

            MarkScopeFaulted(scope);
        }

        return null;
    }

    private void MarkScopeFaulted(string scope)
    {
        lock (_sync)
        {
            _faultedScopes.Add(scope);
            _grammarCache[scope] = null;
        }
    }

    private IGrammar? GetOrLoadGrammar(string scope)
    {
        lock (_sync)
        {
            if (_faultedScopes.Contains(scope))
            {
                return null;
            }

            if (_grammarCache.TryGetValue(scope, out var cached))
            {
                return cached;
            }

            IGrammar? loaded;
            try
            {
                loaded = _registry.LoadGrammar(scope);
            }
            catch
            {
                loaded = null;
            }

            _grammarCache[scope] = loaded;
            return loaded;
        }
    }

    private static IReadOnlyList<HighlightSegment>? TokenizeWithGrammar(string code, IGrammar grammar)
    {
        var segments = new List<HighlightSegment>();
        var lines = code.Split('\n');
        IStateStack? state = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            ITokenizeLineResult? result;

            try
            {
                result = state is null
                    ? grammar.TokenizeLine(line)
                    : grammar.TokenizeLine(new LineText(line), state, TimeSpan.FromMilliseconds(20));
            }
            catch
            {
                return null;
            }

            if (result is null)
            {
                return null;
            }

            state = result.RuleStack ?? state;

            try
            {
                AppendLineSegments(line, result.Tokens, segments);
            }
            catch
            {
                return null;
            }

            if (i < lines.Length - 1)
            {
                AppendSegment(segments, "\n", null);
            }
        }

        return segments;
    }

    private static void AppendLineSegments(string line, IReadOnlyList<IToken>? tokens, List<HighlightSegment> output)
    {
        if (line.Length == 0) return;

        if (tokens is null || tokens.Count == 0)
        {
            AppendSegment(output, line, null);
            return;
        }

        var position = 0;

        foreach (var token in tokens)
        {
            var start = Math.Clamp(token.StartIndex, 0, line.Length);
            var end = Math.Clamp(token.EndIndex, start, line.Length);

            if (end <= start) continue;

            if (start > position)
            {
                AppendSegment(output, line[position..start], null);
            }

            var tokenKey = SyntaxScopeMap.Resolve(token.Scopes);
            AppendSegment(output, line[start..end], tokenKey);
            position = end;
        }

        if (position < line.Length)
        {
            AppendSegment(output, line[position..], null);
        }
    }

    private static void AppendSegment(List<HighlightSegment> output, string text, string? tokenKey)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (output.Count > 0)
        {
            var last = output[^1];
            if (string.Equals(last.TokenKey, tokenKey, StringComparison.Ordinal))
            {
                output[^1] = new HighlightSegment(last.Text + text, tokenKey);
                return;
            }
        }

        output.Add(new HighlightSegment(text, tokenKey));
    }
}
