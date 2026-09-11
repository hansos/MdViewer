namespace MdViewer.Rendering.Highlighting;

/// <summary>
/// Folds a TextMate scope onto one of the seven syntax theme tokens
/// (SPECIFICATION.md 5.3).
///
/// TextMate grammars emit hundreds of scopes and a full TextMate theme would
/// bring its own colours with it — which would put code colouring outside the
/// token set and break theme switching for the one thing readers stare at
/// longest. Mapping to buckets instead keeps every colour in Tokens.axaml.
/// </summary>
internal static class SyntaxScopeMap
{
    /// <summary>
    /// Scope prefixes, most specific first by construction: lookup strips one
    /// trailing dotted segment at a time, so "keyword.operator.arithmetic.cs"
    /// finds "keyword.operator" before it would ever reach "keyword".
    /// </summary>
    private static readonly Dictionary<string, string> Buckets = new(StringComparer.Ordinal)
    {
        ["comment"] = Themed.Keys.SyntaxComment,
        ["punctuation.definition.comment"] = Themed.Keys.SyntaxComment,

        ["string"] = Themed.Keys.SyntaxString,
        ["constant.character"] = Themed.Keys.SyntaxString,
        ["punctuation.definition.string"] = Themed.Keys.SyntaxString,

        ["constant.numeric"] = Themed.Keys.SyntaxNumber,

        ["entity.name.function"] = Themed.Keys.SyntaxFunction,
        ["support.function"] = Themed.Keys.SyntaxFunction,
        ["meta.function-call"] = Themed.Keys.SyntaxFunction,
        ["variable.function"] = Themed.Keys.SyntaxFunction,

        ["entity.name.type"] = Themed.Keys.SyntaxType,
        ["entity.name.class"] = Themed.Keys.SyntaxType,
        ["entity.name.namespace"] = Themed.Keys.SyntaxType,
        ["entity.name.tag"] = Themed.Keys.SyntaxType,
        ["entity.other.inherited-class"] = Themed.Keys.SyntaxType,
        ["support.class"] = Themed.Keys.SyntaxType,
        ["support.type"] = Themed.Keys.SyntaxType,
        ["storage.type"] = Themed.Keys.SyntaxType,

        ["keyword"] = Themed.Keys.SyntaxKeyword,
        ["storage"] = Themed.Keys.SyntaxKeyword,
        ["constant.language"] = Themed.Keys.SyntaxKeyword,
        ["variable.language"] = Themed.Keys.SyntaxKeyword,

        ["keyword.operator"] = Themed.Keys.SyntaxOperator,
        ["punctuation"] = Themed.Keys.SyntaxOperator,
        ["meta.brace"] = Themed.Keys.SyntaxOperator
    };

    /// <summary>
    /// Resolves the token for a TextMate token's scope stack, or <c>null</c>
    /// when nothing in it is interesting. The stack runs general to specific,
    /// so it is walked backwards and the first match wins.
    /// </summary>
    public static string? Resolve(IReadOnlyList<string>? scopes)
    {
        if (scopes is null) return null;

        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            var token = ResolveScope(scopes[i]);
            if (token is not null) return token;
        }

        return null;
    }

    private static string? ResolveScope(string scope)
    {
        var candidate = scope;

        while (candidate.Length > 0)
        {
            if (Buckets.TryGetValue(candidate, out var token)) return token;

            var cut = candidate.LastIndexOf('.');
            if (cut < 0) break;

            candidate = candidate[..cut];
        }

        return null;
    }
}
